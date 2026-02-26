using DocAuditoria.Function.Portaria.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Interfaces
{
    public interface IMainApiIntegrationService
    {
        Task<SolicitacaoDto?> GetSolicitacaoAsync(Guid id);
        Task AtualizarStatusAsync(Guid id, string status, string erro = null);
        Task<List<int>> ObterTodosIdsAsync(int empresaId, int? idOrigem, string origem);

        Task CriarItensPendentesAsync(Guid solicitacaoId, List<int> funcionarioIds);
        Task<FuncionarioValidadePortariaViewModel> ValidarFuncionarioIndividualAsync(int empresaId, int funcionarioId);
        Task<string> ValidarStatusBloqueioNaValideAsync(int empresaId, string funcionarioId);
        Task<bool> AtualizarItemEVerificarFinalizacaoAsync(Guid solicitacaoId, int funcionarioId, FuncionarioValidadePortariaViewModel resultado);
        Task<List<FuncionarioValidadePortariaViewModel>> ObterResultadosConsolidadosAsync(Guid solicitacaoId);

        Task<bool> AtualizarItemEVerificarFinalizacaoAsync(Guid solicitacaoId, int funcionarioId, string resultado);    
    }
}
