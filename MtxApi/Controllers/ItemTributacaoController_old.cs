using MtxApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Web.Http;

namespace MtxApi.Controllers
{
    public class ItemTributacaoController : ApiController
    {
        //log
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //Enviar dados para o banco de tributação das empresas pelo cnpj
        //Os dados vao para lista ItemTributaçãoJson que contem todos os campos
        // POST: api/ItemTributacao/123
        [Route("api/ItemTributacao/{cnpj}")]
        public IHttpActionResult PostListaTributacao(string cnpj, List<ItemTributacaoJson> itens)
        {
            //contador de intens que não possuem codigo de barras
            int aux = 0;
            int prodZerado = 0;
            //verificar a qtd de digitos
            if (cnpj.Length != 14)
            {
                return BadRequest("CNPJ PASSADO COMO PARÂMETRO ESTÁ INCORRETO");
            }
            if (itens == null)
            {
                return BadRequest("NENHUM ITEM INFORMADO PARA IMPORTAÇÃO");
            }

            /*CONFIRMAR EXISTENCIA DA EMPRESA */

            //formatando a string
            string cnpjFormatado = FormataCnpj.FormatarCNPJ(cnpj);

            //Instancia do contexto do banco
            MtxApiContext db = new MtxApiContext();

            //Cria o objeto empresa pelo seu cnpj
            Empresa empresa = db.Empresas.FirstOrDefault(x => x.cnpj.Equals(cnpjFormatado));

            //se for nula, não existe
            if (empresa == null)
            {
                return BadRequest("EMPRESA NÃO FOI LOCALIZADA PELO CNPJ");
            }

            /*VERIFICAÇÕES NOS DADOS DO JSON ENVIADO */
            foreach (ItemTributacaoJson item in itens)
            {
                //Cnpj incorreto: veio nullo
                if (item.CNPJ_EMPRESA == null)
                {
                    return BadRequest("ITEM DO JSON SEM CNPJ DE EMPRESA!");
                }
                else //caso nao seja nulo
                {

                    if (item.CNPJ_EMPRESA != cnpjFormatado) //verifica se é diferente ao formatado
                    {
                        if (item.CNPJ_EMPRESA != cnpj) //se for ele ainda verifica se é diferente do cnpj original
                        {
                            //se ambos estiverem diferentes retorna o erro
                            return BadRequest("CNPJ DE ITEM NO JSON DIFERE DO CNPJ INFORMADO COMO PARAMETRO!");
                        }

                    }

                }



            } //fim foreach

            //contador auxiliar
            int cont = 0;

            //verificar o numero de intes, se forem nullo os itens do json vieram vazios
            if (itens == null)
            {
                _log.Debug("LOGGER DE JSON VAZIO OU CAMPO INVÁLIDO");
                return BadRequest("JSON VAZIO OU CAMPO INVÁLIDO!");
            }

            //SALVAR JSON PASTA UPLOADS - caso seja necessário recuperar esse arquivo json
            try
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(List<ItemTributacaoJson>));
                MemoryStream ms = new MemoryStream();
                ser.WriteObject(ms, itens);
                string jsonString = Encoding.UTF8.GetString(ms.ToArray());
                ms.Close();
                string path = System.Web.HttpContext.Current.Server.MapPath("~/Content/Uploads/objetomtx.json");
                System.IO.File.WriteAllText(path, jsonString);
            }
            catch
            {
                throw;
            }

            //lista com o objeto para retorno
            List<TributacaoEmpresa> listaSalvosTribEmpresa = new List<TributacaoEmpresa>();

            //laço para percorrer o objeto recebido e salvo no json
            foreach (ItemTributacaoJson item in itens)
            {
                //se o item retornado no campo codbarras for diferente de nullo ele entra
                if (item.PRODUTO_COD_BARRAS != null)
                {
                    //instanciando objeto para salvar os dados recebidos no json
                    TributacaoEmpresa itemSalvar = new TributacaoEmpresa();

                    //verificar se o produto ja foi importado
                    var tribEmpresas2 = from s in db.TributacaoEmpresas select s; //select na tabela

                   


                    /*Implementar busca pela categoria e verificar se a categoria que vem do cliente
                     existe na tabela de categoria da matriz*/
                    //pegou o ID da categoria
                    var categoriaProd = (from ab in db.CategoriasProdutos where item.PRODUTO_CATEGORIA == ab.descricao select ab.id).FirstOrDefault();

                    //Se houver a categoria ele atribui ao item e continua, caso não tenha ele atribui nullo e continua
                    /*Isso se deve ao fato que o cliente pode haver mais categorias e/ou categorias diferentes
                     o que não é relevante para analise, por isso atribuimos nulla caso seja diferente ou inexistente
                    na tabela da matriz*/
                    if (categoriaProd > 0)
                    {
                        item.PRODUTO_CATEGORIA = categoriaProd.ToString();
                    }
                    else
                    {
                        item.PRODUTO_CATEGORIA = null;
                    }

                    //where: where com o codigo de barras do produto e cnpj
                    /*aqui ele verifica se o produto ja contem no cnpj informado*/
                    tribEmpresas2 = tribEmpresas2.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado));

