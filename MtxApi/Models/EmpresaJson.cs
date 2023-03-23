using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MtxApi.Models
{
    public class EmpresaJson
    {
        public string RAZAO_SOCIAL { get; set; }
        public string FANTASIA { get; set; }
        public string CNPJ_EMPRESA { get; set; }

        public string CNPJ_SOFTWAREHOUSE { get; set; }

        public string CHAVE_TOKEN { get; set; }
        public string LOGRADOURO { get; set; }
        public string NUMERO { get; set; }
        public string CEP { get; set; }
        public string COMPLEMENTO { get; set; }
        public string CIDADE { get; set; }
        public string ESTADO { get; set; }
        public string TELEFONE { get; set; }
        public string EMAIL { get; set; }

        public string USUARIO_ADMIN_INICIAL { get; set; }

    }
}