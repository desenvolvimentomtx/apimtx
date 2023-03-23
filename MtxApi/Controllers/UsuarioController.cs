using MtxApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace MtxApi.Controllers
{
    public class UsuarioController : ApiController
    {
        private MtxApiContext db = new MtxApiContext();
        public UsuarioController()
        {
            db = new MtxApiContext();
        }
        [Route("api/UsuarioSalvar/{cnpj}")]
        public IHttpActionResult PostEmpresaSalvar(string cnpj, List<EmpresaJson> dados)
        {
            //verificar se cnpj é nulo
            if (cnpj == null)
            {
                return BadRequest("FAVOR INFORMAR O CNPJ NO PARÂMETRO");
            }
            else
            {
                //verificar a qtd de digitos
                if (cnpj.Length != 14)
                {
                    return BadRequest("CNPJ PASSADO COMO PARÂMETRO ESTÁ INCORRETO");
                }
            }

            //verificar se os dados informados no json estão nulos
            if (dados == null)
            {
                return BadRequest("JSON SEM DADOS DA EMPRESA PARA CADASTRO");
            }

            return Ok();

        }

    }
}
