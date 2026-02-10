using DocAuditoria.Function.Portaria.Interfaces;
using Newtonsoft.Json;
using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace DocAuditoria.Function.Portaria.Services
{
    public class GeradorArquivoService : IGeradorArquivoService
    {
        public Stream GerarArquivo(object dados, int tipoArquivo)
        {
            return tipoArquivo == 1 ? GerarPdf(dados) : GerarExcel(dados);
        }

        private Stream GerarPdf(object dados)
        {
            var stream = new MemoryStream();

            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true))
            {
                var jsonDebug = JsonConvert.SerializeObject(dados);
                writer.Write(jsonDebug); 

                writer.Flush(); 
            }

            stream.Position = 0;
            return stream;
        }

        private Stream GerarExcel(object dados)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var stream = new MemoryStream();

            using (var package = new ExcelPackage(stream))
            {
                var sheet = package.Workbook.Worksheets.Add("Relatorio");

                var json = JsonConvert.SerializeObject(dados);
                var listaDados = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

                if (listaDados != null && listaDados.Count > 0)
                {
                    int col = 1;
                    foreach (var key in listaDados[0].Keys)
                    {
                        sheet.Cells[1, col].Value = key.ToUpper();
                        sheet.Cells[1, col].Style.Font.Bold = true;
                        col++;
                    }

                    int row = 2;
                    foreach (var item in listaDados)
                    {
                        col = 1;
                        foreach (var valor in item.Values)
                        {
                            sheet.Cells[row, col].Value = valor;
                            col++;
                        }
                        row++;
                    }

                    sheet.Cells.AutoFitColumns();
                }

                package.Save(); 
            }

            stream.Position = 0;
            return stream;
        }
    }
}