using DocAuditoria.Function.Portaria.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Interfaces
{
    public interface IGeradorArquivoService
    {
        Stream GerarArquivo(object dados, int tipoArquivo);
        MemoryStream GerarExcelBloqueiosEstilizado(List<FuncionarioValidadePortariaViewModel> dados);
    }
}
