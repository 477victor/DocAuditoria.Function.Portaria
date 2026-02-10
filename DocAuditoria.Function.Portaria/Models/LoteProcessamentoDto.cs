using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class LoteProcessamentoDto
    {
        public int EmpresaId { get; set; }
        public int UsuarioId { get; set; }
        public int? EstabelecimentoId { get; set; }
        public List<int> FuncionarioIds { get; set; }
        public bool IgnoraDocumentacao { get; set; }
    }
}
