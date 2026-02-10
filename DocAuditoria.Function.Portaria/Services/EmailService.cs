using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _client;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IHttpClientFactory httpClientFactory, ILogger<EmailService> logger)
        {
            _client = httpClientFactory.CreateClient("ValideApi");
            _logger = logger;
        }

        public async Task EnviarEmailComAnexoAsync(string para, string assunto, string corpo, Stream arquivo, string nomeArquivo, string mimeType)
        {
            try
            {
                if (arquivo.CanSeek && arquivo.Position > 0)
                    arquivo.Position = 0;

                byte[] arquivoBytes;


                if (arquivo is MemoryStream ms)
                {
                    arquivoBytes = ms.ToArray();
                }
                else
                {
                    using (var tempStream = new MemoryStream())
                    {
                        await arquivo.CopyToAsync(tempStream);
                        arquivoBytes = tempStream.ToArray();
                    }
                }

                var payload = new EmailRequestDto
                {
                    Para = para,
                    Assunto = assunto,
                    Corpo = corpo,
                    NomeArquivo = nomeArquivo,
                    MimeType = mimeType,
                    ConteudoArquivo = arquivoBytes
                };

                var response = await _client.PostAsJsonAsync("api/ValideInternal/enviar-email-relatorio", payload);

                if (!response.IsSuccessStatusCode)
                {
                    var erro = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API recusou envio: {response.StatusCode} - {erro}");
                }

                _logger.LogInformation($"[EmailService] Sucesso. API aceitou o e-mail para {para}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[EmailService] Falha: {ex.Message}");
                throw;
            }
        }
    }
}