                    //tribEmpresas3 = tribEmpresas3.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado));

                   
                    //contar os que vieram com codigo de barras 0
                    if(item.PRODUTO_COD_BARRAS == "0")
                    {
                        prodZerado++;
                    }
                    
                    //se o codigo de barras não foi importado o entra na condição, ou seja o retorno do tribempresas2 é 0
                    //sendo zero o produto nao foi importado, agora ele será com todos os seus dados
                    //alteração 16092021->alem de nao ter encontrado nada no banco, count=0 o codigo de barras deve ser diferente de 0(zero)
                    if (tribEmpresas2.Count() <= 0 && item.PRODUTO_COD_BARRAS != "0")
                    {
                        //atribunido dados ao objeto
                        itemSalvar.CNPJ_EMPRESA = empresa.cnpj;
                        itemSalvar.PRODUTO_COD_BARRAS = item.PRODUTO_COD_BARRAS;
                        itemSalvar.PRODUTO_DESCRICAO = item.PRODUTO_DESCRICAO;
                        itemSalvar.PRODUTO_CEST = item.PRODUTO_CEST;
                        itemSalvar.PRODUTO_NCM = item.PRODUTO_NCM;
                        itemSalvar.PRODUTO_CATEGORIA = item.PRODUTO_CATEGORIA;/*Ponto a analisar, pois vem do cliente descrição*/
                        itemSalvar.FECP = item.FECP;
                        itemSalvar.COD_NAT_RECEITA = item.COD_NAT_RECEITA;
                        itemSalvar.CST_ENTRADA_PIS_COFINS = item.CST_ENTRADA_PIS_COFINS;
                        itemSalvar.CST_SAIDA_PIS_COFINS = item.CST_SAIDA_PIS_COFINS;
                        itemSalvar.ALIQ_ENTRADA_PIS = item.ALIQ_ENTRADA_PIS;
                        itemSalvar.ALIQ_SAIDA_PIS = item.ALIQ_ENTRADA_PIS;
                        itemSalvar.ALIQ_ENTRADA_COFINS = item.ALIQ_ENTRADA_COFINS;
                        itemSalvar.ALIQ_SAIDA_COFINS = item.ALIQ_SAIDA_COFINS;
                        itemSalvar.CST_VENDA_ATA = item.CST_VENDA_ATA;
                        itemSalvar.ALIQ_ICMS_VENDA_ATA = item.ALIQ_ICMS_VENDA_ATA;
                        itemSalvar.ALIQ_ICMS_ST_VENDA_ATA = item.ALIQ_ICMS_ST_VENDA_ATA;
                        itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA = item.RED_BASE_CALC_ICMS_VENDA_ATA;
                        itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA = item.RED_BASE_CALC_ICMS_ST_VENDA_ATA;
                        itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL = item.CST_VENDA_ATA_SIMP_NACIONAL;
                        itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
                        itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
                        itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
                        itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
                        itemSalvar.CST_VENDA_VAREJO_CONT = item.CST_VENDA_VAREJO_CONT;
                        itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT = item.ALIQ_ICMS_VENDA_VAREJO_CONT;
                        itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONT = item.ALIQ_ICMS_ST_VENDA_VAREJO_CONT;
                        itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT = item.RED_BASE_CALC_VENDA_VAREJO_CONT;
                        itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;
                        itemSalvar.CST_VENDA_VAREJO_CONS_FINAL = item.CST_VENDA_VAREJO_CONS_FINAL;
                        itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
                        itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
                        itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
                        itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
                        itemSalvar.CST_COMPRA_DE_IND = item.CST_COMPRA_DE_IND;
                        itemSalvar.ALIQ_ICMS_COMP_DE_IND = item.ALIQ_ICMS_COMP_DE_IND;
                        itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND = item.ALIQ_ICMS_ST_COMP_DE_IND;
                        itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND = item.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
                        itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;
                        itemSalvar.CST_COMPRA_DE_ATA = item.CST_COMPRA_DE_ATA;
                        itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA = item.ALIQ_ICMS_COMPRA_DE_ATA;
                        itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA = item.ALIQ_ICMS_ST_COMPRA_DE_ATA;
                        itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
                        itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;
                        itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL = item.CST_COMPRA_DE_SIMP_NACIONAL;
                        itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
                        itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
                        itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
                        itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
                        itemSalvar.CST_DA_NFE_DA_IND_FORN = item.CST_DA_NFE_DA_IND_FORN;
                        itemSalvar.CST_DA_NFE_DE_ATA_FORN = item.CST_DA_NFE_DE_ATA_FORN;
                        itemSalvar.CSOSNT_DANFE_DOS_NFOR = item.CSOSNT_DANFE_DOS_NFOR;
                        itemSalvar.ALIQ_ICMS_NFE = item.ALIQ_ICMS_NFE;
                        itemSalvar.ALIQ_ICMS_NFE_FOR_ATA = item.ALIQ_ICMS_NFE_FOR_ATA;
                        itemSalvar.ALIQ_ICMS_NFE_FOR_SN = item.ALIQ_ICMS_NFE_FOR_SN;
                        itemSalvar.TIPO_MVA = item.TIPO_MVA;
                        itemSalvar.VALOR_MVA_IND = item.VALOR_MVA_IND;
                        itemSalvar.INICIO_VIGENCIA_MVA = item.INICIO_VIGENCIA_MVA; //data
                        itemSalvar.FIM_VIGENCIA_MVA = item.FIM_VIGENCIA_MVA; //data
                        itemSalvar.CREDITO_OUTORGADO = item.CREDITO_OUTORGADO;
                        itemSalvar.VALOR_MVA_ATACADO = item.VALOR_MVA_ATACADO;
                        itemSalvar.REGIME_2560 = item.REGIME_2560;
                        itemSalvar.ESTADO = item.ESTADO;

                        //data da inclusão/alteração
                        itemSalvar.DT_ALTERACAO = DateTime.Now;


                        //try catch para salvar no banco e na lista de retorno
                        try
                        {

                            db.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco
                            listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
                            cont++;
                        }
                        catch (Exception e)
                        {
                            //erros e mensagens
                            if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
                            {
                               
                                _log.Error(e.InnerException.InnerException.Message);
                                return BadRequest("ERRO AO SALVAR ITEM: " + e.InnerException.InnerException.Message);
                            }

                            if (e.Message != null)
                            {
                               
                                _log.Error("ERRO AO SALVAR itemRec " + e.Message);
                                return BadRequest("ERRO AO SALVAR ITEM: " + e.Message);
                            }

                            return BadRequest("ERRO AO SALVAR ITEM");
                        }//fim do catch


                    }//fim do if que verifica se o codigo de barras ja foi importado

                } //fim do if do codigo barras nulo
                else
                {
                    aux++; //soma um a cada vez que um item não possuir codigo de barras
                }//fim do else

            }//fim do foreach dos itens

            //se o contador de itens salvos vier zero, retorno
            if (cont <= 0)
            {
                return BadRequest("NENHUM PRODUTO IMPORTADO, TODOS INFORMADOS JÁ ESTÃO NO BANCO OU TODOS ESTÃO COM COD_BARRAS = 0(zero) - Cod. Barras igual a 0(zero): "+prodZerado.ToString());
            }
            else
            {
                db.SaveChanges(); //salva caso o contador seja maior que zero
            }
            _log.Debug("FINAL DE PROCESSO COM " + cont + " ITENS SALVOS");
            return Ok("Itens informados no JSON: " + itens.Count() + " - Itens salvos: " + cont + " - Itens sem código de barras : " + aux+ " - Item com COD_BARRAS = 0(zero) : "+ prodZerado.ToString());

        } //fim da action


        //Excluir dados de importação pelo cnpj
        // POST: api/ItemTributacaoDelete/123
        [Route("api/ItemTributacaoDelete/{cnpj}")]
        public IHttpActionResult DeleteDeletaItemTributacao(string cnpj)
        {
            if (cnpj == null)
            {
                return BadRequest("FAVOR INFORMAR O CNPJ NO PARÂMETRO");
            }
            //formatando a string
            string cnpjFormatado = FormataCnpj.FormatarCNPJ(cnpj);

            //verificar a qtd de digitos
            if (cnpj.Length != 14)
            {
                return BadRequest("CNPJ PASSADO COMO PARÂMETRO ESTÁ INCORRETO");
            }
            //Instancia do contexto do banco
            MtxApiContext db = new MtxApiContext();

            //Cria o objeto empresa pelo seu cnpj
            TributacaoEmpresa empresa = db.TributacaoEmpresas.FirstOrDefault(x => x.CNPJ_EMPRESA.Equals(cnpjFormatado));

            //se for nula, não existe
            if (empresa == null)
            {
                return BadRequest("NÃO HÁ DADOS IMPORTADOS PARA O CNPJ INFORMADO");
            }


            var cont = db.Database.ExecuteSqlCommand("DELETE from tributacao_empresa where CNPJ_EMPRESA= '" + cnpjFormatado + "'");
            return Ok(cont + " Iten(s) deletado(s) da tabela para o CNPJ : " + cnpjFormatado);
        }

        //Mostrar analise de tributação 

        [Route("api/TributacaoEmpresaAnalise/{cnpj}")]
        public HttpResponseMessage GetTributacaoEmpresaAnalise(string cnpj)
        {
            var response = Request.CreateResponse(HttpStatusCode.Moved);
            //response.Headers.Location = new Uri("https://localhost:44320/HomeApi/EmpresaTributacao?cnpj="+cnpj);
            response.Headers.Location = new Uri("http://precisotax-001-site3.itempurl.com/HomeApi/EmpresaTributacao?cnpj=" + cnpj);
            return response;
        }


           
    }
}
