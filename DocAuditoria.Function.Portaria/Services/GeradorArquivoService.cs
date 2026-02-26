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
            private readonly XLColor VALIDE_LIGHT_BLUE = XLColor.FromHtml("#4472C4");
            private readonly XLColor VALIDE_GREEN = XLColor.FromHtml("#2E8B57");
            private readonly XLColor VALIDE_RED = XLColor.FromHtml("#C0392B");
            private readonly XLColor LIGHT_GREEN_BG = XLColor.FromHtml("#E8F5E9");
            private readonly XLColor LIGHT_RED_BG = XLColor.FromHtml("#FDEDEC");
            private readonly XLColor HEADER_TEXT_COLOR = XLColor.White;

            public MemoryStream GerarExcelBloqueiosEstilizado(List<FuncionarioValidadePortariaViewModel> dados)
            {
                var ms = new MemoryStream();
                using (var wb = new XLWorkbook())
                {
                    var listaProcessada = dados.Select(d => {
                        bool liberado = false;
                        string statusTxt = "Erro ao analisar";

                        if (!string.IsNullOrEmpty(d.StatusLiberacao))
                        {
                            try
                            {
                                var json = JObject.Parse(d.StatusLiberacao);
                                liberado = json["liberado"]?.Value<bool>() ?? false;
                                statusTxt = liberado ? "Liberado" : "Bloqueado";
                            }
                            catch { }
                        }
                        return new { Item = d, IsLiberado = liberado, StatusText = statusTxt };
                    }).ToList();

                    CriarAbaResumo(wb, listaProcessada);
                    CriarAbaDados(wb, "Todos Funcionários", VALIDE_LIGHT_BLUE, listaProcessada);
                    CriarAbaDados(wb, "Liberados", VALIDE_GREEN, listaProcessada.Where(x => x.IsLiberado).ToList());
                    CriarAbaDados(wb, "Bloqueados", VALIDE_RED, listaProcessada.Where(x => !x.IsLiberado).ToList());
                    CriarAbaFornecedor(wb, listaProcessada);

                    wb.SaveAs(ms);
                }
                return ms;
            }

        private void CriarAbaResumo(XLWorkbook wb, dynamic lista)
        {
            var ws = wb.Worksheets.Add("Resumo");
            ws.TabColor = VALIDE_BLUE;

            var listaProcessada = ((IEnumerable<dynamic>)lista).ToList();
            int total = listaProcessada.Count;
            int liberados = listaProcessada.Count(x => x.IsLiberado);
            int bloqueados = total - liberados;

            var headerRange = ws.Range("B2:E2");
            headerRange.Merge().Value = "Relatório de Status de Funcionários";
            headerRange.Style.Font.SetFontSize(18).Font.SetBold().Font.SetFontColor(XLColor.White);
            headerRange.Style.Fill.SetBackgroundColor(VALIDE_BLUE);
            headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(2).Height = 45;

            ws.Range("B3:E3").Merge().Value = $"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cell(3, 2).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            RenderizarLinhaDashboard(ws, 5, "Total de Funcionários", total, VALIDE_LIGHT_BLUE);
            RenderizarLinhaDashboard(ws, 6, "Liberados", liberados, VALIDE_GREEN);
            RenderizarLinhaDashboard(ws, 7, "Bloqueados", bloqueados, VALIDE_RED);

            double percLib = total > 0 ? (double)liberados / total : 0;
            double percBloq = total > 0 ? (double)bloqueados / total : 0;

            ws.Cell(9, 2).Value = "% Liberados";
            ws.Cell(9, 2).Style.Font.SetBold();
            var cellPercLib = ws.Cell(9, 3);
            cellPercLib.Value = percLib;
            cellPercLib.Style.NumberFormat.Format = "0.0%";
            cellPercLib.Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(VALIDE_GREEN);

            // Percentual Bloqueados
            ws.Cell(10, 2).Value = "% Bloqueados";
            ws.Cell(10, 2).Style.Font.SetBold();
            var cellPercBloq = ws.Cell(10, 3);
            cellPercBloq.Value = percBloq;
            cellPercBloq.Style.NumberFormat.Format = "0.0%";
            cellPercBloq.Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(VALIDE_RED);


            ws.Columns(2, 5).AdjustToContents();
            ws.Column(3).Width = 20;
        }
        private void RenderizarLinhaDashboard(IXLWorksheet ws, int row, string label, int value, XLColor color)
        {
            var cellLabel = ws.Cell(row, 2);
            cellLabel.Value = label;
            cellLabel.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(color);

            var cellValue = ws.Cell(row, 3);
            cellValue.Value = value;
            cellValue.Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(color);
            cellValue.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Row(row).Height = 35;
        }
        private void RenderizarLinhaResumo(IXLWorksheet ws, int row, string label, int value, XLColor color)
            {
                ws.Cell(row, 2).Value = label;
                ws.Cell(row, 2).Style.Font.SetBold().Font.SetFontColor(HEADER_TEXT_COLOR).Fill.SetBackgroundColor(color);
                ws.Cell(row, 3).Value = value;
                ws.Cell(row, 3).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(color).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Row(row).Height = 35;
            }

        private void CriarAbaDados(XLWorkbook wb, string nome, XLColor tabColor, dynamic data)
        {
            var ws = wb.Worksheets.Add(nome);
            ws.TabColor = tabColor;

            string[] headers = { "#", "ID Funcionário", "Nome", "Fornecedor", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.SetBold().Fill.SetBackgroundColor(VALIDE_BLUE).Font.SetFontColor(HEADER_TEXT_COLOR);
            }

            int r = 2;
            foreach (var d in data)
            {
                ws.Cell(r, 1).Value = r - 1;
                ws.Cell(r, 2).Value = (d.Item.FuncionarioId ?? 0).ToString();
                ws.Cell(r, 3).Value = (d.Item.NomeFuncionario ?? "").ToString();
                ws.Cell(r, 4).Value = (d.Item.NomeFornecedor ?? "Sem Fornecedor").ToString();
                ws.Cell(r, 5).Value = (d.StatusText ?? "N/A").ToString();

                var rowRange = ws.Range(r, 1, r, 5);
                rowRange.Style.Fill.SetBackgroundColor(d.IsLiberado ? LIGHT_GREEN_BG : LIGHT_RED_BG);
                ws.Cell(r, 5).Style.Font.SetBold().Font.SetFontColor(d.IsLiberado ? VALIDE_GREEN : VALIDE_RED);
                r++;
            }
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
        }

        private void CriarAbaFornecedor(XLWorkbook wb, dynamic listaCompleta)
            {
                var ws = wb.Worksheets.Add("Por Fornecedor");
                ws.TabColor = XLColor.Purple;

                var grupos = ((IEnumerable<dynamic>)listaCompleta)
                    .GroupBy(x => x.Item.NomeFornecedor ?? "Sem Fornecedor")
                    .Select(g => new {
                        Fornecedor = g.Key,
                        Total = g.Count(),
                        Lib = g.Count(x => x.IsLiberado),
                        Bloq = g.Count(x => !x.IsLiberado),
                        Perc = g.Count() > 0 ? (double)g.Count(x => x.IsLiberado) / g.Count() : 0
                    }).OrderByDescending(x => x.Total).ToList();

                string[] headers = { "#", "Fornecedor", "Total", "Lib.", "Bloq.", "% Lib." };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.SetBold().Fill.SetBackgroundColor(VALIDE_BLUE).Font.SetFontColor(HEADER_TEXT_COLOR);
                }

                int r = 2;
                foreach (var g in grupos)
                {
                    ws.Cell(r, 1).Value = r - 1;
                    ws.Cell(r, 2).Value = (g.Fornecedor ?? "Sem Fornecedor").ToString();
                    ws.Cell(r, 3).Value = (int)g.Total;
                    ws.Cell(r, 4).Value = (int)g.Lib;
                    ws.Cell(r, 5).Value = (int)g.Bloq;
                    ws.Cell(r, 6).Value = g.Perc;
                    ws.Cell(r, 6).Style.NumberFormat.Format = "0.0%";
                    r++;
                }
                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);
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
                    cell.Value = mapaColunas[nomeTecnico].ToUpper(); 

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