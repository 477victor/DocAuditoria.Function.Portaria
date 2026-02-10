using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Interfaces
{
    public interface IEmailService
    {
        Task EnviarEmailComAnexoAsync(string para, string assunto, string corpo, Stream arquivo, string nomeArquivo, string mimeType);
    }
}

