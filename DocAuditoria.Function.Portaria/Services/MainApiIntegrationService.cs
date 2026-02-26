using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
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
        private string _cachedToken = string.Empty;
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

        public async Task<List<int>> ObterTodosIdsAsync(int empresaId, int? idOrigem, string origem)
        {
            var dto = new FiltroRelatorioDto { EmpresaId = empresaId };

            // Lógica condicional baseada na origem dos dados
            if (origem.Equals("Portaria", StringComparison.OrdinalIgnoreCase))
            {
                dto.FornecedorId = idOrigem;
            }
            else if (origem.Equals("Bloqueio", StringComparison.OrdinalIgnoreCase))
            {
                dto.UnidadeId = idOrigem;
            }

            var response = await _client.PostAsJsonAsync("api/ValideInternal/listar-ids-funcionario", dto);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<int>>();
            }

            return new List<int>();
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

        public async Task<FuncionarioValidadePortariaViewModel> ValidarFuncionarioIndividualAsync(int empresaId, int funcionarioId)
        {
            var payload = new
            {
                EmpresaId = empresaId,
                UsuarioId = 7, 
                FuncionarioId = funcionarioId, 
                IgnoraDocumentacao = false
            };

            var response = await _client.PostAsJsonAsync("api/ValideInternal/validar-lote-relatorio", payload);

            if (response.IsSuccessStatusCode)
            {
                var resultado = await response.Content.ReadFromJsonAsync<FuncionarioValidadePortariaViewModel>();
                return resultado;
            }

            _logger.LogError($"[PORTARIA] Falha na API Interna: {response.StatusCode}");
            return null;
        }

        public async Task<string> ValidarStatusBloqueioNaValideAsync(int empresaId, string funcionarioId)
        {
            string token = await ObterTokenValideAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.GetAsync($"api/fiscalization/check-entrance-device/{funcionarioId}");
            return await response.Content.ReadAsStringAsync(); 
        }
        private async Task<string> ObterTokenValideAsync()
        {

            if (!string.IsNullOrEmpty(_cachedToken)) return _cachedToken;

            var loginPayload = new
            {
                email = "samanta.marcolino@validesolucoes.com.br",
                password = "12345",
                apiKey = "713CE135-9F48-46F1-993A-2D153EE5023F"
            };

            var response = await _client.PostAsJsonAsync("https://valide-api-v1.azurewebsites.net/api/auth/login", loginPayload);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>();
                _cachedToken = content.TryGetProperty("token", out var t) ? t.GetString() :
                               content.TryGetProperty("accessToken", out var at) ? at.GetString() :
                               content.TryGetProperty("tokenAPI", out var tapi) ? tapi.GetString() : string.Empty;

                return _cachedToken;
            }

            throw new Exception($"Falha na autenticação com a API Valide: {response.StatusCode}");
        }

        public async Task<bool> AtualizarItemEVerificarFinalizacaoAsync(Guid solicitacaoId, int funcionarioId, string resultado)
        {
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
