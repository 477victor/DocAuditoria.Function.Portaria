using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class RelatorioQueueMessage
    {
        public Guid SolicitacaoId { get; set; }
        public int EmpresaId { get; set; }
        public int UsuarioId { get; set; }
        public int TipoArquivo { get; set; } 
        public int? EstabelecimentoId { get; set; }
        public bool IgnoraDocumentacaoEmpresa { get; set; }
    }
}
