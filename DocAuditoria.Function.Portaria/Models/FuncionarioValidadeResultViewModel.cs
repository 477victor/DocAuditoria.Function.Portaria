using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class FuncionarioValidadeResultViewModel
    {
        public int FuncionarioId { get; set; }
        public string NomeFuncionario { get; set; }
        public string StatusLiberacao { get; set; }
        public DateTime? DtValidadeDocumentoFuncionario { get; set; }
        public string Contrato { get; set; }
    }
}
