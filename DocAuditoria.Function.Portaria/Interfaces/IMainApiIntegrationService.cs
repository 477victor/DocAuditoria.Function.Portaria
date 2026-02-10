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
        Task<List<int>> ObterTodosIdsAsync(int empresaId, int? estabelecimentoId);
        Task<List<FuncionarioValidadeResultViewModel>> ProcessarLotesAsync(RelatorioQueueMessage pedido, List<int> todosIds);
    }
}
