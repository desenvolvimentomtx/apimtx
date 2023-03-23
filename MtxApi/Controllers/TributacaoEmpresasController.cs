using MtxApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;

namespace MtxApi.Controllers
{
    /*Retorna a tributação para empresa informada pelo cnpj, caso nao informe o cnpj
     no parametro trará todas as empresas*/
    public class TributacaoEmpresasController : ApiController
    {

        private MtxApiContext db = new MtxApiContext(); //acesso ao banco de dados pelos modelos

        //// GET: api/TributacaoEmpresa
        public IQueryable<TributacaoEmpresa> GetTributacaoEmpresas()
        {
            return db.TributacaoEmpresas;
        }

        //Retorna a tributacao pelo cnpj
        // GET: api/TributacaoEmpresa/5
        [Route("api/TributacaoEmpresas/{cnpj}")]
        [ResponseType(typeof(TributacaoEmpresa))]
        public IHttpActionResult GetTributacaoEmpresas(string cnpj)
        {

            //pegar a chave no header
            //pegar o header que é a chave
            var chaveToken = HttpContext.Current.Request.Headers["chave"];
            if (chaveToken == null)
            {
                return BadRequest("AUTENTICAÇÃO INCORRETA. CHAVE NECESSÁRIA");
            }


            int qtd = cnpj.Length; //pegar quantidade de digitos

            //verificar a qtd de digitos
            if (qtd != 14)
            {
                return BadRequest("CNPJ INCORRETO");
            }

            //formatando a string
            string formatado = FormatCNPJ(cnpj);

            //pesquisa no banco
            var tribempresas = from s in db.TributacaoEmpresas select s;

            tribempresas = tribempresas.Where(s => s.CNPJ_EMPRESA.Equals(formatado));

            ////verificar o retorno do banco
            if (tribempresas.Count() == 0)
            {
                return BadRequest("DADOS PARA O CNPJ: " + formatado + " NÃO ENCONTRADOS OU CNPJ INVÁLIDO");
            }
            else
            {
                var empresaCliente = from s in db.Empresas select s;
                empresaCliente = empresaCliente.Where(s => s.cnpj.Equals(formatado));
                empresaCliente = empresaCliente.Where(s => s.SoftwareHouse.Chave.Equals(chaveToken.ToString()));
                if(empresaCliente.Count() == 0)
                {
                    return BadRequest("Chave incorreta para o CNPJ: " + formatado + ": CORRESPONDENCIA INVÁLIDA");

                }
            }

            //caso esteja ok, exibir o resultado
            return Ok(new { sucess = "true", data = tribempresas.ToArray(),  totalItens = tribempresas.Count() });
            //return Ok(tribempresas);

        }

        public static string FormatCNPJ(string CNPJ)
        {
            return Convert.ToUInt64(CNPJ).ToString(@"00\.000\.000\/0000\-00");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }



    }
}
