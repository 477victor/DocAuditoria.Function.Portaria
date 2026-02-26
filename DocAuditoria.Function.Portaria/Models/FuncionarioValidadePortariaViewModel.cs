using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocAuditoria.Function.Portaria.Models
{
    public class FuncionarioValidadePortariaViewModel
    {
        public int EstabelecimentoId { get; set; }
        public int FuncionarioId { get; set; }
        public string NomeFuncionario { get; set; }
        public string NomeFornecedor { get; set; }
        public string Matricula { get; set; }
        public DateTime? DtFimVinculoContrato { get; set; }
        public DateTime? DtValidadeDocumentoFuncionario { get; set; }
        public int EnviosFuncionario { get; set; }
        public DateTime? DtValidadeDocumentoEmpresa { get; set; }
        public int EnviosEmpresa { get; set; }
        public string Funcao { get; set; }
        public string Segmento { get; set; }
        public string StatusLiberacao { get; set; }
        public int? FornecedorId { get; set; }
        public string Contrato { get; set; }
    }
}
