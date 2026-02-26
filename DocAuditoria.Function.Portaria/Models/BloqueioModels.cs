using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class RelatorioBloqueioMessage
    {
        public Guid SolicitacaoId { get; set; }
        public int EmpresaId { get; set; }
        public int? EstabelecimentoId { get; set; }
        public string EmailDestino { get; set; }
    }

    public class ItemBloqueioMessage
    {
        public Guid SolicitacaoId { get; set; }
        public string FuncionarioId { get; set; }
        public int EmpresaId { get; set; }
    }
}
