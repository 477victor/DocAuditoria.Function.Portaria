using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class ItemProcessamentoMessage
    {
        public Guid SolicitacaoId { get; set; }
        public int FuncionarioId { get; set; }
        public int EmpresaId { get; set; }
        public int TipoArquivo { get; set; }
    }

    public class FinalizarRelatorioMessage
    {
        public Guid SolicitacaoId { get; set; }
        public int TipoArquivo { get; set; }
    }
}
