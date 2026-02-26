using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class FiltroRelatorioDto
    {
        public int EmpresaId { get; set; }
        public int? UnidadeId { get; set; }
        public int? PortariaId { get; set; }        
        public int? FornecedorId { get; set; }     
        public bool? BloqueioEstabelecimento { get; set; } 
    }
}
