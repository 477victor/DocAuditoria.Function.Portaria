using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class RelatorioBloqueioMessage
    {
        public RelatorioBloqueioMessage()
        {
            EstabelecimentosIds = new List<int>(); // Garante que nunca seja null
        }
        public Guid SolicitacaoId { get; set; }
        public int EmpresaId { get; set; }
        public int? EstabelecimentoId { get; set; }
        public string EmailDestino { get; set; }
        public List<int>? EstabelecimentosIds { get; set; }
    }

    public class ItemBloqueioMessage
    {
        public Guid SolicitacaoId { get; set; }
        public string FuncionarioId { get; set; }
        public int EmpresaId { get; set; }
    }
}
