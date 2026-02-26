using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;

namespace DocAuditoria.Function.Portaria
{
    public class BloqueioWorker
    {
        private readonly ILogger<BloqueioWorker> _logger;
        private readonly IMainApiIntegrationService _apiService;
        private readonly IGeradorArquivoService _geradorService;
        private readonly IEmailService _emailService;

        public BloqueioWorker(ILogger<BloqueioWorker> logger, IMainApiIntegrationService apiService,
                              IGeradorArquivoService geradorService, IEmailService emailService)
        {
            _logger = logger;
            _apiService = apiService;
            _geradorService = geradorService;
            _emailService = emailService;
        }

        [Function("Bloqueio_Step1_Distribuidor")]
        public async Task<Step1BloqueioOutput> RunStep1(
        [ServiceBusTrigger("topico-solicitacao-bloqueio", "sub-worker-bloqueio", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions)
        {
            var bodyString = message.Body.ToString();
            if (string.IsNullOrEmpty(bodyString))
            {
                await messageActions.DeadLetterMessageAsync(message);
                return null;
            }

            var payload = message.Body.ToObjectFromJson<RelatorioBloqueioMessage>();

            if (payload == null || payload.EstabelecimentosIds == null)
            {
                payload.EstabelecimentosIds = new List<int>();
            }

            var todosIdsFuncionarios = new HashSet<int>();

            foreach (var estId in payload.EstabelecimentosIds)
            {
                var idsDoEstabelecimento = await _apiService.ObterTodosIdsAsync(payload.EmpresaId, estId);

                if (idsDoEstabelecimento != null)
                {
                    foreach (var funcId in idsDoEstabelecimento)
                    {
                        todosIdsFuncionarios.Add(funcId);
                    }
                }
            }

            await _apiService.CriarItensPendentesAsync(payload.SolicitacaoId, todosIdsFuncionarios.ToList());

            var mensagens = todosIdsFuncionarios.Select(id => JsonSerializer.Serialize(new ItemBloqueioMessage
            {
                SolicitacaoId = payload.SolicitacaoId,
                FuncionarioId = id.ToString(),
                EmpresaId = payload.EmpresaId
            })).ToArray();

            await messageActions.CompleteMessageAsync(message);
            return new Step1BloqueioOutput { Mensagens = mensagens };
        }

        [Function("Bloqueio_Step2_Verificador")]
        public async Task<Step2BloqueioOutput> RunStep2(
         [ServiceBusTrigger("fila-processamento-bloqueio", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
         ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions)
        {
            var item = message.Body.ToObjectFromJson<ItemBloqueioMessage>();

            var resultadoJson = await _apiService.ValidarStatusBloqueioNaValideAsync(
                item.EmpresaId,
                item.FuncionarioId
            );

            bool finalizou = await _apiService.AtualizarItemEVerificarFinalizacaoAsync(
                item.SolicitacaoId,
                int.Parse(item.FuncionarioId),
                resultadoJson
            );

            string? output = finalizou ? JsonSerializer.Serialize(new { SolicitacaoId = item.SolicitacaoId }) : null;
            await messageActions.CompleteMessageAsync(message);
            return new Step2BloqueioOutput { ProximaFila = output };
        }

        [Function("Bloqueio_Step3_GeradorExcel")]
        public async Task RunStep3(
            [ServiceBusTrigger("fila-geracao-bloqueio-excel", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions)
        {
            var final = message.Body.ToObjectFromJson<FinalizarRelatorioMessage>();

            var dados = await _apiService.ObterResultadosConsolidadosAsync(final.SolicitacaoId);
            var info = await _apiService.GetSolicitacaoAsync(final.SolicitacaoId);

            using (var stream = _geradorService.GerarExcelBloqueiosEstilizado(dados))
            {
                stream.Position = 0;
                await _emailService.EnviarEmailComAnexoAsync(
                    info.EmailDestino,
                    "Relatório de Bloqueios Valide",
                    "Segue em anexo o relatório detalhado de bloqueios consolidado por estabelecimento.",
                    stream,
                    $"Relatorio_Bloqueios_{DateTime.Now:yyyyMMdd}.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                );
            }

            await _apiService.AtualizarStatusAsync(final.SolicitacaoId, "CONCLUIDO");
            await messageActions.CompleteMessageAsync(message);
        }
    }

    public class Step1BloqueioOutput
    {
        [ServiceBusOutput("fila-processamento-bloqueio", Connection = "ServiceBusConnection")]
        public string[] Mensagens { get; set; }
    }

    public class Step2BloqueioOutput
    {
        [ServiceBusOutput("fila-geracao-bloqueio-excel", Connection = "ServiceBusConnection")]
        public string? ProximaFila { get; set; }
    }
}