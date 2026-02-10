using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class EmailRequestDto
    {
        public string Para { get; set; }
        public string Assunto { get; set; }
        public string Corpo { get; set; }
        public string NomeArquivo { get; set; }
        public string MimeType { get; set; }
        public byte[] ConteudoArquivo { get; set; }
    }
}
