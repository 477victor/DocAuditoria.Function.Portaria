using ClosedXML.Excel;
using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace DocAuditoria.Function.Portaria.Services
{
    public class GeradorArquivoService : IGeradorArquivoService
    {
        private readonly string CorPrimariaHex = "#0D0B23";

        private readonly Dictionary<string, string> mapaColunas = new Dictionary<string, string>
        {
            { "NomeFuncionario", "NOME" },
            { "NomeFornecedor", "FORNECEDOR" },
            { "Matricula", "MATRÍCULA" },
            { "Funcao", "FUNÇÃO" },
            { "Contrato", "CONTRATO" },
            { "StatusLiberacao", "STATUS" },
            { "DtValidadeDocumentoFuncionario", "VALIDADE FUNCIONÁRIO" },
            { "DtValidadeDocumentoEmpresa", "VALIDADE EMPRESA" }
        };

        private readonly XLColor VALIDE_BLUE = XLColor.FromHtml("#003366");
        private readonly XLColor VALIDE_GREEN = XLColor.FromHtml("#2E8B57");
        private readonly XLColor VALIDE_RED = XLColor.FromHtml("#C0392B");
        private readonly XLColor LIGHT_GREEN_BG = XLColor.FromHtml("#E8F5E9");
        private readonly XLColor LIGHT_RED_BG = XLColor.FromHtml("#FDEDEC");

        public MemoryStream GerarExcelBloqueiosEstilizado(List<FuncionarioValidadePortariaViewModel> dados)
        {
            var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Relatório de Bloqueios");

                // Header
                string[] headers = { "#", "ID Funcionário", "Nome", "Fornecedor", "Status" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Fill.BackgroundColor = VALIDE_BLUE;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Font.Bold = true;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (int i = 0; i < dados.Count; i++)
                {
                    var item = dados[i];
                    int row = i + 2;

                    // Lógica para extrair o status do JSON que a API devolve
                    bool isLiberado = false;
                    string statusTexto = "Erro";

                    if (!string.IsNullOrEmpty(item.StatusLiberacao))
                    {
                        try
                        {
                            // Tenta ler o campo 'liberado' do JSON retornado pela API
                            var json = JObject.Parse(item.StatusLiberacao);
                            isLiberado = json["liberado"]?.Value<bool>() ?? false;
                            statusTexto = isLiberado ? "Liberado" : "Bloqueado";
                        }
                        catch { statusTexto = "Formato Inválido"; }
                    }

                    ws.Cell(row, 1).Value = i + 1;
                    ws.Cell(row, 2).Value = item.FuncionarioId.ToString(); // Resolvido int para string
                    ws.Cell(row, 3).Value = item.NomeFuncionario;
                    ws.Cell(row, 4).Value = item.NomeFornecedor;
                    ws.Cell(row, 5).Value = statusTexto;

                    // Estilização condicional
                    var bgColor = isLiberado ? LIGHT_GREEN_BG : LIGHT_RED_BG;
                    ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = bgColor;

                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Cell(row, 5).Style.Font.FontColor = isLiberado ? VALIDE_GREEN : VALIDE_RED;
                    ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);
                wb.SaveAs(ms);
            }
            return ms;
        }
    
        public Stream GerarArquivo(object dados, int tipoArquivo)
        {
            var json = JsonConvert.SerializeObject(dados);
            var listaDados = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

            var colunasVivas = listaDados
                .SelectMany(d => d.Keys)
                .Distinct()
                .Where(k => mapaColunas.ContainsKey(k)) 
                .Where(key => listaDados.Any(d => d.ContainsKey(key) && d[key] != null && !string.IsNullOrWhiteSpace(d[key].ToString())))
                .ToList();

            return tipoArquivo == 1 ? GerarPdf(listaDados, colunasVivas) : GerarExcel(listaDados, colunasVivas);
        }

        private Stream GerarPdf(List<Dictionary<string, object>> listaDados, List<string> colunasVivas)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Text("RELATÓRIO LISTA DE LIBERAÇÃO - VALIDE").FontSize(16).SemiBold().FontColor(CorPrimariaHex);

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var col in colunasVivas)
                            {
                                if (col.Contains("Nome") || col.Contains("Fornecedor")) columns.RelativeColumn(3);
                                else columns.RelativeColumn(1.5f);
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var nomeTecnico in colunasVivas)
                            {
                                var nomeExibicao = mapaColunas[nomeTecnico]; 

                                header.Cell().Background(CorPrimariaHex).Padding(5)
                                    .Text(nomeExibicao.ToUpper())
                                    .FontColor(Colors.White)
                                    .FontSize(10)
                                    .Bold()
                                    .Italic();
                            }
                        });

                        foreach (var item in listaDados)
                        {
                            foreach (var nomeTecnico in colunasVivas)
                            {
                                var valor = item.TryGetValue(nomeTecnico, out var v) ? v?.ToString() : "";
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(valor).FontSize(9);
                            }
                        }
                    });
                });
            }).GeneratePdf(stream);

            stream.Position = 0;
            return stream;
        }

        private Stream GerarExcel(List<Dictionary<string, object>> listaDados, List<string> colunasVivas)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var stream = new MemoryStream();

            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("RELATORIO");
                var corFundo = ColorTranslator.FromHtml(CorPrimariaHex);

                int col = 1;
                foreach (var nomeTecnico in colunasVivas)
                {
                    var cell = sheet.Cells[1, col];
                    cell.Value = mapaColunas[nomeTecnico].ToUpper(); // Usa o nome amigável

                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Italic = true;
                    cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(corFundo);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    col++;
                }

                int row = 2;
                foreach (var item in listaDados)
                {
                    col = 1;
                    foreach (var nomeTecnico in colunasVivas)
                    {
                        var cell = sheet.Cells[row, col];
                        if (item.TryGetValue(nomeTecnico, out object valor))
                            cell.Value = valor is DateTime dt ? dt.ToShortDateString() : valor?.ToString();

                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        col++;
                    }
                    row++;
                }

                sheet.Cells.AutoFitColumns();
                package.SaveAs(stream);
            }

            stream.Position = 0;
            return stream;
        }
    }
}