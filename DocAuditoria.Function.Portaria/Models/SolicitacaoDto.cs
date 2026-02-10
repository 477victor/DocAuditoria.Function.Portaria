using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class SolicitacaoDto
    {
        public Guid Id { get; set; }
        public string EmailDestino { get; set; }
        public string NomeUsuario { get; set; }
        public string Status { get; set; }
    }
}
