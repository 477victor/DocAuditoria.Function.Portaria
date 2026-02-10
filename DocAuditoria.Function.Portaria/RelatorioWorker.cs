using Azure.Messaging.ServiceBus;
using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria
{
    public class RelatorioWorker
    {
        private readonly ILogger<RelatorioWorker> _logger;
        private readonly IMainApiIntegrationService _apiService;
        private readonly IGeradorArquivoService _geradorService;
        private readonly IEmailService _emailService;

        public RelatorioWorker(
            ILogger<RelatorioWorker> logger,
            IMainApiIntegrationService apiService,
            IGeradorArquivoService geradorService,
            IEmailService emailService)
        {
            _logger = logger;
            _apiService = apiService;
            _geradorService = geradorService;
            _emailService = emailService;
        }

        [Function("ProcessarRelatorioPortaria")]
        public async Task Run(
            [ServiceBusTrigger("fila-relatorios-portaria", Connection = "ServiceBusConnection", AutoCompleteMessages = false )]
             ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, CancellationToken cancellationToken)
        {
            RelatorioQueueMessage Queue = new();

            try
            {

                var payload = message.Body.ToObjectFromJson<RelatorioQueueMessage>();

                _logger.LogInformation($"[START] Solicitacao: {payload.SolicitacaoId}");

                await _apiService.AtualizarStatusAsync(payload.SolicitacaoId, "PROCESSANDO");

                var ids = await _apiService.ObterTodosIdsAsync(payload.EmpresaId, payload.EstabelecimentoId);

                if (ids == null || !ids.Any())
                {
                    await _apiService.AtualizarStatusAsync(payload.SolicitacaoId, "CONCLUIDO");
                    return;
                }

                _logger.LogInformation($"Processando {ids.Count} funcionários...");
                var dadosValidados = await _apiService.ProcessarLotesAsync(payload, ids);

                _logger.LogInformation("Gerando documento...");


                using (var fileStream = _geradorService.GerarArquivo(dadosValidados, payload.TipoArquivo))
                {
                    var info = await _apiService.GetSolicitacaoAsync(payload.SolicitacaoId);
                    string ext = payload.TipoArquivo == 1 ? "pdf" : "xlsx";
                    string mime = payload.TipoArquivo == 1 ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                    await _emailService.EnviarEmailComAnexoAsync(
                        info.EmailDestino,
                        "Relatório Portaria",
                        "Segue anexo o relatório solicitado.",
                        fileStream,
                        $"Relatorio_{DateTime.Now:yyyyMMdd}.{ext}",
                        mime
                    );
                }

                await _apiService.AtualizarStatusAsync(payload.SolicitacaoId, "CONCLUIDO");
                await messageActions.CompleteMessageAsync(message, cancellationToken);

                _logger.LogInformation("[SUCCESS] Finalizado.");
            }
            catch (Exception ex)
            {
                if (message.DeliveryCount >= 5)
                {
                    await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Número máximo de tentativas", deadLetterErrorDescription: ex.Message);
                }
                else
                {
                    await messageActions.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                }
            }
        }
    }
}