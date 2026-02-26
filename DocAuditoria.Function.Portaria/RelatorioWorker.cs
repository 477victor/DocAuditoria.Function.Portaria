using Azure.Messaging.ServiceBus;
using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace DocAuditoria.Function.Portaria
{
    public class PortariaWorker
    {
        private readonly ILogger<PortariaWorker> _logger;
        private readonly IMainApiIntegrationService _apiService;
        private readonly IGeradorArquivoService _geradorService;
        private readonly IEmailService _emailService;

        public PortariaWorker(
            ILogger<PortariaWorker> logger,
            IMainApiIntegrationService apiService,
            IGeradorArquivoService geradorService,
            IEmailService emailService)
        {
            _logger = logger;
            _apiService = apiService;
            _geradorService = geradorService;
            _emailService = emailService;
        }

        // --- STEP 1: DISTRIBUIDOR ---
        [Function("Step1_Distribuidor")]
        public async Task<Step1Output> RunStep1(
            [ServiceBusTrigger("topico-solicitacao-portaria", "sub-worker-distribuidor", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            CancellationToken cancellationToken)
        {
            try
            {
                var payload = message.Body.ToObjectFromJson<RelatorioQueueMessage>();
                _logger.LogInformation($"[STEP 1] Iniciando Distribuição: {payload.SolicitacaoId}");

                await _apiService.AtualizarStatusAsync(payload.SolicitacaoId, "DISTRIBUINDO");

                var ids = await _apiService.ObterTodosIdsAsync(payload.EmpresaId, payload.EstabelecimentoId, "Portaria");

                await _apiService.CriarItensPendentesAsync(payload.SolicitacaoId, ids);

                var mensagensJson = ids.Select(id => JsonSerializer.Serialize(new ItemProcessamentoMessage
                {
                    SolicitacaoId = payload.SolicitacaoId,
                    FuncionarioId = id,
                    EmpresaId = payload.EmpresaId,
                    TipoArquivo = payload.TipoArquivo
                })).ToArray();

                await messageActions.CompleteMessageAsync(message, cancellationToken);

                return new Step1Output { MensagensParaFila2 = mensagensJson };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro no Step 1: {ex.Message}");
                throw;
            }
        }

        // --- STEP 2: PROCESSADOR ITEM (CORRIGIDO COM ICOLLECTOR) ---
        [Function("Step2_ProcessadorItem")]
        public async Task<Step2Output> RunStep2(
        [ServiceBusTrigger("fila-processamento-item", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
        {
            string? mensagemSaida = null;

            try
            {
                string corpoJson = message.Body.ToString();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var item = JsonSerializer.Deserialize<ItemProcessamentoMessage>(corpoJson, options);

                if (item == null || item.SolicitacaoId == Guid.Empty)
                {
                    await messageActions.DeadLetterMessageAsync(message);
                    return new Step2Output(); 
                }

                _logger.LogInformation($"[STEP 2] Validando Funcionário: {item.FuncionarioId}");

                var resultado = await _apiService.ValidarFuncionarioIndividualAsync(item.EmpresaId, item.FuncionarioId);
                bool finalizouTudo = await _apiService.AtualizarItemEVerificarFinalizacaoAsync(item.SolicitacaoId, item.FuncionarioId, resultado);

                if (finalizouTudo)
                {
                    var finalMsg = new FinalizarRelatorioMessage { SolicitacaoId = item.SolicitacaoId, TipoArquivo = item.TipoArquivo };
                    mensagemSaida = JsonSerializer.Serialize(finalMsg);
                    _logger.LogInformation($"[STEP 2] Lote Finalizado! Preparando Step 3.");
                }

                await messageActions.CompleteMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[STEP 2] Erro: {ex.Message}");
                await messageActions.AbandonMessageAsync(message, null, cancellationToken);
                throw;
            }

            return new Step2Output { MensagemParaFila3 = mensagemSaida };
        }

        [Function("Step3_GeradorRelatorio")]
        public async Task RunStep3(
            [ServiceBusTrigger("fila-geracao-relatorio", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
             ServiceBusReceivedMessage message, 
            ServiceBusMessageActions messageActions,
            CancellationToken cancellationToken)
        {
            try
            {
                string corpoJson = Encoding.UTF8.GetString(message.Body.ToArray());
                _logger.LogInformation($"[STEP 3] Processando JSON: {corpoJson}");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var final = JsonSerializer.Deserialize<FinalizarRelatorioMessage>(corpoJson, options);

                if (final == null || final.SolicitacaoId == Guid.Empty)
                {
                    await messageActions.DeadLetterMessageAsync(message, propertiesToModify: new Dictionary<string, object> { { "Erro", "ID_VAZIO" } });
                    return;
                }

                var dados = await _apiService.ObterResultadosConsolidadosAsync(final.SolicitacaoId);
                var info = await _apiService.GetSolicitacaoAsync(final.SolicitacaoId);

                if (info == null)
                {
                    _logger.LogError($"[STEP 3] Solicitação {final.SolicitacaoId} não encontrada (404).");
                    await messageActions.DeadLetterMessageAsync(message, propertiesToModify: new Dictionary<string, object> { { "Erro", "404_API" } });
                    return;
                }

                using (var stream = _geradorService.GerarArquivo(dados, final.TipoArquivo))
                {
                    stream.Position = 0;

                    string ext = final.TipoArquivo == 1 ? "pdf" : "xlsx";
                    string mime = final.TipoArquivo == 1 ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                    await _emailService.EnviarEmailComAnexoAsync(
                        info.EmailDestino,
                        "Relatório Disponível",
                        "O arquivo PDF solicitado segue em anexo.",
                        stream,
                        $"Relatorio_{final.SolicitacaoId}.{ext}",
                        mime
                    );
                }

                await _apiService.AtualizarStatusAsync(final.SolicitacaoId, "CONCLUIDO");
                await messageActions.CompleteMessageAsync(message, cancellationToken);
                _logger.LogInformation($"[STEP 3] Sucesso total!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[STEP 3] Erro fatal: {ex.Message}");
                if (message != null) await messageActions.AbandonMessageAsync(message);
            }
        }
    }

    public class Step1Output
    {
        [ServiceBusOutput("fila-processamento-item", Connection = "ServiceBusConnection")]
        public string[] MensagensParaFila2 { get; set; }
    }

    public class Step2Output
    {
        [ServiceBusOutput("fila-geracao-relatorio", Connection = "ServiceBusConnection")]
        public string? MensagemParaFila3 { get; set; }
    }
}