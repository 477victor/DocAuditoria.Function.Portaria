using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class UpdateStatusDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; }
        public DateTime? DataConclusao { get; set; }
        public string MensagemErro { get; set; }
    }
}
