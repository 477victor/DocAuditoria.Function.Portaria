using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

        public async Task CriarItensPendentesAsync(Guid solicitacaoId, List<int> funcionarioIds)
        {
            await _client.PostAsJsonAsync($"api/ValideInternal/solicitacao/{solicitacaoId}/registrar-itens", funcionarioIds);
        }

        public async Task<FuncionarioValidadePortariaViewModel> ValidarFuncionarioIndividualAsync(int empresaId, int funcionarioId)
        {
            var response = await _client.PostAsJsonAsync("api/ValideInternal/validar-lote-relatorio", new
            {
                EmpresaId = empresaId,
                FuncionarioIds = new List<int> { funcionarioId }
            });

            if (response.IsSuccessStatusCode)
            {
                var lista = await response.Content.ReadFromJsonAsync<List<FuncionarioValidadePortariaViewModel>>();
                return lista?.FirstOrDefault();
            }
            return null;
        }

        public async Task<bool> AtualizarItemEVerificarFinalizacaoAsync(Guid solicitacaoId, int funcionarioId, FuncionarioValidadePortariaViewModel resultado)
        {
            var response = await _client.PutAsJsonAsync($"api/ValideInternal/solicitacao/{solicitacaoId}/item/{funcionarioId}/concluir", resultado);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (jsonDoc.TryGetProperty("finalizouSolicitacao", out var prop))
                {
                    return prop.GetBoolean();
                }
            }

            _logger.LogWarning($"[API] Falha ao concluir item {funcionarioId}. Status: {response.StatusCode}");
            return false;
        }

        public async Task<List<FuncionarioValidadePortariaViewModel>> ObterResultadosConsolidadosAsync(Guid solicitacaoId)
        {
            var response = await _client.GetAsync($"api/ValideInternal/solicitacao/{solicitacaoId}/consolidado");
            if (!response.IsSuccessStatusCode) return new List<FuncionarioValidadePortariaViewModel>();
            return await response.Content.ReadFromJsonAsync<List<FuncionarioValidadePortariaViewModel>>();
        }

        public async Task<string> ValidarStatusBloqueioNaValideAsync(int empresaId, string funcionarioId)
        {
            try
            {
                // O HttpClient já deve estar configurado com a BaseAddress da API da Valide
                // Conforme seu script Node: GET em fiscalization/check-entrance-device/{id}
                var response = await _client.GetAsync($"api/fiscalization/check-entrance-device/{funcionarioId}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                return $"{{ \"liberado\": false, \"erro\": \"Status API: {response.StatusCode}\" }}";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro ao validar bloqueio na Valide: {ex.Message}");
                return $"{{ \"liberado\": false, \"erro\": \"{ex.Message}\" }}";
            }
        }

        public async Task<bool> AtualizarItemEVerificarFinalizacaoAsync(Guid solicitacaoId, int funcionarioId, string resultado)
        {
            // Criamos um objeto temporário para enviar o resultado como StatusLiberacao
            var payload = new { StatusLiberacao = resultado };

            var response = await _client.PutAsJsonAsync($"api/ValideInternal/solicitacao/{solicitacaoId}/item/{funcionarioId}/concluir", payload);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (jsonDoc.TryGetProperty("finalizouSolicitacao", out var prop))
                {
                    return prop.GetBoolean();
                }
            }

            return false;
        }

    }
}
