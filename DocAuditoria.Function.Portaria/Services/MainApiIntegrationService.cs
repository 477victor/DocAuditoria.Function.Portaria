using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Services
{
    public class MainApiIntegrationService : IMainApiIntegrationService
    {
        private readonly HttpClient _client;
        private readonly ILogger<MainApiIntegrationService> _logger;

        public MainApiIntegrationService(IHttpClientFactory httpClientFactory, ILogger<MainApiIntegrationService> logger)
        {
            _client = httpClientFactory.CreateClient("ValideApi");
            _logger = logger;
        }

        public async Task<SolicitacaoDto?> GetSolicitacaoAsync(Guid id)
        {
            var response = await _client.GetAsync($"api/ValideInternal/solicitacao/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SolicitacaoDto>();
        }

        public async Task AtualizarStatusAsync(Guid id, string status, string erro = null)
        {
            var payload = new UpdateStatusDto { Id = id, Status = status, MensagemErro = erro };
            await _client.PutAsJsonAsync("api/ValideInternal/solicitacao/status", payload);
        }

        public async Task<List<int>> ObterTodosIdsAsync(int empresaId, int? estabelecimentoId)
        {
            var dto = new FiltroRelatorioDto { EmpresaId = empresaId, EstabelecimentoId = estabelecimentoId };
            var response = await _client.PostAsJsonAsync("api/ValideInternal/listar-ids-funcionario", dto);

            if (!response.IsSuccessStatusCode) return new List<int>();
            return await response.Content.ReadFromJsonAsync<List<int>>();
        }

        public async Task<List<FuncionarioValidadeResultViewModel>> ProcessarLotesAsync(RelatorioQueueMessage pedido, List<int> todosIds)
        {
            var bagResultados = new ConcurrentBag<FuncionarioValidadeResultViewModel>();

            var lotes = todosIds.Chunk(5).ToList();


            var options = new ParallelOptions { MaxDegreeOfParallelism = 1 };

            await Parallel.ForEachAsync(lotes, options, async (loteIds, token) =>
            {
                try
                {
                    var response = await _client.PostAsJsonAsync("api/ValideInternal/validar-lote-relatorio", new
                    {
                        EmpresaId = pedido.EmpresaId,
                        FuncionarioIds = loteIds
                    }, token);

                    if (response.IsSuccessStatusCode)
                    {
                        var dados = await response.Content.ReadFromJsonAsync<List<FuncionarioValidadeResultViewModel>>();
                        foreach (var d in dados) bagResultados.Add(d);
                    }
                    else
                    {
                        Console.WriteLine($"Falha no lote: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro de conexão no lote: {ex.Message}");
                }
            });

            return bagResultados.ToList();
        }
    }
}
