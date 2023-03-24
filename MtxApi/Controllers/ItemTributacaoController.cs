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
using System.Web;
using System.Web.Http;
//ALTERAÇÃO 24032023
namespace MtxApi.Controllers
{
    public class ItemTributacaoController : ApiController
    {
        //log
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //Instancia do contexto do banco
        MtxApiContext db = new MtxApiContext();
        MtxClienteContext bd = new MtxClienteContext();

        //Enviar dados para o banco de tributação das empresas pelo cnpj
        //Os dados vao para lista ItemTributaçãoJson que contem todos os campos
        // POST: api/ItemTributacao/123


        //nova acation com novos dados do cliente atualizado em 03/2023
        //Teste Git
        [Route("api/ItemTributacao/{cnpj}")]
        public IHttpActionResult PostListaTributacaoMTX(string cnpj, List<ItemTributacaoJson> itens)
        {
            //VAriavel auxiliar para receber o codigo de barras gerado pelo sistema (cód auxiliar na identificacao do item)
            string codBarrasGerado = "";

            //pegar o header que é a chave
            var chaveToken = HttpContext.Current.Request.Headers["chave"];

            //verificar se veio nula
            if (chaveToken == null)
            {
                return BadRequest("AUTENTICAÇÃO INCORRETA. CHAVE NECESSÁRIA");
            }


            //contador de intens que não possuem codigo de barras
            int aux = 0;
            int prodZerado = 0;

            //CONTADOR DE ITENS SEM ESTADO ORIGEM E DESTINO
            int auxEstado = 0;
            int codBarrasTamanho = 0;

            //verificar a qtd de digitos do cnpj
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
            string cnpjFormatado = FormataCnpj.FormatarCNPJ(cnpj); //puro
            string cnpjItemFormatado = ""; //que veio do json

            ////Instancia do contexto do banco
            //MtxApiContext db = new MtxApiContext();

            //Cria o objeto empresa pelo seu cnpj: que foi passado pelo post da api
            Empresa empresa = db.Empresas.FirstOrDefault(x => x.cnpj.Equals(cnpjFormatado));

            //se for nula, não existe
            if (empresa == null)
            {
                return BadRequest("EMPRESA NÃO FOI LOCALIZADA PELO CNPJ");
            }
            //se a chave nao corresponde
            if (!empresa.SoftwareHouse.Chave.Equals(chaveToken.ToString()))
            {
                return BadRequest("Chave incorreta para o CNPJ: " + cnpjFormatado + ": CORRESPONDENCIA INVÁLIDA");
            }



            //contador auxiliar
            int cont = 0;
            int contAlterados = 0;
            int contProdSalvos = 0;
            int contRegistrosNulos = 0;
            int contRegistrosCNPJInválido = 0;

            //verificar o numero de intes, se forem nullo os itens do json vieram vazios
            if (itens == null)
            {
                _log.Debug("LOGGER DE JSON VAZIO OU CAMPO INVÁLIDO");
                return BadRequest("JSON VAZIO OU CAMPO INVÁLIDO!");
            }

            //lista com o objeto para retorno
            List<TributacaoEmpresa> listaSalvosTribEmpresa = new List<TributacaoEmpresa>();


            /*VERIFICAÇÕES NOS DADOS DO JSON ENVIADO */
            foreach (ItemTributacaoJson item in itens)
            {
                //Cnpj incorreto: veio nullo
                if (item.CNPJ_EMPRESA == null || item.CNPJ_EMPRESA == "")
                {
                    //return BadRequest("ITEM DO JSON SEM CNPJ DE EMPRESA!");
                    contRegistrosCNPJInválido++;
                }
                else //caso nao seja nulo
                {
                    cnpjItemFormatado = FormataCnpj.FormatarCNPJ(item.CNPJ_EMPRESA);


                    //verifica se o cnpj passado no ITEM é diferente do cnpj passado no parametro da requisição
                    if (cnpjItemFormatado != cnpjFormatado) 
                    {
                        //return BadRequest("ITEM DO JSON SEM CNPJ DE EMPRESA!");
                        contRegistrosCNPJInválido++;

                    }
                    else //CASO O CNPJ DO ITEM ESTEJA CORRETO E IGUAL AO PASSADO PELO PARAMETRO, ELE CONTINUA O PROCESSO
                    {
                        //VARIAVEIS AUXILIARES
                        codBarrasGerado = "";
                        contRegistrosNulos = 0;

                        //CONDICIONAL PARA VERIFICAR SE O ESTADO ORIGEM E DESTINO VEIO VAZIO
                        if (item.UF_ORIGEM == null || item.UF_ORIGEM == "" || item.UF_DESTINO == null || item.UF_DESTINO == "")
                        {
                            auxEstado++; //CASO ESTEJA NULO ELE SOME MAIS UM NA VARIAVEL DE APOIO, E SAI DO LAÇO CONDICIONAL PASSANDO PARA O PROXIMO ITEM DO ARQUIVO JSON
                        }
                        else //SE NAO TIVER NULO OU VAZIO ELE CONTINUA O PROCESSO DESSE ITEM
                        {
                            //CASO O ITEM NAO VENHA COM CODIGO DE BARRAS NULO, OU COM VALOR 0 ELE ENTRA E CONTINUA O PROCESSO
                            if (item.PRODUTO_COD_BARRAS != null && item.PRODUTO_COD_BARRAS != "0")
                            {
                                //Vefificar o tamanho da string e retirando os espaços de inicio e fim 
                                item.PRODUTO_COD_BARRAS = item.PRODUTO_COD_BARRAS.Trim();

                                //Pegar o tamanho do codigo de barras enviado pelo json
                                int tamanho = item.PRODUTO_COD_BARRAS.Length;

                                //PEGA OS DESTINO
                                string[] ufDestinoIni = item.UF_DESTINO.Split('|');

                                /*VAI PASSAR POR TODOS OS DESTINOS VERIFICANDO SE EXISTE O PRODUTO LANÇADO NA TRIBUTAÇÃO NO CLIENTE*/

                                //retira o elemento vazio do array
                                ufDestinoIni = ufDestinoIni.Where(a => a != "").ToArray();

                                //verifivar se o item está na tabela de cliente
                                //PERCORRER TODOS OS DESTINOS
                                for (int i = 0; i < ufDestinoIni.Count(); i++)
                                {
                                    string dest = ufDestinoIni[i].ToString();


                                    /*Caso ele esteja na tabela de cliente, precisamos pegar o cod_de_barras_gerado pelo mtx pra prosseguir*/
                                    TributacaoEmpresa tribEmpresas3 = db.TributacaoEmpresas.Where(x => x.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && x.CNPJ_EMPRESA.Contains(cnpjFormatado) && x.UF_ORIGEM.Equals(item.UF_ORIGEM) && x.UF_DESTINO.Equals(dest)).FirstOrDefault(); //verifica o cadastro

                                    //se for tribEmpresas3 diferente de nula, quer dizer que ha tributacação para esse destino, É PRECISO ENTAO ANALISAR O ITEM                                                                                                                                                                                                                                                        //se for diferente de nula, quer dizer que ha tributacação para esse destino
                                    if (tribEmpresas3 != null)
                                    {
                                        //PEGAR O VALOR DO COD_BARRAS_GERADO CASO SEJA DIFERENTE DE NULO, PARA TODOS OS ITENS QUE FOREM ANALISADOS PELO FOR
                                        if (tribEmpresas3.COD_BARRAS_GERADO != null)
                                        {
                                            codBarrasGerado = tribEmpresas3.COD_BARRAS_GERADO; //a variavel codigo de barras gerado vai receber esse valor do objejeto
                                        }
                                        else
                                        {
                                            /*Se ele for nulo,(o codigo de barras gerado do objeto) 
                                            * ele tem que verificar se o tamanho do cod barras é maior que 7, se for ele so
                                            * atribui ao codigo de barras gerado, se nao ele gera um novo, salva na tabela do cliente 
                                            * e passa esse codigo gerado para frente para   que o cadastro do produto tenha o mesmo codigo,
                                            * igualando as referencias*/

                                            if (tribEmpresas3.PRODUTO_COD_BARRAS.Count() > 7)
                                            {
                                                codBarrasGerado = tribEmpresas3.PRODUTO_COD_BARRAS.ToString();
                                                TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
                                                /*VERIFICAR TODOS OS DESTINO*/

                                                itemSalvar = db.TributacaoEmpresas.Find(tribEmpresas3.ID);
                                                itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;

                                                db.SaveChanges(); //salva com o mesmo numero do codigo de barras, pois ele eh maior que 7
                                            }
                                            else
                                            {
                                                if (codBarrasGerado != "") //VERIFICA SE É DIFERENTE DE VAZIO, SE FOR DIFERENTE DE VAZIO, QUER DIZER QUE TEM ALGO E ATRIBUI
                                                {
                                                    TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
                                                    /*VERIFICAR TODOS OS DESTINO*/

                                                    itemSalvar = db.TributacaoEmpresas.Find(tribEmpresas3.ID);
                                                    itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;

                                                    db.SaveChanges();

                                                }//SE FOR VAZIO GERA-SE UM CODIGO RANDOMICO E ATRUIBUI
                                                else
                                                {
                                                    Random randNum = new Random();

                                                    for (int ib = 0; ib < 1; ib++)
                                                    {
                                                        codBarrasGerado = (randNum.Next().ToString());
                                                    }
                                                    TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
                                                    /*VERIFICAR TODOS OS DESTINO*/

                                                    itemSalvar = db.TributacaoEmpresas.Find(tribEmpresas3.ID);
                                                    itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;

                                                    db.SaveChanges();
                                                }

                                            }//FIM DO ELSE DA VERIFICACAO DO CODIGO BARRAS GERADO
                                        }
                                    }
                                    else
                                    {
                                        //se nesse destino vier nula, efetuar um contador para verificar se o codBArrasGerado foi atribuido algum valor
                                        contRegistrosNulos++;
                                    }




                                }//FIM DO FOR QUE PERCORRE O ARRAY DE DESTINO


                                //apos o for, verificar o contador de registros nulos, se não continuar 0 quer dizer que  foi encontardo registro na
                                //tabela do cliente, ou seja, NÃO devemos gerar um codigo de barras para atribuir ao valor do campo codigo de barras gerado
                                if (contRegistrosNulos != 0)
                                {
                                    if (item.PRODUTO_COD_BARRAS.Count() <= 7) //se for menor ou igual a sete, devemos gerar um codigo de barras
                                    {
                                        Random randNum = new Random();

                                        for (int ib = 0; ib < 1; ib++)
                                        {
                                            codBarrasGerado = (randNum.Next().ToString());
                                        }
                                    }
                                    else
                                    {
                                        codBarrasGerado = item.PRODUTO_COD_BARRAS.ToString();
                                    }
                                }

                                //condicional tamanho do codigo de barras
                                if (tamanho > 7)
                                {
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
                                    /*ROTINA PARA VERIFICAR SE O PRODUTO ESTÁ CADASTRADO E TRIBUTADO NA TABELA MATRIZ*/
                                    long? prodItem = long.Parse(item.PRODUTO_COD_BARRAS);
                                    /*TO-DO
                                    * Essa busca deve ser melhorada, so pelo codigo de barras não é suficiente, uma vez
                                    * que existem outros codigos de barras iguais cadastrados anteriormente*/

                                    Produto cadProd = db.Produtos.Where(x => x.codBarras == prodItem && x.CodBarrasGErado.Equals(codBarrasGerado.ToString())).FirstOrDefault(); //verifica o cadastro

                                    Produto prodSalvar = new Produto();

                                    //se ele nao esta cadastrado na tabela de produto ele deve ser cadastrado nesta tabela
                                    if (cadProd == null)
                                    {

                                        prodSalvar.codBarras = Int64.Parse(item.PRODUTO_COD_BARRAS);
                                        prodSalvar.CodBarrasGErado = codBarrasGerado;
                                        prodSalvar.descricao = item.PRODUTO_DESCRICAO;
                                        prodSalvar.cest = item.PRODUTO_CEST;
                                        prodSalvar.ncm = item.PRODUTO_NCM;

                                        if (item.PRODUTO_CATEGORIA != null)
                                        {
                                            prodSalvar.idCategoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

                                        }
                                        else
                                        {
                                            prodSalvar.idCategoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
                                        }

                                        prodSalvar.status = 1;
                                        prodSalvar.dataCad = DateTime.Now;
                                        prodSalvar.dataAlt = DateTime.Now;
                                        prodSalvar.auditadoNCM = 0; //nao auditado

                                        //try-catch para salvar o produto na tabela
                                        try
                                        {

                                            db.Produtos.Add(prodSalvar);//objeto para ser salvo no banco
                                            db.SaveChanges();

                                            contProdSalvos++;
                                        }
                                        catch (Exception e)
                                        {
                                            //erros e mensagens
                                            if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
                                            {

                                                _log.Error(e.InnerException.InnerException.Message);
                                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
                                            }

                                            if (e.Message != null)
                                            {

                                                _log.Error("ERRO AO SALVAR itemRec " + e.Message);
                                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
                                            }

                                            return BadRequest("ERRO AO SALVAR PRODUTO");
                                        }//fim do catch



                                    } //fim cad produto

                                    //VERIFICAR SE HA TRIBUTAÇÃO PARA O PRODUTO DEPENDENDO DA EMPRESA (SIMPLES OU NORMAL)
                                    if (cadProd == null)
                                    {
                                        cadProd = db.Produtos.Where(x => x.codBarras == prodItem).FirstOrDefault();
                                    }
                                    /*Salvar na tabela TributacaoNCM, caso nao exista*/
                                    string prodItemNCM = item.PRODUTO_NCM; //PEGA O NCM DO ITEM
                                                                           //Array de destino: PEGA TODOS OS DESTINOS QUE VIERAM NO JSON DO CLIENTE
                                    string[] ufDestinoProd = item.UF_DESTINO.Split('|');

                                    //retira o elemento vazio do array
                                    ufDestinoProd = ufDestinoProd.Where(a => a != "").ToArray();

                                    //PEGA O CRT E O REGIME TRIBUTARIO DA EMPRESA
                                    int? crt = empresa.crt;
                                    int? regime_tributario = empresa.regime_trib;

                                    //PASSAR PELOS DESTINOS PARA PROCURAR OS ITENS NA TABELA DE NCM - se faz necessario pois cada tributacao tem sua origem e destino
                                    for (int i = 0; i < ufDestinoProd.Count(); i++)
                                    {
                                        string dest = ufDestinoProd[i].ToString();
                                        //BUSCA PELO NCM NA TABELA, PASSANDO O CRT E O REGIME
                                        TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest && x.CRT == crt && x.Regime_Trib == regime_tributario).FirstOrDefault();

                                        if (tribnaNCM == null) //SE FOR NULLO NAO HA TRIBUTACAO PARA ESSE ITEM NA TABELA
                                        {
                                            //MONTAR O MECANISMO PARA SALVAR O ITEM NA TABELA
                                            TributacaoNCM prodTribNCMSalvar = new TributacaoNCM();
                                            //VERIFICAR A CATEGORIA
                                            if (item.PRODUTO_CATEGORIA != null)
                                            {
                                                prodTribNCMSalvar.categoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

                                            }
                                            else
                                            {
                                                prodTribNCMSalvar.categoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
                                            }

                                            //ATRIBUIR OS OUTROS DADOS
                                            prodTribNCMSalvar.UF_Origem = item.UF_ORIGEM;
                                            prodTribNCMSalvar.UF_Destino = ufDestinoProd[i];
                                            prodTribNCMSalvar.cest = item.PRODUTO_CEST;
                                            prodTribNCMSalvar.ncm = item.PRODUTO_NCM;
                                            prodTribNCMSalvar.auditadoPorNCM = 0;
                                            prodTribNCMSalvar.CRT = crt;
                                            prodTribNCMSalvar.Regime_Trib = regime_tributario;
                                            prodTribNCMSalvar.dataCad = DateTime.Now;
                                            prodTribNCMSalvar.dataAlt = DateTime.Now;

                                            //TRY CATCH PARA SALVAR O ITEM NA TABELA
                                            try
                                            {
                                                //salvar
                                                db.TributacaoNCM.Add(prodTribNCMSalvar);//objeto para ser salvo no banco
                                                db.SaveChanges();

                                            }
                                            catch (Exception e)
                                            {
                                                //erros e mensagens
                                                if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
                                                {

                                                    _log.Error(e.InnerException.InnerException.Message);
                                                    return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
                                                }

                                                if (e.Message != null)
                                                {

                                                    _log.Error("ERRO AO SALVAR itemRec " + e.Message);
                                                    return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
                                                }

                                                return BadRequest("ERRO AO SALVAR PRODUTO");
                                            }//fim do catch


                                        }//FIM DA VERIFICACAO SE VEIO NULO A CONSULTA


                                    }//FIM DO FOR PARA BUSCAR NA TABELA DE TRIBUTACAO POR NCM
                                     //contar os que vieram com codigo de barras 0
                                    if (item.PRODUTO_COD_BARRAS == "0")
                                    {
                                        prodZerado++;
                                    }

                                    //Verificar em todos os destinos se o item foi tributado no cliente
                                    string[] ufDestino = item.UF_DESTINO.Split('|');
                                    //retira o elemento vazio do array deixando somente os id dos registros
                                    ufDestino = ufDestino.Where(a => a != "").ToArray();
                                    for (int i = 0; i < ufDestino.Count(); i++)
                                    {
                                        string dest = ufDestino[i].ToString();
                                        //where: where com o codigo de barras do produto e cnpj
                                        /*aqui ele verifica se o produto ja contem no cnpj informado*/
                                        tribEmpresas2 = tribEmpresas2.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest));

                                        //se vier algo da consulta acima: se vier 0 ELE VAI SALVAR O ITEM NA TABELA DO CLIENTE
                                        if (tribEmpresas2.Count() <= 0 && item.PRODUTO_COD_BARRAS != "0")
                                        {
                                            TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
                                            //atribunido dados ao objeto
                                            itemSalvar.CNPJ_EMPRESA = empresa.cnpj;
                                            itemSalvar.PRODUTO_COD_BARRAS = item.PRODUTO_COD_BARRAS;
                                            itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;
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
                                            itemSalvar.UF_ORIGEM = item.UF_ORIGEM;
                                            itemSalvar.UF_DESTINO = ufDestino[i];
                                            itemSalvar.PRODUTO_COD_INTERNO = item.PRODUTO_COD_INTERNO;
                                            //data da inclusão/alteração
                                            itemSalvar.DT_ALTERACAO = DateTime.Now;


                                            //Verifica se o item veio ativo, caso venha null considera ativo
                                            if (item.ATIVO == null)
                                            {
                                                itemSalvar.ATIVO = 1;
                                            }
                                            else
                                            {
                                                itemSalvar.ATIVO = sbyte.Parse(item.ATIVO);
                                            }



                                            //try catch para salvar no banco e na lista de retorno
                                            try
                                            {
                                                //salva os itens quando nao existem na tabela
                                                db.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco
                                                bd.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco de comparação
                                                listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
                                                db.SaveChanges();
                                                bd.SaveChanges();


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

                                        }
                                        else//SE O PRODUTO EXISTIR, ELE VAI SER ALTERADO NA ESTRUTURA ABAIXO
                                        {
                                            //se ele nao existir na tabela do cliente ele dever ser importado
                                            //se o codigo de barras não foi importado o entra na condição, ou seja o retorno do tribempresas2 é 0
                                            //sendo zero o produto nao foi importado, agora ele será com todos os seus dados
                                            //alteração 16092021->alem de nao ter encontrado nada no banco, count=0 o codigo de barras deve ser diferente de 0(zero)
                                            //pegar o id desse registro
                                            var idDoRegistros = db.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID).FirstOrDefault();
                                            var idDoRegistros2 = bd.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID).FirstOrDefault();

                                            if (idDoRegistros != 0)
                                            {
                                                TributacaoEmpresa itemSalvar = new TributacaoEmpresa();

                                                itemSalvar = db.TributacaoEmpresas.Find(idDoRegistros);

                                                itemSalvar.PRODUTO_DESCRICAO = (itemSalvar.PRODUTO_DESCRICAO != item.PRODUTO_DESCRICAO) ? ((item.PRODUTO_DESCRICAO != null) ? item.PRODUTO_DESCRICAO : itemSalvar.PRODUTO_DESCRICAO) : itemSalvar.PRODUTO_DESCRICAO;
                                                itemSalvar.PRODUTO_CEST = (itemSalvar.PRODUTO_CEST != item.PRODUTO_CEST) ? ((item.PRODUTO_CEST != null) ? item.PRODUTO_CEST : itemSalvar.PRODUTO_CEST) : itemSalvar.PRODUTO_CEST;
                                                itemSalvar.PRODUTO_NCM = (itemSalvar.PRODUTO_NCM != item.PRODUTO_NCM) ? ((item.PRODUTO_NCM != null) ? item.PRODUTO_NCM : itemSalvar.PRODUTO_NCM) : itemSalvar.PRODUTO_NCM;
                                                itemSalvar.PRODUTO_CATEGORIA = (itemSalvar.PRODUTO_CATEGORIA != item.PRODUTO_CATEGORIA) ? ((item.PRODUTO_CATEGORIA != null) ? item.PRODUTO_CATEGORIA : itemSalvar.PRODUTO_CATEGORIA) : itemSalvar.PRODUTO_CATEGORIA;
                                                itemSalvar.FECP = (itemSalvar.FECP != item.FECP) ? ((item.FECP != null) ? item.FECP : itemSalvar.FECP) : itemSalvar.FECP;
                                                itemSalvar.COD_NAT_RECEITA = (itemSalvar.COD_NAT_RECEITA != item.COD_NAT_RECEITA) ? ((item.COD_NAT_RECEITA != null) ? item.COD_NAT_RECEITA : itemSalvar.COD_NAT_RECEITA) : itemSalvar.COD_NAT_RECEITA;

                                                itemSalvar.CST_ENTRADA_PIS_COFINS = (itemSalvar.CST_ENTRADA_PIS_COFINS != item.CST_ENTRADA_PIS_COFINS) ? ((item.CST_ENTRADA_PIS_COFINS != null) ? item.CST_ENTRADA_PIS_COFINS : itemSalvar.CST_ENTRADA_PIS_COFINS) : itemSalvar.CST_ENTRADA_PIS_COFINS;
                                                itemSalvar.CST_SAIDA_PIS_COFINS = (itemSalvar.CST_SAIDA_PIS_COFINS != item.CST_SAIDA_PIS_COFINS) ? ((item.CST_SAIDA_PIS_COFINS != null) ? item.CST_SAIDA_PIS_COFINS : itemSalvar.CST_SAIDA_PIS_COFINS) : itemSalvar.CST_SAIDA_PIS_COFINS;
                                                itemSalvar.ALIQ_ENTRADA_PIS = (itemSalvar.ALIQ_ENTRADA_PIS != item.ALIQ_ENTRADA_PIS) ? ((item.ALIQ_ENTRADA_PIS != null) ? item.ALIQ_ENTRADA_PIS : itemSalvar.ALIQ_ENTRADA_PIS) : itemSalvar.ALIQ_ENTRADA_PIS;
                                                itemSalvar.ALIQ_SAIDA_PIS = (itemSalvar.ALIQ_SAIDA_PIS != item.ALIQ_SAIDA_PIS) ? ((item.ALIQ_SAIDA_PIS != null) ? item.ALIQ_SAIDA_PIS : itemSalvar.ALIQ_SAIDA_PIS) : itemSalvar.ALIQ_SAIDA_PIS;
                                                itemSalvar.ALIQ_ENTRADA_COFINS = (itemSalvar.ALIQ_ENTRADA_COFINS != item.ALIQ_ENTRADA_COFINS) ? ((item.ALIQ_ENTRADA_COFINS != null) ? item.ALIQ_ENTRADA_COFINS : itemSalvar.ALIQ_ENTRADA_COFINS) : itemSalvar.ALIQ_ENTRADA_COFINS;
                                                itemSalvar.ALIQ_SAIDA_COFINS = (itemSalvar.ALIQ_SAIDA_COFINS != item.ALIQ_SAIDA_COFINS) ? ((item.ALIQ_SAIDA_COFINS != null) ? item.ALIQ_SAIDA_COFINS : itemSalvar.ALIQ_SAIDA_COFINS) : itemSalvar.ALIQ_SAIDA_COFINS;

                                                itemSalvar.CST_VENDA_ATA = (itemSalvar.CST_VENDA_ATA != item.CST_VENDA_ATA) ? ((item.CST_VENDA_ATA != null) ? item.CST_VENDA_ATA : itemSalvar.CST_VENDA_ATA) : itemSalvar.CST_VENDA_ATA;
                                                itemSalvar.ALIQ_ICMS_VENDA_ATA = (itemSalvar.ALIQ_ICMS_VENDA_ATA != item.ALIQ_ICMS_VENDA_ATA) ? ((item.ALIQ_ICMS_VENDA_ATA != null) ? item.ALIQ_ICMS_VENDA_ATA : itemSalvar.ALIQ_ICMS_VENDA_ATA) : itemSalvar.ALIQ_ICMS_VENDA_ATA;
                                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA != item.ALIQ_ICMS_ST_VENDA_ATA) ? ((item.ALIQ_ICMS_ST_VENDA_ATA != null) ? item.ALIQ_ICMS_ST_VENDA_ATA : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA != item.RED_BASE_CALC_ICMS_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA;

                                                itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL != item.CST_VENDA_ATA_SIMP_NACIONAL) ? ((item.CST_VENDA_ATA_SIMP_NACIONAL != null) ? item.CST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;


                                                itemSalvar.CST_VENDA_VAREJO_CONT = (itemSalvar.CST_VENDA_VAREJO_CONT != item.CST_VENDA_VAREJO_CONT) ? ((item.CST_VENDA_VAREJO_CONT != null) ? item.CST_VENDA_VAREJO_CONT : itemSalvar.CST_VENDA_VAREJO_CONT) : itemSalvar.CST_VENDA_VAREJO_CONT;
                                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT != item.ALIQ_ICMS_VENDA_VAREJO_CONT) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONT != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONT : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT;
                                                itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT != item.RED_BASE_CALC_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT;
                                                itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;


                                                itemSalvar.CST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.CST_VENDA_VAREJO_CONS_FINAL != item.CST_VENDA_VAREJO_CONS_FINAL) ? ((item.CST_VENDA_VAREJO_CONS_FINAL != null) ? item.CST_VENDA_VAREJO_CONS_FINAL : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;


                                                itemSalvar.CST_COMPRA_DE_IND = (itemSalvar.CST_COMPRA_DE_IND != item.CST_COMPRA_DE_IND) ? ((item.CST_COMPRA_DE_IND != null) ? item.CST_COMPRA_DE_IND : itemSalvar.CST_COMPRA_DE_IND) : itemSalvar.CST_COMPRA_DE_IND;
                                                itemSalvar.ALIQ_ICMS_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_COMP_DE_IND != item.ALIQ_ICMS_COMP_DE_IND) ? ((item.ALIQ_ICMS_COMP_DE_IND != null) ? item.ALIQ_ICMS_COMP_DE_IND : itemSalvar.ALIQ_ICMS_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_COMP_DE_IND;
                                                itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND != item.ALIQ_ICMS_ST_COMP_DE_IND) ? ((item.ALIQ_ICMS_ST_COMP_DE_IND != null) ? item.ALIQ_ICMS_ST_COMP_DE_IND : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND;
                                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;

                                                itemSalvar.CST_COMPRA_DE_ATA = (itemSalvar.CST_COMPRA_DE_ATA != item.CST_COMPRA_DE_ATA) ? ((item.CST_COMPRA_DE_ATA != null) ? item.CST_COMPRA_DE_ATA : itemSalvar.CST_COMPRA_DE_ATA) : itemSalvar.CST_COMPRA_DE_ATA;
                                                itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA != item.ALIQ_ICMS_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA;
                                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA != item.ALIQ_ICMS_ST_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;

                                                itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL != item.CST_COMPRA_DE_SIMP_NACIONAL) ? ((item.CST_COMPRA_DE_SIMP_NACIONAL != null) ? item.CST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;


                                                itemSalvar.CST_DA_NFE_DA_IND_FORN = (itemSalvar.CST_DA_NFE_DA_IND_FORN != item.CST_DA_NFE_DA_IND_FORN) ? ((item.CST_DA_NFE_DA_IND_FORN != null) ? item.CST_DA_NFE_DA_IND_FORN : itemSalvar.CST_DA_NFE_DA_IND_FORN) : itemSalvar.CST_DA_NFE_DA_IND_FORN;
                                                itemSalvar.CST_DA_NFE_DE_ATA_FORN = (itemSalvar.CST_DA_NFE_DE_ATA_FORN != item.CST_DA_NFE_DE_ATA_FORN) ? ((item.CST_DA_NFE_DE_ATA_FORN != null) ? item.CST_DA_NFE_DE_ATA_FORN : itemSalvar.CST_DA_NFE_DE_ATA_FORN) : itemSalvar.CST_DA_NFE_DE_ATA_FORN;
                                                itemSalvar.CSOSNT_DANFE_DOS_NFOR = (itemSalvar.CSOSNT_DANFE_DOS_NFOR != item.CSOSNT_DANFE_DOS_NFOR) ? ((item.CSOSNT_DANFE_DOS_NFOR != null) ? item.CSOSNT_DANFE_DOS_NFOR : itemSalvar.CSOSNT_DANFE_DOS_NFOR) : itemSalvar.CSOSNT_DANFE_DOS_NFOR;

                                                itemSalvar.ALIQ_ICMS_NFE = (itemSalvar.ALIQ_ICMS_NFE != item.ALIQ_ICMS_NFE) ? ((item.ALIQ_ICMS_NFE != null) ? item.ALIQ_ICMS_NFE : itemSalvar.ALIQ_ICMS_NFE) : itemSalvar.ALIQ_ICMS_NFE;
                                                itemSalvar.ALIQ_ICMS_NFE_FOR_ATA = (itemSalvar.ALIQ_ICMS_NFE_FOR_ATA != item.ALIQ_ICMS_NFE_FOR_ATA) ? ((item.ALIQ_ICMS_NFE_FOR_ATA != null) ? item.ALIQ_ICMS_NFE_FOR_ATA : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA) : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA;
                                                itemSalvar.ALIQ_ICMS_NFE_FOR_SN = (itemSalvar.ALIQ_ICMS_NFE_FOR_SN != item.ALIQ_ICMS_NFE_FOR_SN) ? ((item.ALIQ_ICMS_NFE_FOR_SN != null) ? item.ALIQ_ICMS_NFE_FOR_SN : itemSalvar.ALIQ_ICMS_NFE_FOR_SN) : itemSalvar.ALIQ_ICMS_NFE_FOR_SN;


                                                itemSalvar.TIPO_MVA = (itemSalvar.TIPO_MVA != item.TIPO_MVA) ? ((item.TIPO_MVA != null) ? item.TIPO_MVA : itemSalvar.TIPO_MVA) : itemSalvar.TIPO_MVA;

                                                itemSalvar.VALOR_MVA_IND = (itemSalvar.VALOR_MVA_IND != item.VALOR_MVA_IND) ? ((item.VALOR_MVA_IND != null) ? item.VALOR_MVA_IND : itemSalvar.VALOR_MVA_IND) : itemSalvar.VALOR_MVA_IND;

                                                itemSalvar.INICIO_VIGENCIA_MVA = (itemSalvar.INICIO_VIGENCIA_MVA != item.INICIO_VIGENCIA_MVA) ? ((item.INICIO_VIGENCIA_MVA != null) ? item.INICIO_VIGENCIA_MVA : itemSalvar.INICIO_VIGENCIA_MVA) : itemSalvar.INICIO_VIGENCIA_MVA;

                                                itemSalvar.FIM_VIGENCIA_MVA = (itemSalvar.FIM_VIGENCIA_MVA != item.FIM_VIGENCIA_MVA) ? ((item.FIM_VIGENCIA_MVA != null) ? item.FIM_VIGENCIA_MVA : itemSalvar.FIM_VIGENCIA_MVA) : itemSalvar.FIM_VIGENCIA_MVA;

                                                itemSalvar.CREDITO_OUTORGADO = (itemSalvar.CREDITO_OUTORGADO != item.CREDITO_OUTORGADO) ? ((item.CREDITO_OUTORGADO != null) ? item.CREDITO_OUTORGADO : itemSalvar.CREDITO_OUTORGADO) : itemSalvar.CREDITO_OUTORGADO;

                                                itemSalvar.VALOR_MVA_ATACADO = (itemSalvar.VALOR_MVA_ATACADO != item.VALOR_MVA_ATACADO) ? ((item.VALOR_MVA_ATACADO != null) ? item.VALOR_MVA_ATACADO : itemSalvar.VALOR_MVA_ATACADO) : itemSalvar.VALOR_MVA_ATACADO;

                                                itemSalvar.REGIME_2560 = (itemSalvar.REGIME_2560 != item.REGIME_2560) ? ((item.REGIME_2560 != null) ? item.REGIME_2560 : itemSalvar.REGIME_2560) : itemSalvar.REGIME_2560;

                                                itemSalvar.UF_ORIGEM = (itemSalvar.UF_ORIGEM != item.UF_ORIGEM) ? ((item.UF_ORIGEM != null) ? item.UF_ORIGEM : itemSalvar.UF_ORIGEM) : itemSalvar.UF_ORIGEM;

                                                itemSalvar.UF_DESTINO = (itemSalvar.UF_DESTINO != ufDestino[i]) ? ((item.UF_DESTINO != null) ? item.UF_DESTINO : ufDestino[i]) : itemSalvar.UF_DESTINO;

                                                //data da inclusão/alteração
                                                itemSalvar.DT_ALTERACAO = DateTime.Now;


                                                //segundo banco: SALVAR ALTERAÇÕES NO BANCO DE BKP OU TABELAS INICIAIS DO CLIENTE
                                                TributacaoEmpresa itemSalvar2 = new TributacaoEmpresa();
                                                itemSalvar2 = bd.TributacaoEmpresas.Find(idDoRegistros2);

                                                itemSalvar2.PRODUTO_DESCRICAO = (itemSalvar2.PRODUTO_DESCRICAO != item.PRODUTO_DESCRICAO) ? ((item.PRODUTO_DESCRICAO != null) ? item.PRODUTO_DESCRICAO : itemSalvar2.PRODUTO_DESCRICAO) : itemSalvar2.PRODUTO_DESCRICAO;
                                                itemSalvar2.PRODUTO_CEST = (itemSalvar2.PRODUTO_CEST != item.PRODUTO_CEST) ? ((item.PRODUTO_CEST != null) ? item.PRODUTO_CEST : itemSalvar2.PRODUTO_CEST) : itemSalvar2.PRODUTO_CEST;
                                                itemSalvar2.PRODUTO_NCM = (itemSalvar2.PRODUTO_NCM != item.PRODUTO_NCM) ? ((item.PRODUTO_NCM != null) ? item.PRODUTO_NCM : itemSalvar2.PRODUTO_NCM) : itemSalvar2.PRODUTO_NCM;
                                                itemSalvar2.PRODUTO_CATEGORIA = (itemSalvar2.PRODUTO_CATEGORIA != item.PRODUTO_CATEGORIA) ? ((item.PRODUTO_CATEGORIA != null) ? item.PRODUTO_CATEGORIA : itemSalvar2.PRODUTO_CATEGORIA) : itemSalvar2.PRODUTO_CATEGORIA;
                                                itemSalvar2.FECP = (itemSalvar2.FECP != item.FECP) ? ((item.FECP != null) ? item.FECP : itemSalvar2.FECP) : itemSalvar2.FECP;
                                                itemSalvar2.COD_NAT_RECEITA = (itemSalvar2.COD_NAT_RECEITA != item.COD_NAT_RECEITA) ? ((item.COD_NAT_RECEITA != null) ? item.COD_NAT_RECEITA : itemSalvar2.COD_NAT_RECEITA) : itemSalvar2.COD_NAT_RECEITA;

                                                itemSalvar2.CST_ENTRADA_PIS_COFINS = (itemSalvar2.CST_ENTRADA_PIS_COFINS != item.CST_ENTRADA_PIS_COFINS) ? ((item.CST_ENTRADA_PIS_COFINS != null) ? item.CST_ENTRADA_PIS_COFINS : itemSalvar2.CST_ENTRADA_PIS_COFINS) : itemSalvar2.CST_ENTRADA_PIS_COFINS;
                                                itemSalvar2.CST_SAIDA_PIS_COFINS = (itemSalvar2.CST_SAIDA_PIS_COFINS != item.CST_SAIDA_PIS_COFINS) ? ((item.CST_SAIDA_PIS_COFINS != null) ? item.CST_SAIDA_PIS_COFINS : itemSalvar2.CST_SAIDA_PIS_COFINS) : itemSalvar2.CST_SAIDA_PIS_COFINS;
                                                itemSalvar2.ALIQ_ENTRADA_PIS = (itemSalvar2.ALIQ_ENTRADA_PIS != item.ALIQ_ENTRADA_PIS) ? ((item.ALIQ_ENTRADA_PIS != null) ? item.ALIQ_ENTRADA_PIS : itemSalvar2.ALIQ_ENTRADA_PIS) : itemSalvar2.ALIQ_ENTRADA_PIS;
                                                itemSalvar2.ALIQ_SAIDA_PIS = (itemSalvar2.ALIQ_SAIDA_PIS != item.ALIQ_SAIDA_PIS) ? ((item.ALIQ_SAIDA_PIS != null) ? item.ALIQ_SAIDA_PIS : itemSalvar2.ALIQ_SAIDA_PIS) : itemSalvar2.ALIQ_SAIDA_PIS;
                                                itemSalvar2.ALIQ_ENTRADA_COFINS = (itemSalvar2.ALIQ_ENTRADA_COFINS != item.ALIQ_ENTRADA_COFINS) ? ((item.ALIQ_ENTRADA_COFINS != null) ? item.ALIQ_ENTRADA_COFINS : itemSalvar2.ALIQ_ENTRADA_COFINS) : itemSalvar2.ALIQ_ENTRADA_COFINS;
                                                itemSalvar2.ALIQ_SAIDA_COFINS = (itemSalvar2.ALIQ_SAIDA_COFINS != item.ALIQ_SAIDA_COFINS) ? ((item.ALIQ_SAIDA_COFINS != null) ? item.ALIQ_SAIDA_COFINS : itemSalvar2.ALIQ_SAIDA_COFINS) : itemSalvar2.ALIQ_SAIDA_COFINS;

                                                itemSalvar2.CST_VENDA_ATA = (itemSalvar2.CST_VENDA_ATA != item.CST_VENDA_ATA) ? ((item.CST_VENDA_ATA != null) ? item.CST_VENDA_ATA : itemSalvar2.CST_VENDA_ATA) : itemSalvar2.CST_VENDA_ATA;
                                                itemSalvar2.ALIQ_ICMS_VENDA_ATA = (itemSalvar2.ALIQ_ICMS_VENDA_ATA != item.ALIQ_ICMS_VENDA_ATA) ? ((item.ALIQ_ICMS_VENDA_ATA != null) ? item.ALIQ_ICMS_VENDA_ATA : itemSalvar2.ALIQ_ICMS_VENDA_ATA) : itemSalvar2.ALIQ_ICMS_VENDA_ATA;
                                                itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA = (itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA != item.ALIQ_ICMS_ST_VENDA_ATA) ? ((item.ALIQ_ICMS_ST_VENDA_ATA != null) ? item.ALIQ_ICMS_ST_VENDA_ATA : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA) : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA != item.RED_BASE_CALC_ICMS_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA;

                                                itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL != item.CST_VENDA_ATA_SIMP_NACIONAL) ? ((item.CST_VENDA_ATA_SIMP_NACIONAL != null) ? item.CST_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;


                                                itemSalvar2.CST_VENDA_VAREJO_CONT = (itemSalvar2.CST_VENDA_VAREJO_CONT != item.CST_VENDA_VAREJO_CONT) ? ((item.CST_VENDA_VAREJO_CONT != null) ? item.CST_VENDA_VAREJO_CONT : itemSalvar2.CST_VENDA_VAREJO_CONT) : itemSalvar2.CST_VENDA_VAREJO_CONT;
                                                itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT = (itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT != item.ALIQ_ICMS_VENDA_VAREJO_CONT) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONT != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONT : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT) : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT;
                                                itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT = (itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT != item.RED_BASE_CALC_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_VENDA_VAREJO_CONT : itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT) : itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT;
                                                itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = (itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT : itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) : itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;


                                                itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL != item.CST_VENDA_VAREJO_CONS_FINAL) ? ((item.CST_VENDA_VAREJO_CONS_FINAL != null) ? item.CST_VENDA_VAREJO_CONS_FINAL : itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;


                                                itemSalvar2.CST_COMPRA_DE_IND = (itemSalvar2.CST_COMPRA_DE_IND != item.CST_COMPRA_DE_IND) ? ((item.CST_COMPRA_DE_IND != null) ? item.CST_COMPRA_DE_IND : itemSalvar2.CST_COMPRA_DE_IND) : itemSalvar2.CST_COMPRA_DE_IND;
                                                itemSalvar2.ALIQ_ICMS_COMP_DE_IND = (itemSalvar2.ALIQ_ICMS_COMP_DE_IND != item.ALIQ_ICMS_COMP_DE_IND) ? ((item.ALIQ_ICMS_COMP_DE_IND != null) ? item.ALIQ_ICMS_COMP_DE_IND : itemSalvar2.ALIQ_ICMS_COMP_DE_IND) : itemSalvar2.ALIQ_ICMS_COMP_DE_IND;
                                                itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND = (itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND != item.ALIQ_ICMS_ST_COMP_DE_IND) ? ((item.ALIQ_ICMS_ST_COMP_DE_IND != null) ? item.ALIQ_ICMS_ST_COMP_DE_IND : itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND) : itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND;
                                                itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND = (itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_IND : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND) : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = (itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;

                                                itemSalvar2.CST_COMPRA_DE_ATA = (itemSalvar2.CST_COMPRA_DE_ATA != item.CST_COMPRA_DE_ATA) ? ((item.CST_COMPRA_DE_ATA != null) ? item.CST_COMPRA_DE_ATA : itemSalvar2.CST_COMPRA_DE_ATA) : itemSalvar2.CST_COMPRA_DE_ATA;
                                                itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA = (itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA != item.ALIQ_ICMS_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_COMPRA_DE_ATA : itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA) : itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA;
                                                itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA = (itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA != item.ALIQ_ICMS_ST_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_ATA : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA) : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;

                                                itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL != item.CST_COMPRA_DE_SIMP_NACIONAL) ? ((item.CST_COMPRA_DE_SIMP_NACIONAL != null) ? item.CST_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;


                                                itemSalvar2.CST_DA_NFE_DA_IND_FORN = (itemSalvar2.CST_DA_NFE_DA_IND_FORN != item.CST_DA_NFE_DA_IND_FORN) ? ((item.CST_DA_NFE_DA_IND_FORN != null) ? item.CST_DA_NFE_DA_IND_FORN : itemSalvar2.CST_DA_NFE_DA_IND_FORN) : itemSalvar2.CST_DA_NFE_DA_IND_FORN;
                                                itemSalvar2.CST_DA_NFE_DE_ATA_FORN = (itemSalvar2.CST_DA_NFE_DE_ATA_FORN != item.CST_DA_NFE_DE_ATA_FORN) ? ((item.CST_DA_NFE_DE_ATA_FORN != null) ? item.CST_DA_NFE_DE_ATA_FORN : itemSalvar2.CST_DA_NFE_DE_ATA_FORN) : itemSalvar2.CST_DA_NFE_DE_ATA_FORN;
                                                itemSalvar2.CSOSNT_DANFE_DOS_NFOR = (itemSalvar2.CSOSNT_DANFE_DOS_NFOR != item.CSOSNT_DANFE_DOS_NFOR) ? ((item.CSOSNT_DANFE_DOS_NFOR != null) ? item.CSOSNT_DANFE_DOS_NFOR : itemSalvar2.CSOSNT_DANFE_DOS_NFOR) : itemSalvar2.CSOSNT_DANFE_DOS_NFOR;

                                                itemSalvar2.ALIQ_ICMS_NFE = (itemSalvar2.ALIQ_ICMS_NFE != item.ALIQ_ICMS_NFE) ? ((item.ALIQ_ICMS_NFE != null) ? item.ALIQ_ICMS_NFE : itemSalvar2.ALIQ_ICMS_NFE) : itemSalvar2.ALIQ_ICMS_NFE;
                                                itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA = (itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA != item.ALIQ_ICMS_NFE_FOR_ATA) ? ((item.ALIQ_ICMS_NFE_FOR_ATA != null) ? item.ALIQ_ICMS_NFE_FOR_ATA : itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA) : itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA;
                                                itemSalvar2.ALIQ_ICMS_NFE_FOR_SN = (itemSalvar2.ALIQ_ICMS_NFE_FOR_SN != item.ALIQ_ICMS_NFE_FOR_SN) ? ((item.ALIQ_ICMS_NFE_FOR_SN != null) ? item.ALIQ_ICMS_NFE_FOR_SN : itemSalvar2.ALIQ_ICMS_NFE_FOR_SN) : itemSalvar2.ALIQ_ICMS_NFE_FOR_SN;


                                                itemSalvar2.TIPO_MVA = (itemSalvar2.TIPO_MVA != item.TIPO_MVA) ? ((item.TIPO_MVA != null) ? item.TIPO_MVA : itemSalvar2.TIPO_MVA) : itemSalvar2.TIPO_MVA;

                                                itemSalvar2.VALOR_MVA_IND = (itemSalvar2.VALOR_MVA_IND != item.VALOR_MVA_IND) ? ((item.VALOR_MVA_IND != null) ? item.VALOR_MVA_IND : itemSalvar2.VALOR_MVA_IND) : itemSalvar2.VALOR_MVA_IND;

                                                itemSalvar2.INICIO_VIGENCIA_MVA = (itemSalvar2.INICIO_VIGENCIA_MVA != item.INICIO_VIGENCIA_MVA) ? ((item.INICIO_VIGENCIA_MVA != null) ? item.INICIO_VIGENCIA_MVA : itemSalvar2.INICIO_VIGENCIA_MVA) : itemSalvar2.INICIO_VIGENCIA_MVA;

                                                itemSalvar2.FIM_VIGENCIA_MVA = (itemSalvar2.FIM_VIGENCIA_MVA != item.FIM_VIGENCIA_MVA) ? ((item.FIM_VIGENCIA_MVA != null) ? item.FIM_VIGENCIA_MVA : itemSalvar2.FIM_VIGENCIA_MVA) : itemSalvar2.FIM_VIGENCIA_MVA;

                                                itemSalvar2.CREDITO_OUTORGADO = (itemSalvar2.CREDITO_OUTORGADO != item.CREDITO_OUTORGADO) ? ((item.CREDITO_OUTORGADO != null) ? item.CREDITO_OUTORGADO : itemSalvar2.CREDITO_OUTORGADO) : itemSalvar2.CREDITO_OUTORGADO;

                                                itemSalvar2.VALOR_MVA_ATACADO = (itemSalvar2.VALOR_MVA_ATACADO != item.VALOR_MVA_ATACADO) ? ((item.VALOR_MVA_ATACADO != null) ? item.VALOR_MVA_ATACADO : itemSalvar2.VALOR_MVA_ATACADO) : itemSalvar2.VALOR_MVA_ATACADO;

                                                itemSalvar2.REGIME_2560 = (itemSalvar2.REGIME_2560 != item.REGIME_2560) ? ((item.REGIME_2560 != null) ? item.REGIME_2560 : itemSalvar2.REGIME_2560) : itemSalvar2.REGIME_2560;

                                                itemSalvar2.UF_ORIGEM = (itemSalvar2.UF_ORIGEM != item.UF_ORIGEM) ? ((item.UF_ORIGEM != null) ? item.UF_ORIGEM : itemSalvar2.UF_ORIGEM) : itemSalvar2.UF_ORIGEM;

                                                itemSalvar2.UF_DESTINO = (itemSalvar2.UF_DESTINO != ufDestino[i]) ? ((item.UF_DESTINO != null) ? item.UF_DESTINO : ufDestino[i]) : itemSalvar2.UF_DESTINO;

                                                //data da inclusão/alteração
                                                itemSalvar2.DT_ALTERACAO = DateTime.Now;


                                                //try catch para salvar no banco e na lista de retorno
                                                try
                                                {
                                                    //COLOCA NA LISTA PARA RETORNO
                                                    listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
                                                    db.SaveChanges(); //SALVAR AS ALTERACOES
                                                    bd.SaveChanges();

                                                    contAlterados++;
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

                                            }//se o registro veio diferente de 0, DEVE SER ALTERADO


                                        }   //FIM DO ELSE PRODUTO JA CADASTRADO


                                    }


                                }
                                else//SE O CODIGO DE BARRAS VIER MENR QUE 7
                                {
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

                                    /*ROTINA PARA VERIFICAR SE O PRODUTO ESTÁ CADASTRADO E TRIBUTADO NA TABELA MATRIZ*/

                                    long? prodItem = long.Parse(item.PRODUTO_COD_BARRAS); //passa para long

                                    Produto cadProd = db.Produtos.Where(x => x.codBarras == prodItem && x.CodBarrasGErado.Equals(codBarrasGerado.ToString())).FirstOrDefault(); //verifica o cadastro


                                    Produto prodSalvar = new Produto();

                                    //se ele nao esta cadastrado na tabela de produto ele deve ser cadastrado nesta tabela
                                    if (cadProd == null)
                                    {

                                        prodSalvar.codBarras = Int64.Parse(item.PRODUTO_COD_BARRAS);
                                        prodSalvar.CodBarrasGErado = codBarrasGerado;
                                        prodSalvar.descricao = item.PRODUTO_DESCRICAO;
                                        prodSalvar.cest = item.PRODUTO_CEST;
                                        prodSalvar.ncm = item.PRODUTO_NCM;

                                        if (item.PRODUTO_CATEGORIA != null)
                                        {
                                            prodSalvar.idCategoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

                                        }
                                        else
                                        {
                                            prodSalvar.idCategoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
                                        }

                                        prodSalvar.status = 1;
                                        prodSalvar.dataCad = DateTime.Now;
                                        prodSalvar.dataAlt = DateTime.Now;
                                        prodSalvar.auditadoNCM = 0; //nao auditado

                                        //try-catch para salvar o produto na tabela
                                        try
                                        {

                                            db.Produtos.Add(prodSalvar);//objeto para ser salvo no banco
                                            bd.Produtos.Add(prodSalvar);//objeto para ser salvo no banco de comparação
                                            db.SaveChanges();

                                            contProdSalvos++;
                                        }
                                        catch (Exception e)
                                        {
                                            //erros e mensagens
                                            if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
                                            {

                                                _log.Error(e.InnerException.InnerException.Message);
                                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
                                            }

                                            if (e.Message != null)
                                            {

                                                _log.Error("ERRO AO SALVAR itemRec " + e.Message);
                                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
                                            }

                                            return BadRequest("ERRO AO SALVAR PRODUTO");
                                        }//fim do catch



                                    }
                                    //VERIFICAR SE HA TRIBUTAÇÃO PARA O PRODUTO DEPENDENDO DA EMPRESA (SIMPLES OU NORMAL)
                                    if (cadProd == null)
                                    {
                                        cadProd = db.Produtos.Where(x => x.codBarras == prodItem && x.CodBarrasGErado == codBarrasGerado).FirstOrDefault();
                                    }



                                    /*Salvar na tabela TributacaoNCM, caso nao exista*/
                                    string prodItemNCM = item.PRODUTO_NCM; //PEGA O NCM DO ITEM

                                    string[] ufDestinoTNCM = item.UF_DESTINO.Split('|');

                                    //PEGA O CRT E O REGIME TRIBUTARIO DA EMPRESA
                                    int? crt = empresa.crt;
                                    int? regime_tributario = empresa.regime_trib;

                                    //retira o elemento vazio do array
                                    ufDestinoTNCM = ufDestinoTNCM.Where(a => a != "").ToArray();

                                    //PASSAR PELOS DESTINOS PARA PROCURAR OS ITENS NA TABELA DE NCM - se faz necessario pois cada tributacao tem sua origem e destino
                                    for (int i = 0; i < ufDestinoTNCM.Count(); i++)
                                    {
                                        string dest = ufDestinoTNCM[i].ToString();
                                        //BUSCA PELO NCM NA TABELA, PASSANDO O CRT E O REGIME
                                        TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest && x.CRT == crt && x.Regime_Trib == regime_tributario).FirstOrDefault();
                                        if (tribnaNCM == null)
                                        {

                                            TributacaoNCM prodTribNCMSalvar = new TributacaoNCM();

                                            if (item.PRODUTO_CATEGORIA != null)
                                            {
                                                prodTribNCMSalvar.categoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

                                            }
                                            else
                                            {
                                                prodTribNCMSalvar.categoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
                                            }


                                            prodTribNCMSalvar.UF_Origem = item.UF_ORIGEM;
                                            prodTribNCMSalvar.UF_Destino = ufDestinoTNCM[i];
                                            prodTribNCMSalvar.cest = item.PRODUTO_CEST;
                                            prodTribNCMSalvar.ncm = item.PRODUTO_NCM;
                                            prodTribNCMSalvar.auditadoPorNCM = 0;
                                            prodTribNCMSalvar.CRT = crt;
                                            prodTribNCMSalvar.Regime_Trib = regime_tributario;
                                            prodTribNCMSalvar.dataCad = DateTime.Now;
                                            prodTribNCMSalvar.dataAlt = DateTime.Now;

                                            try
                                            {

                                                db.TributacaoNCM.Add(prodTribNCMSalvar);//objeto para ser salvo no banco
                                                db.SaveChanges();

                                            }
                                            catch (Exception e)
                                            {
                                                //erros e mensagens
                                                if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
                                                {

                                                    _log.Error(e.InnerException.InnerException.Message);
                                                    return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
                                                }

                                                if (e.Message != null)
                                                {

                                                    _log.Error("ERRO AO SALVAR itemRec " + e.Message);
                                                    return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
                                                }

                                                return BadRequest("ERRO AO SALVAR PRODUTO");
                                            }//fim do catch

                                        }



                                    }//FIM DO FOR PASSAR PELOS DESTINOS
                                     //contar os que vieram com codigo de barras 0
                                    if (item.PRODUTO_COD_BARRAS == "0")
                                    {
                                        prodZerado++;
                                    }
                                    //NA TABELA DO CLIENTE (CONTEXTO GTIN MENOR QUE 7
                                    //Verificar em todos os destinos se o item foi tributado no cliente
                                    string[] ufDestinoE = item.UF_DESTINO.Split('|');
                                    //retira o elemento vazio do array deixando somente os id dos registros
                                    ufDestinoE = ufDestinoE.Where(a => a != "").ToArray();


                                    for (int i = 0; i < ufDestinoE.Count(); i++)
                                    {
                                        string dest = ufDestinoE[i].ToString();

                                        var tribEmpresas2 = db.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID != 0); //select na tabela



                                        int contnumb = tribEmpresas2.Count();
                                        if (tribEmpresas2.Count() <= 0 && item.PRODUTO_COD_BARRAS != "0")
                                        {
                                            TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
                                            //atribunido dados ao objeto
                                            itemSalvar.CNPJ_EMPRESA = empresa.cnpj;
                                            itemSalvar.PRODUTO_COD_BARRAS = item.PRODUTO_COD_BARRAS;
                                            itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;
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
                                            itemSalvar.UF_ORIGEM = item.UF_ORIGEM;
                                            itemSalvar.UF_DESTINO = ufDestinoE[i];
                                            itemSalvar.PRODUTO_COD_INTERNO = item.PRODUTO_COD_INTERNO;
                                            //data da inclusão/alteração
                                            itemSalvar.DT_ALTERACAO = DateTime.Now;
                                            //Verifica se o item veio ativo, caso venha null considera ativo
                                            if (item.ATIVO == null)
                                            {
                                                itemSalvar.ATIVO = 1;
                                            }
                                            else
                                            {
                                                itemSalvar.ATIVO = sbyte.Parse(item.ATIVO);
                                            }



                                            //try catch para salvar no banco e na lista de retorno
                                            try
                                            {
                                                //salva os itens quando nao existe na tabela
                                                db.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco
                                                bd.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco de comparação
                                                listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
                                                db.SaveChanges();
                                                db.SaveChanges();

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



                                        }
                                        else //se nao foi importado incluir na tabela do cliente
                                        {
                                            //se o codigo de barras não foi importado o entra na condição, ou seja o retorno do tribempresas2 é 0
                                            //sendo zero o produto nao foi importado, agora ele será com todos os seus dados
                                            //alteração 16092021->alem de nao ter encontrado nada no banco, count=0 o codigo de barras deve ser diferente de 0(zero)
                                            //pegar o id desse registro
                                            var idDoRegistros = db.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID).FirstOrDefault();
                                            var idDoRegistros2 = bd.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID).FirstOrDefault();

                                            if (idDoRegistros != 0)
                                            {
                                                TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
                                                itemSalvar = db.TributacaoEmpresas.Find(idDoRegistros);
                                                itemSalvar.PRODUTO_DESCRICAO = (itemSalvar.PRODUTO_DESCRICAO != item.PRODUTO_DESCRICAO) ? ((item.PRODUTO_DESCRICAO != null) ? item.PRODUTO_DESCRICAO : itemSalvar.PRODUTO_DESCRICAO) : itemSalvar.PRODUTO_DESCRICAO;
                                                itemSalvar.PRODUTO_CEST = (itemSalvar.PRODUTO_CEST != item.PRODUTO_CEST) ? ((item.PRODUTO_CEST != null) ? item.PRODUTO_CEST : itemSalvar.PRODUTO_CEST) : itemSalvar.PRODUTO_CEST;
                                                itemSalvar.PRODUTO_NCM = (itemSalvar.PRODUTO_NCM != item.PRODUTO_NCM) ? ((item.PRODUTO_NCM != null) ? item.PRODUTO_NCM : itemSalvar.PRODUTO_NCM) : itemSalvar.PRODUTO_NCM;
                                                itemSalvar.PRODUTO_CATEGORIA = (itemSalvar.PRODUTO_CATEGORIA != item.PRODUTO_CATEGORIA) ? ((item.PRODUTO_CATEGORIA != null) ? item.PRODUTO_CATEGORIA : itemSalvar.PRODUTO_CATEGORIA) : itemSalvar.PRODUTO_CATEGORIA;
                                                itemSalvar.FECP = (itemSalvar.FECP != item.FECP) ? ((item.FECP != null) ? item.FECP : itemSalvar.FECP) : itemSalvar.FECP;
                                                itemSalvar.COD_NAT_RECEITA = (itemSalvar.COD_NAT_RECEITA != item.COD_NAT_RECEITA) ? ((item.COD_NAT_RECEITA != null) ? item.COD_NAT_RECEITA : itemSalvar.COD_NAT_RECEITA) : itemSalvar.COD_NAT_RECEITA;

                                                itemSalvar.CST_ENTRADA_PIS_COFINS = (itemSalvar.CST_ENTRADA_PIS_COFINS != item.CST_ENTRADA_PIS_COFINS) ? ((item.CST_ENTRADA_PIS_COFINS != null) ? item.CST_ENTRADA_PIS_COFINS : itemSalvar.CST_ENTRADA_PIS_COFINS) : itemSalvar.CST_ENTRADA_PIS_COFINS;
                                                itemSalvar.CST_SAIDA_PIS_COFINS = (itemSalvar.CST_SAIDA_PIS_COFINS != item.CST_SAIDA_PIS_COFINS) ? ((item.CST_SAIDA_PIS_COFINS != null) ? item.CST_SAIDA_PIS_COFINS : itemSalvar.CST_SAIDA_PIS_COFINS) : itemSalvar.CST_SAIDA_PIS_COFINS;
                                                itemSalvar.ALIQ_ENTRADA_PIS = (itemSalvar.ALIQ_ENTRADA_PIS != item.ALIQ_ENTRADA_PIS) ? ((item.ALIQ_ENTRADA_PIS != null) ? item.ALIQ_ENTRADA_PIS : itemSalvar.ALIQ_ENTRADA_PIS) : itemSalvar.ALIQ_ENTRADA_PIS;
                                                itemSalvar.ALIQ_SAIDA_PIS = (itemSalvar.ALIQ_SAIDA_PIS != item.ALIQ_SAIDA_PIS) ? ((item.ALIQ_SAIDA_PIS != null) ? item.ALIQ_SAIDA_PIS : itemSalvar.ALIQ_SAIDA_PIS) : itemSalvar.ALIQ_SAIDA_PIS;
                                                itemSalvar.ALIQ_ENTRADA_COFINS = (itemSalvar.ALIQ_ENTRADA_COFINS != item.ALIQ_ENTRADA_COFINS) ? ((item.ALIQ_ENTRADA_COFINS != null) ? item.ALIQ_ENTRADA_COFINS : itemSalvar.ALIQ_ENTRADA_COFINS) : itemSalvar.ALIQ_ENTRADA_COFINS;
                                                itemSalvar.ALIQ_SAIDA_COFINS = (itemSalvar.ALIQ_SAIDA_COFINS != item.ALIQ_SAIDA_COFINS) ? ((item.ALIQ_SAIDA_COFINS != null) ? item.ALIQ_SAIDA_COFINS : itemSalvar.ALIQ_SAIDA_COFINS) : itemSalvar.ALIQ_SAIDA_COFINS;

                                                itemSalvar.CST_VENDA_ATA = (itemSalvar.CST_VENDA_ATA != item.CST_VENDA_ATA) ? ((item.CST_VENDA_ATA != null) ? item.CST_VENDA_ATA : itemSalvar.CST_VENDA_ATA) : itemSalvar.CST_VENDA_ATA;
                                                itemSalvar.ALIQ_ICMS_VENDA_ATA = (itemSalvar.ALIQ_ICMS_VENDA_ATA != item.ALIQ_ICMS_VENDA_ATA) ? ((item.ALIQ_ICMS_VENDA_ATA != null) ? item.ALIQ_ICMS_VENDA_ATA : itemSalvar.ALIQ_ICMS_VENDA_ATA) : itemSalvar.ALIQ_ICMS_VENDA_ATA;
                                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA != item.ALIQ_ICMS_ST_VENDA_ATA) ? ((item.ALIQ_ICMS_ST_VENDA_ATA != null) ? item.ALIQ_ICMS_ST_VENDA_ATA : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA != item.RED_BASE_CALC_ICMS_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA;

                                                itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL != item.CST_VENDA_ATA_SIMP_NACIONAL) ? ((item.CST_VENDA_ATA_SIMP_NACIONAL != null) ? item.CST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;


                                                itemSalvar.CST_VENDA_VAREJO_CONT = (itemSalvar.CST_VENDA_VAREJO_CONT != item.CST_VENDA_VAREJO_CONT) ? ((item.CST_VENDA_VAREJO_CONT != null) ? item.CST_VENDA_VAREJO_CONT : itemSalvar.CST_VENDA_VAREJO_CONT) : itemSalvar.CST_VENDA_VAREJO_CONT;
                                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT != item.ALIQ_ICMS_VENDA_VAREJO_CONT) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONT != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONT : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT;
                                                itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT != item.RED_BASE_CALC_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT;
                                                itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;


                                                itemSalvar.CST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.CST_VENDA_VAREJO_CONS_FINAL != item.CST_VENDA_VAREJO_CONS_FINAL) ? ((item.CST_VENDA_VAREJO_CONS_FINAL != null) ? item.CST_VENDA_VAREJO_CONS_FINAL : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;


                                                itemSalvar.CST_COMPRA_DE_IND = (itemSalvar.CST_COMPRA_DE_IND != item.CST_COMPRA_DE_IND) ? ((item.CST_COMPRA_DE_IND != null) ? item.CST_COMPRA_DE_IND : itemSalvar.CST_COMPRA_DE_IND) : itemSalvar.CST_COMPRA_DE_IND;
                                                itemSalvar.ALIQ_ICMS_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_COMP_DE_IND != item.ALIQ_ICMS_COMP_DE_IND) ? ((item.ALIQ_ICMS_COMP_DE_IND != null) ? item.ALIQ_ICMS_COMP_DE_IND : itemSalvar.ALIQ_ICMS_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_COMP_DE_IND;
                                                itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND != item.ALIQ_ICMS_ST_COMP_DE_IND) ? ((item.ALIQ_ICMS_ST_COMP_DE_IND != null) ? item.ALIQ_ICMS_ST_COMP_DE_IND : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND;
                                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;

                                                itemSalvar.CST_COMPRA_DE_ATA = (itemSalvar.CST_COMPRA_DE_ATA != item.CST_COMPRA_DE_ATA) ? ((item.CST_COMPRA_DE_ATA != null) ? item.CST_COMPRA_DE_ATA : itemSalvar.CST_COMPRA_DE_ATA) : itemSalvar.CST_COMPRA_DE_ATA;
                                                itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA != item.ALIQ_ICMS_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA;
                                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA != item.ALIQ_ICMS_ST_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;

                                                itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL != item.CST_COMPRA_DE_SIMP_NACIONAL) ? ((item.CST_COMPRA_DE_SIMP_NACIONAL != null) ? item.CST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
                                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;


                                                itemSalvar.CST_DA_NFE_DA_IND_FORN = (itemSalvar.CST_DA_NFE_DA_IND_FORN != item.CST_DA_NFE_DA_IND_FORN) ? ((item.CST_DA_NFE_DA_IND_FORN != null) ? item.CST_DA_NFE_DA_IND_FORN : itemSalvar.CST_DA_NFE_DA_IND_FORN) : itemSalvar.CST_DA_NFE_DA_IND_FORN;
                                                itemSalvar.CST_DA_NFE_DE_ATA_FORN = (itemSalvar.CST_DA_NFE_DE_ATA_FORN != item.CST_DA_NFE_DE_ATA_FORN) ? ((item.CST_DA_NFE_DE_ATA_FORN != null) ? item.CST_DA_NFE_DE_ATA_FORN : itemSalvar.CST_DA_NFE_DE_ATA_FORN) : itemSalvar.CST_DA_NFE_DE_ATA_FORN;
                                                itemSalvar.CSOSNT_DANFE_DOS_NFOR = (itemSalvar.CSOSNT_DANFE_DOS_NFOR != item.CSOSNT_DANFE_DOS_NFOR) ? ((item.CSOSNT_DANFE_DOS_NFOR != null) ? item.CSOSNT_DANFE_DOS_NFOR : itemSalvar.CSOSNT_DANFE_DOS_NFOR) : itemSalvar.CSOSNT_DANFE_DOS_NFOR;

                                                itemSalvar.ALIQ_ICMS_NFE = (itemSalvar.ALIQ_ICMS_NFE != item.ALIQ_ICMS_NFE) ? ((item.ALIQ_ICMS_NFE != null) ? item.ALIQ_ICMS_NFE : itemSalvar.ALIQ_ICMS_NFE) : itemSalvar.ALIQ_ICMS_NFE;
                                                itemSalvar.ALIQ_ICMS_NFE_FOR_ATA = (itemSalvar.ALIQ_ICMS_NFE_FOR_ATA != item.ALIQ_ICMS_NFE_FOR_ATA) ? ((item.ALIQ_ICMS_NFE_FOR_ATA != null) ? item.ALIQ_ICMS_NFE_FOR_ATA : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA) : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA;
                                                itemSalvar.ALIQ_ICMS_NFE_FOR_SN = (itemSalvar.ALIQ_ICMS_NFE_FOR_SN != item.ALIQ_ICMS_NFE_FOR_SN) ? ((item.ALIQ_ICMS_NFE_FOR_SN != null) ? item.ALIQ_ICMS_NFE_FOR_SN : itemSalvar.ALIQ_ICMS_NFE_FOR_SN) : itemSalvar.ALIQ_ICMS_NFE_FOR_SN;


                                                itemSalvar.TIPO_MVA = (itemSalvar.TIPO_MVA != item.TIPO_MVA) ? ((item.TIPO_MVA != null) ? item.TIPO_MVA : itemSalvar.TIPO_MVA) : itemSalvar.TIPO_MVA;

                                                itemSalvar.VALOR_MVA_IND = (itemSalvar.VALOR_MVA_IND != item.VALOR_MVA_IND) ? ((item.VALOR_MVA_IND != null) ? item.VALOR_MVA_IND : itemSalvar.VALOR_MVA_IND) : itemSalvar.VALOR_MVA_IND;

                                                itemSalvar.INICIO_VIGENCIA_MVA = (itemSalvar.INICIO_VIGENCIA_MVA != item.INICIO_VIGENCIA_MVA) ? ((item.INICIO_VIGENCIA_MVA != null) ? item.INICIO_VIGENCIA_MVA : itemSalvar.INICIO_VIGENCIA_MVA) : itemSalvar.INICIO_VIGENCIA_MVA;

                                                itemSalvar.FIM_VIGENCIA_MVA = (itemSalvar.FIM_VIGENCIA_MVA != item.FIM_VIGENCIA_MVA) ? ((item.FIM_VIGENCIA_MVA != null) ? item.FIM_VIGENCIA_MVA : itemSalvar.FIM_VIGENCIA_MVA) : itemSalvar.FIM_VIGENCIA_MVA;

                                                itemSalvar.CREDITO_OUTORGADO = (itemSalvar.CREDITO_OUTORGADO != item.CREDITO_OUTORGADO) ? ((item.CREDITO_OUTORGADO != null) ? item.CREDITO_OUTORGADO : itemSalvar.CREDITO_OUTORGADO) : itemSalvar.CREDITO_OUTORGADO;

                                                itemSalvar.VALOR_MVA_ATACADO = (itemSalvar.VALOR_MVA_ATACADO != item.VALOR_MVA_ATACADO) ? ((item.VALOR_MVA_ATACADO != null) ? item.VALOR_MVA_ATACADO : itemSalvar.VALOR_MVA_ATACADO) : itemSalvar.VALOR_MVA_ATACADO;

                                                itemSalvar.REGIME_2560 = (itemSalvar.REGIME_2560 != item.REGIME_2560) ? ((item.REGIME_2560 != null) ? item.REGIME_2560 : itemSalvar.REGIME_2560) : itemSalvar.REGIME_2560;

                                                itemSalvar.UF_ORIGEM = (itemSalvar.UF_ORIGEM != item.UF_ORIGEM) ? ((item.UF_ORIGEM != null) ? item.UF_ORIGEM : itemSalvar.UF_ORIGEM) : itemSalvar.UF_ORIGEM;

                                                itemSalvar.UF_DESTINO = (itemSalvar.UF_DESTINO != ufDestinoE[i]) ? ((item.UF_DESTINO != null) ? ufDestinoE[i] : itemSalvar.UF_DESTINO) : itemSalvar.UF_DESTINO;

                                                //data da inclusão/alteração
                                                itemSalvar.DT_ALTERACAO = DateTime.Now;


                                                //segundo banco: SALVAR ALTERAÇÕES NO BANCO DE BKP OU TABELAS INICIAIS DO CLIENTE
                                                TributacaoEmpresa itemSalvar2 = new TributacaoEmpresa();
                                                itemSalvar2 = bd.TributacaoEmpresas.Find(idDoRegistros2);

                                                itemSalvar2.PRODUTO_DESCRICAO = (itemSalvar2.PRODUTO_DESCRICAO != item.PRODUTO_DESCRICAO) ? ((item.PRODUTO_DESCRICAO != null) ? item.PRODUTO_DESCRICAO : itemSalvar2.PRODUTO_DESCRICAO) : itemSalvar2.PRODUTO_DESCRICAO;
                                                itemSalvar2.PRODUTO_CEST = (itemSalvar2.PRODUTO_CEST != item.PRODUTO_CEST) ? ((item.PRODUTO_CEST != null) ? item.PRODUTO_CEST : itemSalvar2.PRODUTO_CEST) : itemSalvar2.PRODUTO_CEST;
                                                itemSalvar2.PRODUTO_NCM = (itemSalvar2.PRODUTO_NCM != item.PRODUTO_NCM) ? ((item.PRODUTO_NCM != null) ? item.PRODUTO_NCM : itemSalvar2.PRODUTO_NCM) : itemSalvar2.PRODUTO_NCM;
                                                itemSalvar2.PRODUTO_CATEGORIA = (itemSalvar2.PRODUTO_CATEGORIA != item.PRODUTO_CATEGORIA) ? ((item.PRODUTO_CATEGORIA != null) ? item.PRODUTO_CATEGORIA : itemSalvar2.PRODUTO_CATEGORIA) : itemSalvar2.PRODUTO_CATEGORIA;
                                                itemSalvar2.FECP = (itemSalvar2.FECP != item.FECP) ? ((item.FECP != null) ? item.FECP : itemSalvar2.FECP) : itemSalvar2.FECP;
                                                itemSalvar2.COD_NAT_RECEITA = (itemSalvar2.COD_NAT_RECEITA != item.COD_NAT_RECEITA) ? ((item.COD_NAT_RECEITA != null) ? item.COD_NAT_RECEITA : itemSalvar2.COD_NAT_RECEITA) : itemSalvar2.COD_NAT_RECEITA;

                                                itemSalvar2.CST_ENTRADA_PIS_COFINS = (itemSalvar2.CST_ENTRADA_PIS_COFINS != item.CST_ENTRADA_PIS_COFINS) ? ((item.CST_ENTRADA_PIS_COFINS != null) ? item.CST_ENTRADA_PIS_COFINS : itemSalvar2.CST_ENTRADA_PIS_COFINS) : itemSalvar2.CST_ENTRADA_PIS_COFINS;
                                                itemSalvar2.CST_SAIDA_PIS_COFINS = (itemSalvar2.CST_SAIDA_PIS_COFINS != item.CST_SAIDA_PIS_COFINS) ? ((item.CST_SAIDA_PIS_COFINS != null) ? item.CST_SAIDA_PIS_COFINS : itemSalvar2.CST_SAIDA_PIS_COFINS) : itemSalvar2.CST_SAIDA_PIS_COFINS;
                                                itemSalvar2.ALIQ_ENTRADA_PIS = (itemSalvar2.ALIQ_ENTRADA_PIS != item.ALIQ_ENTRADA_PIS) ? ((item.ALIQ_ENTRADA_PIS != null) ? item.ALIQ_ENTRADA_PIS : itemSalvar2.ALIQ_ENTRADA_PIS) : itemSalvar2.ALIQ_ENTRADA_PIS;
                                                itemSalvar2.ALIQ_SAIDA_PIS = (itemSalvar2.ALIQ_SAIDA_PIS != item.ALIQ_SAIDA_PIS) ? ((item.ALIQ_SAIDA_PIS != null) ? item.ALIQ_SAIDA_PIS : itemSalvar2.ALIQ_SAIDA_PIS) : itemSalvar2.ALIQ_SAIDA_PIS;
                                                itemSalvar2.ALIQ_ENTRADA_COFINS = (itemSalvar2.ALIQ_ENTRADA_COFINS != item.ALIQ_ENTRADA_COFINS) ? ((item.ALIQ_ENTRADA_COFINS != null) ? item.ALIQ_ENTRADA_COFINS : itemSalvar2.ALIQ_ENTRADA_COFINS) : itemSalvar2.ALIQ_ENTRADA_COFINS;
                                                itemSalvar2.ALIQ_SAIDA_COFINS = (itemSalvar2.ALIQ_SAIDA_COFINS != item.ALIQ_SAIDA_COFINS) ? ((item.ALIQ_SAIDA_COFINS != null) ? item.ALIQ_SAIDA_COFINS : itemSalvar2.ALIQ_SAIDA_COFINS) : itemSalvar2.ALIQ_SAIDA_COFINS;

                                                itemSalvar2.CST_VENDA_ATA = (itemSalvar2.CST_VENDA_ATA != item.CST_VENDA_ATA) ? ((item.CST_VENDA_ATA != null) ? item.CST_VENDA_ATA : itemSalvar2.CST_VENDA_ATA) : itemSalvar2.CST_VENDA_ATA;
                                                itemSalvar2.ALIQ_ICMS_VENDA_ATA = (itemSalvar2.ALIQ_ICMS_VENDA_ATA != item.ALIQ_ICMS_VENDA_ATA) ? ((item.ALIQ_ICMS_VENDA_ATA != null) ? item.ALIQ_ICMS_VENDA_ATA : itemSalvar2.ALIQ_ICMS_VENDA_ATA) : itemSalvar2.ALIQ_ICMS_VENDA_ATA;
                                                itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA = (itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA != item.ALIQ_ICMS_ST_VENDA_ATA) ? ((item.ALIQ_ICMS_ST_VENDA_ATA != null) ? item.ALIQ_ICMS_ST_VENDA_ATA : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA) : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA != item.RED_BASE_CALC_ICMS_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA;

                                                itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL != item.CST_VENDA_ATA_SIMP_NACIONAL) ? ((item.CST_VENDA_ATA_SIMP_NACIONAL != null) ? item.CST_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.CST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;


                                                itemSalvar2.CST_VENDA_VAREJO_CONT = (itemSalvar2.CST_VENDA_VAREJO_CONT != item.CST_VENDA_VAREJO_CONT) ? ((item.CST_VENDA_VAREJO_CONT != null) ? item.CST_VENDA_VAREJO_CONT : itemSalvar2.CST_VENDA_VAREJO_CONT) : itemSalvar2.CST_VENDA_VAREJO_CONT;
                                                itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT = (itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT != item.ALIQ_ICMS_VENDA_VAREJO_CONT) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONT != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONT : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT) : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONT;
                                                itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT = (itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT != item.RED_BASE_CALC_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_VENDA_VAREJO_CONT : itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT) : itemSalvar2.RED_BASE_CALC_VENDA_VAREJO_CONT;
                                                itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = (itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT : itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) : itemSalvar2.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;


                                                itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL != item.CST_VENDA_VAREJO_CONS_FINAL) ? ((item.CST_VENDA_VAREJO_CONS_FINAL != null) ? item.CST_VENDA_VAREJO_CONS_FINAL : itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.CST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar2.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;


                                                itemSalvar2.CST_COMPRA_DE_IND = (itemSalvar2.CST_COMPRA_DE_IND != item.CST_COMPRA_DE_IND) ? ((item.CST_COMPRA_DE_IND != null) ? item.CST_COMPRA_DE_IND : itemSalvar2.CST_COMPRA_DE_IND) : itemSalvar2.CST_COMPRA_DE_IND;
                                                itemSalvar2.ALIQ_ICMS_COMP_DE_IND = (itemSalvar2.ALIQ_ICMS_COMP_DE_IND != item.ALIQ_ICMS_COMP_DE_IND) ? ((item.ALIQ_ICMS_COMP_DE_IND != null) ? item.ALIQ_ICMS_COMP_DE_IND : itemSalvar2.ALIQ_ICMS_COMP_DE_IND) : itemSalvar2.ALIQ_ICMS_COMP_DE_IND;
                                                itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND = (itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND != item.ALIQ_ICMS_ST_COMP_DE_IND) ? ((item.ALIQ_ICMS_ST_COMP_DE_IND != null) ? item.ALIQ_ICMS_ST_COMP_DE_IND : itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND) : itemSalvar2.ALIQ_ICMS_ST_COMP_DE_IND;
                                                itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND = (itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_IND : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND) : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = (itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;

                                                itemSalvar2.CST_COMPRA_DE_ATA = (itemSalvar2.CST_COMPRA_DE_ATA != item.CST_COMPRA_DE_ATA) ? ((item.CST_COMPRA_DE_ATA != null) ? item.CST_COMPRA_DE_ATA : itemSalvar2.CST_COMPRA_DE_ATA) : itemSalvar2.CST_COMPRA_DE_ATA;
                                                itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA = (itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA != item.ALIQ_ICMS_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_COMPRA_DE_ATA : itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA) : itemSalvar2.ALIQ_ICMS_COMPRA_DE_ATA;
                                                itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA = (itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA != item.ALIQ_ICMS_ST_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_ATA : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA) : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = (itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;

                                                itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL != item.CST_COMPRA_DE_SIMP_NACIONAL) ? ((item.CST_COMPRA_DE_SIMP_NACIONAL != null) ? item.CST_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.CST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
                                                itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar2.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;


                                                itemSalvar2.CST_DA_NFE_DA_IND_FORN = (itemSalvar2.CST_DA_NFE_DA_IND_FORN != item.CST_DA_NFE_DA_IND_FORN) ? ((item.CST_DA_NFE_DA_IND_FORN != null) ? item.CST_DA_NFE_DA_IND_FORN : itemSalvar2.CST_DA_NFE_DA_IND_FORN) : itemSalvar2.CST_DA_NFE_DA_IND_FORN;
                                                itemSalvar2.CST_DA_NFE_DE_ATA_FORN = (itemSalvar2.CST_DA_NFE_DE_ATA_FORN != item.CST_DA_NFE_DE_ATA_FORN) ? ((item.CST_DA_NFE_DE_ATA_FORN != null) ? item.CST_DA_NFE_DE_ATA_FORN : itemSalvar2.CST_DA_NFE_DE_ATA_FORN) : itemSalvar2.CST_DA_NFE_DE_ATA_FORN;
                                                itemSalvar2.CSOSNT_DANFE_DOS_NFOR = (itemSalvar2.CSOSNT_DANFE_DOS_NFOR != item.CSOSNT_DANFE_DOS_NFOR) ? ((item.CSOSNT_DANFE_DOS_NFOR != null) ? item.CSOSNT_DANFE_DOS_NFOR : itemSalvar2.CSOSNT_DANFE_DOS_NFOR) : itemSalvar2.CSOSNT_DANFE_DOS_NFOR;

                                                itemSalvar2.ALIQ_ICMS_NFE = (itemSalvar2.ALIQ_ICMS_NFE != item.ALIQ_ICMS_NFE) ? ((item.ALIQ_ICMS_NFE != null) ? item.ALIQ_ICMS_NFE : itemSalvar2.ALIQ_ICMS_NFE) : itemSalvar2.ALIQ_ICMS_NFE;
                                                itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA = (itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA != item.ALIQ_ICMS_NFE_FOR_ATA) ? ((item.ALIQ_ICMS_NFE_FOR_ATA != null) ? item.ALIQ_ICMS_NFE_FOR_ATA : itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA) : itemSalvar2.ALIQ_ICMS_NFE_FOR_ATA;
                                                itemSalvar2.ALIQ_ICMS_NFE_FOR_SN = (itemSalvar2.ALIQ_ICMS_NFE_FOR_SN != item.ALIQ_ICMS_NFE_FOR_SN) ? ((item.ALIQ_ICMS_NFE_FOR_SN != null) ? item.ALIQ_ICMS_NFE_FOR_SN : itemSalvar2.ALIQ_ICMS_NFE_FOR_SN) : itemSalvar2.ALIQ_ICMS_NFE_FOR_SN;


                                                itemSalvar2.TIPO_MVA = (itemSalvar2.TIPO_MVA != item.TIPO_MVA) ? ((item.TIPO_MVA != null) ? item.TIPO_MVA : itemSalvar2.TIPO_MVA) : itemSalvar2.TIPO_MVA;

                                                itemSalvar2.VALOR_MVA_IND = (itemSalvar2.VALOR_MVA_IND != item.VALOR_MVA_IND) ? ((item.VALOR_MVA_IND != null) ? item.VALOR_MVA_IND : itemSalvar2.VALOR_MVA_IND) : itemSalvar2.VALOR_MVA_IND;

                                                itemSalvar2.INICIO_VIGENCIA_MVA = (itemSalvar2.INICIO_VIGENCIA_MVA != item.INICIO_VIGENCIA_MVA) ? ((item.INICIO_VIGENCIA_MVA != null) ? item.INICIO_VIGENCIA_MVA : itemSalvar2.INICIO_VIGENCIA_MVA) : itemSalvar2.INICIO_VIGENCIA_MVA;

                                                itemSalvar2.FIM_VIGENCIA_MVA = (itemSalvar2.FIM_VIGENCIA_MVA != item.FIM_VIGENCIA_MVA) ? ((item.FIM_VIGENCIA_MVA != null) ? item.FIM_VIGENCIA_MVA : itemSalvar2.FIM_VIGENCIA_MVA) : itemSalvar2.FIM_VIGENCIA_MVA;

                                                itemSalvar2.CREDITO_OUTORGADO = (itemSalvar2.CREDITO_OUTORGADO != item.CREDITO_OUTORGADO) ? ((item.CREDITO_OUTORGADO != null) ? item.CREDITO_OUTORGADO : itemSalvar2.CREDITO_OUTORGADO) : itemSalvar2.CREDITO_OUTORGADO;

                                                itemSalvar2.VALOR_MVA_ATACADO = (itemSalvar2.VALOR_MVA_ATACADO != item.VALOR_MVA_ATACADO) ? ((item.VALOR_MVA_ATACADO != null) ? item.VALOR_MVA_ATACADO : itemSalvar2.VALOR_MVA_ATACADO) : itemSalvar2.VALOR_MVA_ATACADO;

                                                itemSalvar2.REGIME_2560 = (itemSalvar2.REGIME_2560 != item.REGIME_2560) ? ((item.REGIME_2560 != null) ? item.REGIME_2560 : itemSalvar2.REGIME_2560) : itemSalvar2.REGIME_2560;

                                                itemSalvar2.UF_ORIGEM = (itemSalvar2.UF_ORIGEM != item.UF_ORIGEM) ? ((item.UF_ORIGEM != null) ? item.UF_ORIGEM : itemSalvar2.UF_ORIGEM) : itemSalvar2.UF_ORIGEM;

                                                itemSalvar2.UF_DESTINO = (itemSalvar2.UF_DESTINO != ufDestinoE[i]) ? ((item.UF_DESTINO != null) ? item.UF_DESTINO : ufDestinoE[i]) : itemSalvar2.UF_DESTINO;

                                                //data da inclusão/alteração
                                                itemSalvar2.DT_ALTERACAO = DateTime.Now;

                                                //try catch para salvar no banco e na lista de retorno
                                                try
                                                {

                                                    //salva os itens quando existema na tabela: SALVA AS ALTERAÇÕES
                                                    listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
                                                    db.SaveChanges(); //SALVAR AS ALTERACOES
                                                    bd.SaveChanges();
                                                    contAlterados++;
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
                                            }
                                        }

                                    }//FIM DO FOR DE DESTINOS
                                    codBarrasTamanho++; //quantidade de produtos que o codigo de barras era menor que 7
                                }   //fim DO ELSE DE TAMANHO



                            }
                            else //CASO VENHA NULO OU ZERADO ELE SOMA MAIS UM NA VARIAVEL AUXILIAR E PASSA PARA O PROXIMO ITEM DO JSON
                            {
                                /*TO-DO : PRODUTOS QUE NÃO POSSUEM CODIGO DE BARRAS*/
                                aux++; //soma um a cada vez que um item não possuir codigo de barras
                            }
                        }
                    }

                }



            } //fim foreach ITEM A ITEM DO JSON
          

           
            
            if (contAlterados <= 0 && cont <= 0)
            {
                if (auxEstado > 0)
                {
                    var myError = new
                    {
                        sucess = "false",
                        errors = "UF DE ORIGEM OU DESTINO INFORMADOS INCORRETAMENTE"
                    };
                    return BadRequest(myError.ToString());
                }
                else
                {
                    var myError2 = new
                    {
                        sucess = "false",
                        errors = "NENHUM PRODUTO IMPORTADO,  ESTÃO COM COD_BARRAS = NULL(VAZIOS) / COD_BARRAS igual a 0(zero) / COD_BARRAS com tamanho incorreto"
                    };

                    //return (IHttpActionResult)Request.CreateResponse(HttpStatusCode.BadRequest, myError);
                    //return (HttpStatusCode.BadRequest, Json("email or password is null"));
                    return BadRequest(myError2.ToString());
                }
            }

            _log.Debug("FINAL DE PROCESSO COM " + cont + " ITENS SALVOS");
            return Ok(new { sucess = "true", itensSalvos = cont, itensRegistrosCNPJInvalido = contRegistrosCNPJInválido,  ufOrigemDestinoIncorretos = auxEstado, semCodigoBarras = aux, itemCodigoBarrasZero = prodZerado.ToString(), itensAlterados = contAlterados, codBarrasTamanhoIncorreto = codBarrasTamanho, totalItens = itens.Count() }); ;

        }



        //Excluir dados de importação pelo cnpj
        // POST: api/ItemTributacaoDelete/123
        [Route("api/ItemTributacaoDelete/{cnpj}")]
        public IHttpActionResult DeleteDeletaItemTributacao(string cnpj)
        {
            //pegar o header que é a chave
            var chaveToken = HttpContext.Current.Request.Headers["chave"];
            if (chaveToken == null)
            {
                return BadRequest("AUTENTICAÇÃO INCORRETA. CHAVE NECESSÁRIA");
            }

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
            else
            {
                var empresaCliente = from s in db.Empresas select s;
                empresaCliente = empresaCliente.Where(s => s.cnpj.Equals(cnpjFormatado));
                empresaCliente = empresaCliente.Where(s => s.SoftwareHouse.Chave.Equals(chaveToken.ToString()));
                if (empresaCliente.Count() == 0)
                {
                    return BadRequest("Chave incorreta para o CNPJ: " + cnpjFormatado + ": CORRESPONDENCIA INVÁLIDA");

                }
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
        public static bool VerificaString(string data)
        {
            bool encontrado = false;
            string[] estados = { "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO" };


            for (int i = 0; i < estados.Length; i++)
            {
                if (data == estados[i])
                {
                    encontrado = true;
                    break; //Foi encontrado, não necessita de procurar mais
                }
            }

            return encontrado;
        }


        ////BKP-NAO APAGAR DESSA LINHA ATE O DISPOSE
        //[Route("api/ItemTributacao/{cnpj}")]
        //public IHttpActionResult PostListaTributacao(string cnpj, List<ItemTributacaoJson> itens)
        //{
        //    //VAriavel auxiliar para receber o codigo de barras gerado pelo sistema (cód auxiliar na identificacao do item)
        //    string codBarrasGerado = "";

        //    //pegar o header que é a chave
        //    var chaveToken = HttpContext.Current.Request.Headers["chave"];

        //    //verificar se veio nula
        //    if (chaveToken == null)
        //    {
        //        return BadRequest("AUTENTICAÇÃO INCORRETA. CHAVE NECESSÁRIA");
        //    }


        //    //contador de intens que não possuem codigo de barras
        //    int aux = 0;
        //    int prodZerado = 0;

        //    //CONTADOR DE ITENS SEM ESTADO ORIGEM E DESTINO
        //    int auxEstado = 0;
        //    int codBarrasTamanho = 0;

        //    //verificar a qtd de digitos do cnpj
        //    if (cnpj.Length != 14)
        //    {
        //        return BadRequest("CNPJ PASSADO COMO PARÂMETRO ESTÁ INCORRETO");
        //    }
        //    if (itens == null)
        //    {
        //        return BadRequest("NENHUM ITEM INFORMADO PARA IMPORTAÇÃO");
        //    }


        //    /*CONFIRMAR EXISTENCIA DA EMPRESA */

        //    //formatando a string
        //    string cnpjFormatado = FormataCnpj.FormatarCNPJ(cnpj);

        //    ////Instancia do contexto do banco
        //    //MtxApiContext db = new MtxApiContext();

        //    //Cria o objeto empresa pelo seu cnpj
        //    Empresa empresa = db.Empresas.FirstOrDefault(x => x.cnpj.Equals(cnpjFormatado));

        //    //objeto para busca de produtos (neste caso para simples nacional
        //    List<TributacaoSN> tribProdSN = db.TributacaoSN.ToList();



        //    //se for nula, não existe
        //    if (empresa == null)
        //    {
        //        return BadRequest("EMPRESA NÃO FOI LOCALIZADA PELO CNPJ");
        //    }
        //    //se a chave nao corresponde
        //    if (!empresa.SoftwareHouse.Chave.Equals(chaveToken.ToString()))
        //    {
        //        return BadRequest("Chave incorreta para o CNPJ: " + cnpjFormatado + ": CORRESPONDENCIA INVÁLIDA");
        //    }

        //    /*VERIFICAÇÕES NOS DADOS DO JSON ENVIADO */
        //    foreach (ItemTributacaoJson item in itens)
        //    {
        //        //Cnpj incorreto: veio nullo
        //        if (item.CNPJ_EMPRESA == null)
        //        {
        //            return BadRequest("ITEM DO JSON SEM CNPJ DE EMPRESA!");
        //        }
        //        else //caso nao seja nulo
        //        {


        //            if (item.CNPJ_EMPRESA != cnpjFormatado) //verifica se é diferente ao formatado
        //            {
        //                if (item.CNPJ_EMPRESA != cnpj) //se for ele ainda verifica se é diferente do cnpj original
        //                {
        //                    //se ambos estiverem diferentes retorna o erro
        //                    return BadRequest("CNPJ DE ITEM NO JSON DIFERE DO CNPJ INFORMADO COMO PARAMETRO!");
        //                }

        //            }

        //        }



        //    } //fim foreach

        //    //contador auxiliar
        //    int cont = 0;
        //    int contAlterados = 0;
        //    int contProdSalvos = 0;
        //    int contRegistrosNulos = 0;

        //    //verificar o numero de intes, se forem nullo os itens do json vieram vazios
        //    if (itens == null)
        //    {
        //        _log.Debug("LOGGER DE JSON VAZIO OU CAMPO INVÁLIDO");
        //        return BadRequest("JSON VAZIO OU CAMPO INVÁLIDO!");
        //    }

        //    //SALVAR JSON PASTA UPLOADS - caso seja necessário recuperar esse arquivo json
        //    //try
        //    //{
        //    //    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(List<ItemTributacaoJson>));
        //    //    MemoryStream ms = new MemoryStream();
        //    //    ser.WriteObject(ms, itens);
        //    //    string jsonString = Encoding.UTF8.GetString(ms.ToArray());
        //    //    ms.Close();
        //    //    string path = System.Web.HttpContext.Current.Server.MapPath("~/Content/Uploads/objetomtx.json");
        //    //    System.IO.File.WriteAllText(path, jsonString);
        //    //}
        //    //catch
        //    //{
        //    //    throw;
        //    //}

        //    //lista com o objeto para retorno
        //    List<TributacaoEmpresa> listaSalvosTribEmpresa = new List<TributacaoEmpresa>();


        //    //laço para percorrer o objeto recebido e salvo no json
        //    foreach (ItemTributacaoJson item in itens)
        //    {
        //        codBarrasGerado = "";
        //        contRegistrosNulos = 0;

        //        //verifica o uf destino e origem
        //        if (item.UF_ORIGEM == null || item.UF_ORIGEM == "" || item.UF_DESTINO == null || item.UF_DESTINO == "")
        //        {
        //            auxEstado++;
        //        }
        //        else
        //        {

        //            //if (!VerificaString(item.UF_ORIGEM) || !VerificaString(item.UF_DESTINO))
        //            //{
        //            //    auxEstado++;
        //            //}
        //            //else
        //            //{

        //            //se o item retornado no campo codbarras for diferente de nullo ele entra
        //            if (item.PRODUTO_COD_BARRAS != null && item.PRODUTO_COD_BARRAS != "0")
        //            {

        //                //Vefificar o tamanho da string e retirando os espaços de inicio e fim 
        //                item.PRODUTO_COD_BARRAS = item.PRODUTO_COD_BARRAS.Trim();


        //                //Pegar o tamanho do codigo de barras enviado pelo json
        //                int tamanho = item.PRODUTO_COD_BARRAS.Length;


        //                //PEGA OS DESTINO
        //                string[] ufDestinoIni = item.UF_DESTINO.Split('|');

        //                /*VAI PASSAR POR TODOS OS DESTINOS VERIFICANDO SE EXISTE O PRODUTO LANÇADO NA TRIBUTAÇÃO NO CLIENTE*/

        //                //retira o elemento vazio do array
        //                ufDestinoIni = ufDestinoIni.Where(a => a != "").ToArray();

        //                //laço para percorrer todos os destinos informados no item
        //                for (int i = 0; i < ufDestinoIni.Count(); i++)
        //                {
        //                    string dest = ufDestinoIni[i].ToString();
        //                    //verifivar se o item está na tabela de cliente
        //                    /*Caso ele esteja na tabela de cliente, precisamos pegar o cod_de_barras_gerado pelo mtx pra prosseguir*/
        //                    TributacaoEmpresa tribEmpresas3 = db.TributacaoEmpresas.Where(x => x.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && x.CNPJ_EMPRESA.Contains(cnpjFormatado) && x.UF_ORIGEM.Equals(item.UF_ORIGEM) && x.UF_DESTINO.Equals(dest)).FirstOrDefault(); //verifica o cadastro



        //                    if (tribEmpresas3 != null)
        //                    {
        //                        //se for diferente de nula, quer dizer que ha tributacação para esse destino
        //                        if (tribEmpresas3.COD_BARRAS_GERADO != null)
        //                        {
        //                            codBarrasGerado = tribEmpresas3.COD_BARRAS_GERADO; //a variavel codigo de barras gerado vai receber esse valor do objejeto
        //                        }
        //                        else
        //                        {
        //                            /*Se ele for nulo,(o codigo de barras gerado do objeto) 
        //                             * ele tem que verificar se o tamanho do cod barras é maior que 7, se for ele so
        //                             * atribui ao codigo de barras gerado, se nao ele gera um novo, salva na tabela do cliente 
        //                             * e passa esse codigo gerado para frente para frente para
        //                            que o cadastro do produto tenha o mesmo codigo, igualando as referencias*/

        //                            if (tribEmpresas3.PRODUTO_COD_BARRAS.Count() > 7)
        //                            {
        //                                codBarrasGerado = tribEmpresas3.PRODUTO_COD_BARRAS.ToString();
        //                                TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
        //                                /*VERIFICAR TODOS OS DESTINO*/

        //                                itemSalvar = db.TributacaoEmpresas.Find(tribEmpresas3.ID);
        //                                itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;

        //                                db.SaveChanges(); //salva com o mesmo numero do codigo de barras, pois ele eh maior que 7
        //                            }
        //                            else
        //                            {
        //                                if (codBarrasGerado != "")
        //                                {
        //                                    TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
        //                                    /*VERIFICAR TODOS OS DESTINO*/

        //                                    itemSalvar = db.TributacaoEmpresas.Find(tribEmpresas3.ID);
        //                                    itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;

        //                                    db.SaveChanges();

        //                                }
        //                                else
        //                                {
        //                                    Random randNum = new Random();

        //                                    for (int ib = 0; ib < 1; ib++)
        //                                    {
        //                                        codBarrasGerado = (randNum.Next().ToString());
        //                                    }
        //                                    TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
        //                                    /*VERIFICAR TODOS OS DESTINO*/

        //                                    itemSalvar = db.TributacaoEmpresas.Find(tribEmpresas3.ID);
        //                                    itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;

        //                                    db.SaveChanges();

        //                                }

        //                            }


        //                        }

        //                    }
        //                    else
        //                    {
        //                        //se nesse destino vier nula, efetuar um contador para verificar se o codBArrasGerado foi atribuido algum valor
        //                        contRegistrosNulos++;
        //                    }





        //                } //FIM DO FOR
        //                  //apos o for, verificar o contador de registros nulos, se continuar 0 quer dizer que  foi encontardo registro na
        //                  //tabela do cliente, ou seja, NÃO devemos gerar um codigo de barras para atribuir ao valor do campo codigo de barras gerado
        //                if (contRegistrosNulos != 0)
        //                {
        //                    if (item.PRODUTO_COD_BARRAS.Count() <= 7) //se for menor ou igual a sete, devemos gerar um codigo de barras
        //                    {
        //                        Random randNum = new Random();

        //                        for (int ib = 0; ib < 1; ib++)
        //                        {
        //                            codBarrasGerado = (randNum.Next().ToString());
        //                        }
        //                    }
        //                    else
        //                    {
        //                        codBarrasGerado = item.PRODUTO_COD_BARRAS.ToString();
        //                    }
        //                }

        //                //condicional tamanho do codigo de barras
        //                if (tamanho > 7)
        //                {

        //                    //verificar se o produto ja foi importado
        //                    var tribEmpresas2 = from s in db.TributacaoEmpresas select s; //select na tabela

        //                    /*Implementar busca pela categoria e verificar se a categoria que vem do cliente
        //                     existe na tabela de categoria da matriz*/
        //                    //pegou o ID da categoria
        //                    var categoriaProd = (from ab in db.CategoriasProdutos where item.PRODUTO_CATEGORIA == ab.descricao select ab.id).FirstOrDefault();


        //                    //Se houver a categoria ele atribui ao item e continua, caso não tenha ele atribui nullo e continua
        //                    /*Isso se deve ao fato que o cliente pode haver mais categorias e/ou categorias diferentes
        //                     o que não é relevante para analise, por isso atribuimos nulla caso seja diferente ou inexistente
        //                    na tabela da matriz*/
        //                    if (categoriaProd > 0)
        //                    {
        //                        item.PRODUTO_CATEGORIA = categoriaProd.ToString();
        //                    }
        //                    else
        //                    {
        //                        item.PRODUTO_CATEGORIA = null;
        //                    }



        //                    /*ROTINA PARA VERIFICAR SE O PRODUTO ESTÁ CADASTRADO E TRIBUTADO NA TABELA MATRIZ*/
        //                    //  cadProd = cadProd.Where(s => s.codBarras.Equals(item.PRODUTO_COD_BARRAS) && item.PRODUTO_COD_BARRAS != "0").ToList();

        //                    long? prodItem = long.Parse(item.PRODUTO_COD_BARRAS);
        //                    /*TO-DO
        //                     * Essa busca deve ser melhorada, so pelo codigo de barras não é suficiente, uma vez
        //                     * que existem outros codigos de barras iguais cadastrados anteriormente*/

        //                    Produto cadProd = db.Produtos.Where(x => x.codBarras == prodItem && x.CodBarrasGErado.Equals(codBarrasGerado.ToString())).FirstOrDefault(); //verifica o cadastro

        //                    Produto prodSalvar = new Produto();


        //                    //se ele nao esta cadastrado na tabela de produto ele deve ser cadastrado nesta tabela
        //                    if (cadProd == null)
        //                    {

        //                        prodSalvar.codBarras = Int64.Parse(item.PRODUTO_COD_BARRAS);
        //                        prodSalvar.CodBarrasGErado = codBarrasGerado;
        //                        prodSalvar.descricao = item.PRODUTO_DESCRICAO;
        //                        prodSalvar.cest = item.PRODUTO_CEST;
        //                        prodSalvar.ncm = item.PRODUTO_NCM;

        //                        if (item.PRODUTO_CATEGORIA != null)
        //                        {
        //                            prodSalvar.idCategoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

        //                        }
        //                        else
        //                        {
        //                            prodSalvar.idCategoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
        //                        }

        //                        prodSalvar.status = 1;
        //                        prodSalvar.dataCad = DateTime.Now;
        //                        prodSalvar.dataAlt = DateTime.Now;
        //                        prodSalvar.auditadoNCM = 0; //nao auditado

        //                        //try-catch para salvar o produto na tabela
        //                        try
        //                        {

        //                            db.Produtos.Add(prodSalvar);//objeto para ser salvo no banco
        //                            db.SaveChanges();

        //                            contProdSalvos++;
        //                        }
        //                        catch (Exception e)
        //                        {
        //                            //erros e mensagens
        //                            if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                            {

        //                                _log.Error(e.InnerException.InnerException.Message);
        //                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                            }

        //                            if (e.Message != null)
        //                            {

        //                                _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                            }

        //                            return BadRequest("ERRO AO SALVAR PRODUTO");
        //                        }//fim do catch



        //                    }



        //                    //VERIFICAR SE HA TRIBUTAÇÃO PARA O PRODUTO DEPENDENDO DA EMPRESA (SIMPLES OU NORMAL)
        //                    if (empresa.simples_nacional == 1)
        //                    {
        //                        //se for simples nacional, o produto é tributado em simples nacional
        //                        //verificar o id do produto

        //                        if (cadProd == null)
        //                        {
        //                            cadProd = db.Produtos.Where(x => x.codBarras == prodItem).FirstOrDefault();
        //                        }

        //                        /*Salvar na tabela TributacaoNCM, caso nao exista*/
        //                        string prodItemNCM = item.PRODUTO_NCM; //PEGA O NCM DO ITEM


        //                        //BUSCA PELO NCM NA TABELA
        //                        //TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM).FirstOrDefault();

        //                        //Array de destino: PEGA TODOS OS DESTINOS QUE VIERAM NO JSON DO CLIENTE
        //                        string[] ufDestinoProd = item.UF_DESTINO.Split('|');

        //                        //retira o elemento vazio do array
        //                        ufDestinoProd = ufDestinoProd.Where(a => a != "").ToArray();

        //                        //verifica se é simples nacional realmente para o filtro
        //                        int simpN = int.Parse(empresa.simples_nacional.ToString());

        //                        //PASSAR PELOS DESTINOS PARA PROCURAR OS ITENS NA TABELA DE NCM - se faz necessario pois cada tributacao tem sua origem e destino
        //                        for (int i = 0; i < ufDestinoProd.Count(); i++)
        //                        {
        //                            string dest = ufDestinoProd[i].ToString();
        //                            //BUSCA PELO NCM NA TABELA
        //                            TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest && x.Simp_Nacional == simpN).FirstOrDefault();

        //                            if (tribnaNCM == null)
        //                            {
        //                                TributacaoNCM prodTribNCMSalvar = new TributacaoNCM();
        //                                if (item.PRODUTO_CATEGORIA != null)
        //                                {
        //                                    prodTribNCMSalvar.categoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

        //                                }
        //                                else
        //                                {
        //                                    prodTribNCMSalvar.categoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
        //                                }
        //                                prodTribNCMSalvar.UF_Origem = item.UF_ORIGEM;
        //                                prodTribNCMSalvar.UF_Destino = ufDestinoProd[i];
        //                                prodTribNCMSalvar.cest = item.PRODUTO_CEST;
        //                                prodTribNCMSalvar.ncm = item.PRODUTO_NCM;
        //                                prodTribNCMSalvar.auditadoPorNCM = 0;
        //                                prodTribNCMSalvar.Simp_Nacional = 1;
        //                                prodTribNCMSalvar.dataCad = DateTime.Now;
        //                                prodTribNCMSalvar.dataAlt = DateTime.Now;


        //                                try
        //                                {
        //                                    //salvar
        //                                    db.TributacaoNCM.Add(prodTribNCMSalvar);//objeto para ser salvo no banco
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch
        //                            }
        //                        }


        //                        //Verifica na tabela de tributação da matriz se existe esse produto tributado
        //                        //TributacaoSN tribProd = db.TributacaoSN.Where(x => x.idProduto == cadProd.Id).FirstOrDefault();

        //                        //DA MESMA FORMA, BUSCAR A TRIBUTACAO LEVANDO EM CONTA A ORIGEM E DESTINO
        //                        for (int i = 0; i < ufDestinoProd.Count(); i++)
        //                        {
        //                            string dest = ufDestinoProd[i].ToString();
        //                            //Verifica na tabela de tributação da matriz se existe esse produto tributado
        //                            TributacaoSN tribProd = db.TributacaoSN.Where(x => x.idProduto == cadProd.Id && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest).FirstOrDefault();
        //                            if (tribProd == null)
        //                            {
        //                                TributacaoSN prodTribSalvar = new TributacaoSN()
        //                                {

        //                                    UF_Origem = item.UF_ORIGEM,
        //                                    idProduto = cadProd.Id,
        //                                    idSetor = 91,
        //                                    dataCad = DateTime.Now,
        //                                    dataAlt = DateTime.Now,
        //                                    UF_Destino = ufDestinoProd[i],
        //                                    auditadoPorNCM = 0


        //                                };
        //                                try
        //                                {
        //                                    //salvar
        //                                    db.TributacaoSN.Add(prodTribSalvar);//objeto para ser salvo no banco
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch
        //                            }
        //                        }



        //                    }
        //                    else
        //                    {
        //                        //caso contrario tributado na tabela normal
        //                        //se não for simples nacional, o produto é tributado em tributacao_produtos
        //                        //verificar o id do produto: SE FOR NULO ELE BUSCA NO BANCO, SE NAO FOR ELE PASSA E USA O MESMO QUE FOI ATRIBUIDO ANTERIORMENTE
        //                        string[] ufDestinoTaNCM = item.UF_DESTINO.Split('|'); //pega os uf destino que veio no json

        //                        //retira o elemento vazio do array deixando somente os id dos registros
        //                        ufDestinoTaNCM = ufDestinoTaNCM.Where(a => a != "").ToArray();



        //                        if (cadProd == null)
        //                        {
        //                            cadProd = db.Produtos.Where(x => x.codBarras == prodItem).FirstOrDefault();
        //                        }
        //                        /*Salvar na tabela TributacaoNCM, caso nao exista*/
        //                        string prodItemNCM = item.PRODUTO_NCM; //PEGA O NCM DO ITEM



        //                        //BUSCA PELO NCM NA TABELA
        //                        //TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM).FirstOrDefault();

        //                        //verifica se é simples nacional realmente para o filtro
        //                        int simpN = int.Parse(empresa.simples_nacional.ToString());

        //                        //verificar se ha item na tabela de ncm pelo destino
        //                        for (int i = 0; i < ufDestinoTaNCM.Count(); i++)
        //                        {
        //                            string dest = ufDestinoTaNCM[i].ToString();
        //                            //BUSCA PELO NCM NA TABELA
        //                            TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest && x.Simp_Nacional == simpN).FirstOrDefault();

        //                            if (tribnaNCM == null)
        //                            {
        //                                TributacaoNCM prodTribNCMSalvar = new TributacaoNCM();
        //                                if (item.PRODUTO_CATEGORIA != null)
        //                                {
        //                                    prodTribNCMSalvar.categoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

        //                                }
        //                                else
        //                                {
        //                                    prodTribNCMSalvar.categoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
        //                                }


        //                                prodTribNCMSalvar.UF_Origem = item.UF_ORIGEM;
        //                                prodTribNCMSalvar.UF_Destino = ufDestinoTaNCM[i];
        //                                prodTribNCMSalvar.cest = item.PRODUTO_CEST;
        //                                prodTribNCMSalvar.ncm = item.PRODUTO_NCM;
        //                                prodTribNCMSalvar.auditadoPorNCM = 0;
        //                                prodTribNCMSalvar.Simp_Nacional = 0;
        //                                prodTribNCMSalvar.dataCad = DateTime.Now;
        //                                prodTribNCMSalvar.dataAlt = DateTime.Now;
        //                                try
        //                                {
        //                                    //salvar
        //                                    db.TributacaoNCM.Add(prodTribNCMSalvar);//objeto para ser salvo no banco
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch
        //                            }

        //                        }




        //                        //verificar na tabela de tributações se o produto esta tributado
        //                        //Tributacao tribProd = db.Tributacoes.Where(x => x.idProduto == cadProd.Id).FirstOrDefault();

        //                        for (int i = 0; i < ufDestinoTaNCM.Count(); i++)
        //                        {
        //                            string dest = ufDestinoTaNCM[i].ToString();
        //                            Tributacao tribProd = db.Tributacoes.Where(x => x.idProduto == cadProd.Id && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest).FirstOrDefault();
        //                            if (tribProd == null)
        //                            {
        //                                Tributacao prodTribSalvar = new Tributacao()
        //                                {

        //                                    UF_Origem = item.UF_ORIGEM,
        //                                    idProduto = cadProd.Id,
        //                                    idSetor = 91,
        //                                    dataCad = DateTime.Now,
        //                                    dataAlt = DateTime.Now,
        //                                    UF_Destino = ufDestinoTaNCM[i],
        //                                    auditadoPorNCM = 0

        //                                };

        //                                try
        //                                {

        //                                    db.Tributacoes.Add(prodTribSalvar);//objeto para ser salvo no banco
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch
        //                            }
        //                        }



        //                    }


        //                    //contar os que vieram com codigo de barras 0
        //                    if (item.PRODUTO_COD_BARRAS == "0")
        //                    {
        //                        prodZerado++;
        //                    }


        //                    //Verificar em todos os destinos se o item foi tributado no cliente
        //                    string[] ufDestino = item.UF_DESTINO.Split('|');

        //                    //retira o elemento vazio do array deixando somente os id dos registros
        //                    ufDestino = ufDestino.Where(a => a != "").ToArray();
        //                    for (int i = 0; i < ufDestino.Count(); i++)
        //                    {
        //                        string dest = ufDestino[i].ToString();
        //                        //where: where com o codigo de barras do produto e cnpj
        //                        /*aqui ele verifica se o produto ja contem no cnpj informado*/
        //                        tribEmpresas2 = tribEmpresas2.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest));

        //                        //se vier algo da consulta acima
        //                        if (tribEmpresas2.Count() <= 0 && item.PRODUTO_COD_BARRAS != "0")
        //                        {
        //                            TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
        //                            //atribunido dados ao objeto
        //                            itemSalvar.CNPJ_EMPRESA = empresa.cnpj;
        //                            itemSalvar.PRODUTO_COD_BARRAS = item.PRODUTO_COD_BARRAS;
        //                            itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;
        //                            itemSalvar.PRODUTO_DESCRICAO = item.PRODUTO_DESCRICAO;
        //                            itemSalvar.PRODUTO_CEST = item.PRODUTO_CEST;
        //                            itemSalvar.PRODUTO_NCM = item.PRODUTO_NCM;
        //                            itemSalvar.PRODUTO_CATEGORIA = item.PRODUTO_CATEGORIA;/*Ponto a analisar, pois vem do cliente descrição*/
        //                            itemSalvar.FECP = item.FECP;
        //                            itemSalvar.COD_NAT_RECEITA = item.COD_NAT_RECEITA;
        //                            itemSalvar.CST_ENTRADA_PIS_COFINS = item.CST_ENTRADA_PIS_COFINS;
        //                            itemSalvar.CST_SAIDA_PIS_COFINS = item.CST_SAIDA_PIS_COFINS;
        //                            itemSalvar.ALIQ_ENTRADA_PIS = item.ALIQ_ENTRADA_PIS;
        //                            itemSalvar.ALIQ_SAIDA_PIS = item.ALIQ_ENTRADA_PIS;
        //                            itemSalvar.ALIQ_ENTRADA_COFINS = item.ALIQ_ENTRADA_COFINS;
        //                            itemSalvar.ALIQ_SAIDA_COFINS = item.ALIQ_SAIDA_COFINS;
        //                            itemSalvar.CST_VENDA_ATA = item.CST_VENDA_ATA;
        //                            itemSalvar.ALIQ_ICMS_VENDA_ATA = item.ALIQ_ICMS_VENDA_ATA;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_ATA = item.ALIQ_ICMS_ST_VENDA_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA = item.RED_BASE_CALC_ICMS_VENDA_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA = item.RED_BASE_CALC_ICMS_ST_VENDA_ATA;
        //                            itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL = item.CST_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.CST_VENDA_VAREJO_CONT = item.CST_VENDA_VAREJO_CONT;
        //                            itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT = item.ALIQ_ICMS_VENDA_VAREJO_CONT;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONT = item.ALIQ_ICMS_ST_VENDA_VAREJO_CONT;
        //                            itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT = item.RED_BASE_CALC_VENDA_VAREJO_CONT;
        //                            itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;
        //                            itemSalvar.CST_VENDA_VAREJO_CONS_FINAL = item.CST_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.CST_COMPRA_DE_IND = item.CST_COMPRA_DE_IND;
        //                            itemSalvar.ALIQ_ICMS_COMP_DE_IND = item.ALIQ_ICMS_COMP_DE_IND;
        //                            itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND = item.ALIQ_ICMS_ST_COMP_DE_IND;
        //                            itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND = item.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;
        //                            itemSalvar.CST_COMPRA_DE_ATA = item.CST_COMPRA_DE_ATA;
        //                            itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA = item.ALIQ_ICMS_COMPRA_DE_ATA;
        //                            itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA = item.ALIQ_ICMS_ST_COMPRA_DE_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;
        //                            itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL = item.CST_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.CST_DA_NFE_DA_IND_FORN = item.CST_DA_NFE_DA_IND_FORN;
        //                            itemSalvar.CST_DA_NFE_DE_ATA_FORN = item.CST_DA_NFE_DE_ATA_FORN;
        //                            itemSalvar.CSOSNT_DANFE_DOS_NFOR = item.CSOSNT_DANFE_DOS_NFOR;
        //                            itemSalvar.ALIQ_ICMS_NFE = item.ALIQ_ICMS_NFE;
        //                            itemSalvar.ALIQ_ICMS_NFE_FOR_ATA = item.ALIQ_ICMS_NFE_FOR_ATA;
        //                            itemSalvar.ALIQ_ICMS_NFE_FOR_SN = item.ALIQ_ICMS_NFE_FOR_SN;
        //                            itemSalvar.TIPO_MVA = item.TIPO_MVA;
        //                            itemSalvar.VALOR_MVA_IND = item.VALOR_MVA_IND;
        //                            itemSalvar.INICIO_VIGENCIA_MVA = item.INICIO_VIGENCIA_MVA; //data
        //                            itemSalvar.FIM_VIGENCIA_MVA = item.FIM_VIGENCIA_MVA; //data
        //                            itemSalvar.CREDITO_OUTORGADO = item.CREDITO_OUTORGADO;
        //                            itemSalvar.VALOR_MVA_ATACADO = item.VALOR_MVA_ATACADO;
        //                            itemSalvar.REGIME_2560 = item.REGIME_2560;
        //                            itemSalvar.UF_ORIGEM = item.UF_ORIGEM;
        //                            itemSalvar.UF_DESTINO = ufDestino[i];
        //                            itemSalvar.PRODUTO_COD_INTERNO = item.PRODUTO_COD_INTERNO;
        //                            //data da inclusão/alteração
        //                            itemSalvar.DT_ALTERACAO = DateTime.Now;


        //                            //Verifica se o item veio ativo, caso venha null considera ativo
        //                            if (item.ATIVO == null)
        //                            {
        //                                itemSalvar.ATIVO = 1;
        //                            }
        //                            else
        //                            {
        //                                itemSalvar.ATIVO = sbyte.Parse(item.ATIVO);
        //                            }



        //                            //try catch para salvar no banco e na lista de retorno
        //                            try
        //                            {

        //                                db.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco
        //                                bd.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco de comparação
        //                                listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno


        //                                cont++;
        //                            }
        //                            catch (Exception e)
        //                            {
        //                                //erros e mensagens
        //                                if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                {

        //                                    _log.Error(e.InnerException.InnerException.Message);
        //                                    return BadRequest("ERRO AO SALVAR ITEM: " + e.InnerException.InnerException.Message);
        //                                }

        //                                if (e.Message != null)
        //                                {

        //                                    _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                    return BadRequest("ERRO AO SALVAR ITEM: " + e.Message);
        //                                }

        //                                return BadRequest("ERRO AO SALVAR ITEM");
        //                            }//fim do catch

        //                        }//se o produto existir, ele deve ser alterado
        //                        else
        //                        {

        //                            //se o codigo de barras não foi importado o entra na condição, ou seja o retorno do tribempresas2 é 0
        //                            //sendo zero o produto nao foi importado, agora ele será com todos os seus dados
        //                            //alteração 16092021->alem de nao ter encontrado nada no banco, count=0 o codigo de barras deve ser diferente de 0(zero)
        //                            //pegar o id desse registro
        //                            var idDoRegistros = db.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID).FirstOrDefault();

        //                            //var idRegistros = (from ab in db.TributacaoEmpresas where item.PRODUTO_COD_BARRAS == ab.PRODUTO_COD_BARRAS && ab.CNPJ_EMPRESA.Contains(cnpjFormatado) && ab.UF_ORIGEM.Equals(item.UF_ORIGEM) && ab.UF_DESTINO.Equals(dest) select ab.ID).FirstOrDefault();
        //                            if (idDoRegistros != 0)
        //                            {
        //                                TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
        //                                itemSalvar = db.TributacaoEmpresas.Find(idDoRegistros);

        //                                itemSalvar.PRODUTO_DESCRICAO = (itemSalvar.PRODUTO_DESCRICAO != item.PRODUTO_DESCRICAO) ? ((item.PRODUTO_DESCRICAO != null) ? item.PRODUTO_DESCRICAO : itemSalvar.PRODUTO_DESCRICAO) : itemSalvar.PRODUTO_DESCRICAO;
        //                                itemSalvar.PRODUTO_CEST = (itemSalvar.PRODUTO_CEST != item.PRODUTO_CEST) ? ((item.PRODUTO_CEST != null) ? item.PRODUTO_CEST : itemSalvar.PRODUTO_CEST) : itemSalvar.PRODUTO_CEST;
        //                                itemSalvar.PRODUTO_NCM = (itemSalvar.PRODUTO_NCM != item.PRODUTO_NCM) ? ((item.PRODUTO_NCM != null) ? item.PRODUTO_NCM : itemSalvar.PRODUTO_NCM) : itemSalvar.PRODUTO_NCM;
        //                                itemSalvar.PRODUTO_CATEGORIA = (itemSalvar.PRODUTO_CATEGORIA != item.PRODUTO_CATEGORIA) ? ((item.PRODUTO_CATEGORIA != null) ? item.PRODUTO_CATEGORIA : itemSalvar.PRODUTO_CATEGORIA) : itemSalvar.PRODUTO_CATEGORIA;
        //                                itemSalvar.FECP = (itemSalvar.FECP != item.FECP) ? ((item.FECP != null) ? item.FECP : itemSalvar.FECP) : itemSalvar.FECP;
        //                                itemSalvar.COD_NAT_RECEITA = (itemSalvar.COD_NAT_RECEITA != item.COD_NAT_RECEITA) ? ((item.COD_NAT_RECEITA != null) ? item.COD_NAT_RECEITA : itemSalvar.COD_NAT_RECEITA) : itemSalvar.COD_NAT_RECEITA;

        //                                itemSalvar.CST_ENTRADA_PIS_COFINS = (itemSalvar.CST_ENTRADA_PIS_COFINS != item.CST_ENTRADA_PIS_COFINS) ? ((item.CST_ENTRADA_PIS_COFINS != null) ? item.CST_ENTRADA_PIS_COFINS : itemSalvar.CST_ENTRADA_PIS_COFINS) : itemSalvar.CST_ENTRADA_PIS_COFINS;
        //                                itemSalvar.CST_SAIDA_PIS_COFINS = (itemSalvar.CST_SAIDA_PIS_COFINS != item.CST_SAIDA_PIS_COFINS) ? ((item.CST_SAIDA_PIS_COFINS != null) ? item.CST_SAIDA_PIS_COFINS : itemSalvar.CST_SAIDA_PIS_COFINS) : itemSalvar.CST_SAIDA_PIS_COFINS;
        //                                itemSalvar.ALIQ_ENTRADA_PIS = (itemSalvar.ALIQ_ENTRADA_PIS != item.ALIQ_ENTRADA_PIS) ? ((item.ALIQ_ENTRADA_PIS != null) ? item.ALIQ_ENTRADA_PIS : itemSalvar.ALIQ_ENTRADA_PIS) : itemSalvar.ALIQ_ENTRADA_PIS;
        //                                itemSalvar.ALIQ_SAIDA_PIS = (itemSalvar.ALIQ_SAIDA_PIS != item.ALIQ_SAIDA_PIS) ? ((item.ALIQ_SAIDA_PIS != null) ? item.ALIQ_SAIDA_PIS : itemSalvar.ALIQ_SAIDA_PIS) : itemSalvar.ALIQ_SAIDA_PIS;
        //                                itemSalvar.ALIQ_ENTRADA_COFINS = (itemSalvar.ALIQ_ENTRADA_COFINS != item.ALIQ_ENTRADA_COFINS) ? ((item.ALIQ_ENTRADA_COFINS != null) ? item.ALIQ_ENTRADA_COFINS : itemSalvar.ALIQ_ENTRADA_COFINS) : itemSalvar.ALIQ_ENTRADA_COFINS;
        //                                itemSalvar.ALIQ_SAIDA_COFINS = (itemSalvar.ALIQ_SAIDA_COFINS != item.ALIQ_SAIDA_COFINS) ? ((item.ALIQ_SAIDA_COFINS != null) ? item.ALIQ_SAIDA_COFINS : itemSalvar.ALIQ_SAIDA_COFINS) : itemSalvar.ALIQ_SAIDA_COFINS;

        //                                itemSalvar.CST_VENDA_ATA = (itemSalvar.CST_VENDA_ATA != item.CST_VENDA_ATA) ? ((item.CST_VENDA_ATA != null) ? item.CST_VENDA_ATA : itemSalvar.CST_VENDA_ATA) : itemSalvar.CST_VENDA_ATA;
        //                                itemSalvar.ALIQ_ICMS_VENDA_ATA = (itemSalvar.ALIQ_ICMS_VENDA_ATA != item.ALIQ_ICMS_VENDA_ATA) ? ((item.ALIQ_ICMS_VENDA_ATA != null) ? item.ALIQ_ICMS_VENDA_ATA : itemSalvar.ALIQ_ICMS_VENDA_ATA) : itemSalvar.ALIQ_ICMS_VENDA_ATA;
        //                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA != item.ALIQ_ICMS_ST_VENDA_ATA) ? ((item.ALIQ_ICMS_ST_VENDA_ATA != null) ? item.ALIQ_ICMS_ST_VENDA_ATA : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA != item.RED_BASE_CALC_ICMS_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA;

        //                                itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL != item.CST_VENDA_ATA_SIMP_NACIONAL) ? ((item.CST_VENDA_ATA_SIMP_NACIONAL != null) ? item.CST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;


        //                                itemSalvar.CST_VENDA_VAREJO_CONT = (itemSalvar.CST_VENDA_VAREJO_CONT != item.CST_VENDA_VAREJO_CONT) ? ((item.CST_VENDA_VAREJO_CONT != null) ? item.CST_VENDA_VAREJO_CONT : itemSalvar.CST_VENDA_VAREJO_CONT) : itemSalvar.CST_VENDA_VAREJO_CONT;
        //                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT != item.ALIQ_ICMS_VENDA_VAREJO_CONT) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONT != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONT : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT;
        //                                itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT != item.RED_BASE_CALC_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT;
        //                                itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;


        //                                itemSalvar.CST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.CST_VENDA_VAREJO_CONS_FINAL != item.CST_VENDA_VAREJO_CONS_FINAL) ? ((item.CST_VENDA_VAREJO_CONS_FINAL != null) ? item.CST_VENDA_VAREJO_CONS_FINAL : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;


        //                                itemSalvar.CST_COMPRA_DE_IND = (itemSalvar.CST_COMPRA_DE_IND != item.CST_COMPRA_DE_IND) ? ((item.CST_COMPRA_DE_IND != null) ? item.CST_COMPRA_DE_IND : itemSalvar.CST_COMPRA_DE_IND) : itemSalvar.CST_COMPRA_DE_IND;
        //                                itemSalvar.ALIQ_ICMS_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_COMP_DE_IND != item.ALIQ_ICMS_COMP_DE_IND) ? ((item.ALIQ_ICMS_COMP_DE_IND != null) ? item.ALIQ_ICMS_COMP_DE_IND : itemSalvar.ALIQ_ICMS_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_COMP_DE_IND;
        //                                itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND != item.ALIQ_ICMS_ST_COMP_DE_IND) ? ((item.ALIQ_ICMS_ST_COMP_DE_IND != null) ? item.ALIQ_ICMS_ST_COMP_DE_IND : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND;
        //                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;

        //                                itemSalvar.CST_COMPRA_DE_ATA = (itemSalvar.CST_COMPRA_DE_ATA != item.CST_COMPRA_DE_ATA) ? ((item.CST_COMPRA_DE_ATA != null) ? item.CST_COMPRA_DE_ATA : itemSalvar.CST_COMPRA_DE_ATA) : itemSalvar.CST_COMPRA_DE_ATA;
        //                                itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA != item.ALIQ_ICMS_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA;
        //                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA != item.ALIQ_ICMS_ST_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;

        //                                itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL != item.CST_COMPRA_DE_SIMP_NACIONAL) ? ((item.CST_COMPRA_DE_SIMP_NACIONAL != null) ? item.CST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;


        //                                itemSalvar.CST_DA_NFE_DA_IND_FORN = (itemSalvar.CST_DA_NFE_DA_IND_FORN != item.CST_DA_NFE_DA_IND_FORN) ? ((item.CST_DA_NFE_DA_IND_FORN != null) ? item.CST_DA_NFE_DA_IND_FORN : itemSalvar.CST_DA_NFE_DA_IND_FORN) : itemSalvar.CST_DA_NFE_DA_IND_FORN;
        //                                itemSalvar.CST_DA_NFE_DE_ATA_FORN = (itemSalvar.CST_DA_NFE_DE_ATA_FORN != item.CST_DA_NFE_DE_ATA_FORN) ? ((item.CST_DA_NFE_DE_ATA_FORN != null) ? item.CST_DA_NFE_DE_ATA_FORN : itemSalvar.CST_DA_NFE_DE_ATA_FORN) : itemSalvar.CST_DA_NFE_DE_ATA_FORN;
        //                                itemSalvar.CSOSNT_DANFE_DOS_NFOR = (itemSalvar.CSOSNT_DANFE_DOS_NFOR != item.CSOSNT_DANFE_DOS_NFOR) ? ((item.CSOSNT_DANFE_DOS_NFOR != null) ? item.CSOSNT_DANFE_DOS_NFOR : itemSalvar.CSOSNT_DANFE_DOS_NFOR) : itemSalvar.CSOSNT_DANFE_DOS_NFOR;

        //                                itemSalvar.ALIQ_ICMS_NFE = (itemSalvar.ALIQ_ICMS_NFE != item.ALIQ_ICMS_NFE) ? ((item.ALIQ_ICMS_NFE != null) ? item.ALIQ_ICMS_NFE : itemSalvar.ALIQ_ICMS_NFE) : itemSalvar.ALIQ_ICMS_NFE;
        //                                itemSalvar.ALIQ_ICMS_NFE_FOR_ATA = (itemSalvar.ALIQ_ICMS_NFE_FOR_ATA != item.ALIQ_ICMS_NFE_FOR_ATA) ? ((item.ALIQ_ICMS_NFE_FOR_ATA != null) ? item.ALIQ_ICMS_NFE_FOR_ATA : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA) : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA;
        //                                itemSalvar.ALIQ_ICMS_NFE_FOR_SN = (itemSalvar.ALIQ_ICMS_NFE_FOR_SN != item.ALIQ_ICMS_NFE_FOR_SN) ? ((item.ALIQ_ICMS_NFE_FOR_SN != null) ? item.ALIQ_ICMS_NFE_FOR_SN : itemSalvar.ALIQ_ICMS_NFE_FOR_SN) : itemSalvar.ALIQ_ICMS_NFE_FOR_SN;


        //                                itemSalvar.TIPO_MVA = (itemSalvar.TIPO_MVA != item.TIPO_MVA) ? ((item.TIPO_MVA != null) ? item.TIPO_MVA : itemSalvar.TIPO_MVA) : itemSalvar.TIPO_MVA;

        //                                itemSalvar.VALOR_MVA_IND = (itemSalvar.VALOR_MVA_IND != item.VALOR_MVA_IND) ? ((item.VALOR_MVA_IND != null) ? item.VALOR_MVA_IND : itemSalvar.VALOR_MVA_IND) : itemSalvar.VALOR_MVA_IND;

        //                                itemSalvar.INICIO_VIGENCIA_MVA = (itemSalvar.INICIO_VIGENCIA_MVA != item.INICIO_VIGENCIA_MVA) ? ((item.INICIO_VIGENCIA_MVA != null) ? item.INICIO_VIGENCIA_MVA : itemSalvar.INICIO_VIGENCIA_MVA) : itemSalvar.INICIO_VIGENCIA_MVA;

        //                                itemSalvar.FIM_VIGENCIA_MVA = (itemSalvar.FIM_VIGENCIA_MVA != item.FIM_VIGENCIA_MVA) ? ((item.FIM_VIGENCIA_MVA != null) ? item.FIM_VIGENCIA_MVA : itemSalvar.FIM_VIGENCIA_MVA) : itemSalvar.FIM_VIGENCIA_MVA;

        //                                itemSalvar.CREDITO_OUTORGADO = (itemSalvar.CREDITO_OUTORGADO != item.CREDITO_OUTORGADO) ? ((item.CREDITO_OUTORGADO != null) ? item.CREDITO_OUTORGADO : itemSalvar.CREDITO_OUTORGADO) : itemSalvar.CREDITO_OUTORGADO;

        //                                itemSalvar.VALOR_MVA_ATACADO = (itemSalvar.VALOR_MVA_ATACADO != item.VALOR_MVA_ATACADO) ? ((item.VALOR_MVA_ATACADO != null) ? item.VALOR_MVA_ATACADO : itemSalvar.VALOR_MVA_ATACADO) : itemSalvar.VALOR_MVA_ATACADO;

        //                                itemSalvar.REGIME_2560 = (itemSalvar.REGIME_2560 != item.REGIME_2560) ? ((item.REGIME_2560 != null) ? item.REGIME_2560 : itemSalvar.REGIME_2560) : itemSalvar.REGIME_2560;

        //                                itemSalvar.UF_ORIGEM = (itemSalvar.UF_ORIGEM != item.UF_ORIGEM) ? ((item.UF_ORIGEM != null) ? item.UF_ORIGEM : itemSalvar.UF_ORIGEM) : itemSalvar.UF_ORIGEM;

        //                                itemSalvar.UF_DESTINO = (itemSalvar.UF_DESTINO != ufDestino[i]) ? ((item.UF_DESTINO != null) ? item.UF_DESTINO : ufDestino[i]) : itemSalvar.UF_DESTINO;

        //                                //data da inclusão/alteração
        //                                itemSalvar.DT_ALTERACAO = DateTime.Now;
        //                                //try catch para salvar no banco e na lista de retorno
        //                                try
        //                                {

        //                                    //db.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco
        //                                    listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
        //                                    contAlterados++;
        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR ITEM: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR ITEM: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR ITEM");
        //                                }//fim do catch

        //                            }//fim do else caso o produto ja esteja cadastrado
        //                        }

        //                    }


        //                } //se o codigo de barras for > 7
        //                else
        //                {
        //                    //se for menor que sete
        //                    //instanciando objeto para salvar os dados recebidos no json
        //                    //TributacaoEmpresa itemSalvar = new TributacaoEmpresa();



        //                    //verificar se o produto ja foi importado
        //                    //var tribEmpresas2 = from s in db.TributacaoEmpresas select s; //select na tabela



        //                    /*Implementar busca pela categoria e verificar se a categoria que vem do cliente
        //                     existe na tabela de categoria da matriz*/
        //                    //pegou o ID da categoria
        //                    var categoriaProd = (from ab in db.CategoriasProdutos where item.PRODUTO_CATEGORIA == ab.descricao select ab.id).FirstOrDefault();


        //                    //Se houver a categoria ele atribui ao item e continua, caso não tenha ele atribui nullo e continua
        //                    /*Isso se deve ao fato que o cliente pode haver mais categorias e/ou categorias diferentes
        //                     o que não é relevante para analise, por isso atribuimos nulla caso seja diferente ou inexistente
        //                    na tabela da matriz*/
        //                    if (categoriaProd > 0)
        //                    {
        //                        item.PRODUTO_CATEGORIA = categoriaProd.ToString();
        //                    }
        //                    else
        //                    {
        //                        item.PRODUTO_CATEGORIA = null;
        //                    }



        //                    /*ROTINA PARA VERIFICAR SE O PRODUTO ESTÁ CADASTRADO E TRIBUTADO NA TABELA MATRIZ*/
        //                    //  cadProd = cadProd.Where(s => s.codBarras.Equals(item.PRODUTO_COD_BARRAS) && item.PRODUTO_COD_BARRAS != "0").ToList();

        //                    long? prodItem = long.Parse(item.PRODUTO_COD_BARRAS); //passa para long

        //                    Produto cadProd = db.Produtos.Where(x => x.codBarras == prodItem && x.CodBarrasGErado.Equals(codBarrasGerado.ToString())).FirstOrDefault(); //verifica o cadastro


        //                    Produto prodSalvar = new Produto();



        //                    //se ele nao esta cadastrado na tabela de produto ele deve ser cadastrado nesta tabela
        //                    if (cadProd == null)
        //                    {

        //                        prodSalvar.codBarras = Int64.Parse(item.PRODUTO_COD_BARRAS);
        //                        prodSalvar.CodBarrasGErado = codBarrasGerado;
        //                        prodSalvar.descricao = item.PRODUTO_DESCRICAO;
        //                        prodSalvar.cest = item.PRODUTO_CEST;
        //                        prodSalvar.ncm = item.PRODUTO_NCM;

        //                        if (item.PRODUTO_CATEGORIA != null)
        //                        {
        //                            prodSalvar.idCategoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

        //                        }
        //                        else
        //                        {
        //                            prodSalvar.idCategoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
        //                        }

        //                        prodSalvar.status = 1;
        //                        prodSalvar.dataCad = DateTime.Now;
        //                        prodSalvar.dataAlt = DateTime.Now;
        //                        prodSalvar.auditadoNCM = 0; //nao auditado

        //                        //try-catch para salvar o produto na tabela
        //                        try
        //                        {

        //                            db.Produtos.Add(prodSalvar);//objeto para ser salvo no banco
        //                            bd.Produtos.Add(prodSalvar);//objeto para ser salvo no banco de comparação
        //                            db.SaveChanges();

        //                            contProdSalvos++;
        //                        }
        //                        catch (Exception e)
        //                        {
        //                            //erros e mensagens
        //                            if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                            {

        //                                _log.Error(e.InnerException.InnerException.Message);
        //                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                            }

        //                            if (e.Message != null)
        //                            {

        //                                _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                            }

        //                            return BadRequest("ERRO AO SALVAR PRODUTO");
        //                        }//fim do catch



        //                    }



        //                    //VERIFICAR SE HA TRIBUTAÇÃO PARA O PRODUTO DEPENDENDO DA EMPRESA (SIMPLES OU NORMAL)
        //                    if (empresa.simples_nacional == 1)
        //                    {
        //                        //se for simples nacional, o produto é tributado em simples nacional
        //                        //verificar o id do produto

        //                        if (cadProd == null)
        //                        {
        //                            cadProd = db.Produtos.Where(x => x.codBarras == prodItem && x.CodBarrasGErado == codBarrasGerado).FirstOrDefault();
        //                        }

        //                        //id do produto


        //                        // produnico.Id = (from ab in db.Produtos where ab.codBarras.Equals(item.PRODUTO_COD_BARRAS) select ab.Id).FirstOrDefault();
        //                        // produnico.Id = db.Produtos.Where(x => x.codBarras.Equals(item.PRODUTO_COD_BARRAS)).Select(x => x.Id).FirstOrDefault();
        //                        /*Salvar na tabela TributacaoNCM, caso nao exista*/
        //                        string prodItemNCM = item.PRODUTO_NCM; //PEGA O NCM DO ITEM

        //                        //BUSCA PELO NCM NA TABELA
        //                        //TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM).FirstOrDefault();


        //                        //TributacaoSN tribProd = db.TributacaoSN.Where(x => x.idProduto == cadProd.Id).FirstOrDefault();

        //                        string[] ufDestinoTNCM = item.UF_DESTINO.Split('|');

        //                        //verifica se é simples nacional realmente para o filtro
        //                        int simpN = int.Parse(empresa.simples_nacional.ToString());


        //                        //retira o elemento vazio do array
        //                        ufDestinoTNCM = ufDestinoTNCM.Where(a => a != "").ToArray();

        //                        //PASSAR PELOS DESTINOS PARA PROCURAR OS ITENS NA TABELA DE NCM - se faz necessario pois cada tributacao tem sua origem e destino
        //                        for (int i = 0; i < ufDestinoTNCM.Count(); i++)
        //                        {
        //                            string dest = ufDestinoTNCM[i].ToString();
        //                            //BUSCA PELO NCM NA TABELA
        //                            TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest && x.Simp_Nacional == simpN).FirstOrDefault();
        //                            if (tribnaNCM == null)
        //                            {

        //                                TributacaoNCM prodTribNCMSalvar = new TributacaoNCM();

        //                                if (item.PRODUTO_CATEGORIA != null)
        //                                {
        //                                    prodTribNCMSalvar.categoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

        //                                }
        //                                else
        //                                {
        //                                    prodTribNCMSalvar.categoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
        //                                }


        //                                prodTribNCMSalvar.UF_Origem = item.UF_ORIGEM;
        //                                prodTribNCMSalvar.UF_Destino = ufDestinoTNCM[i];
        //                                prodTribNCMSalvar.cest = item.PRODUTO_CEST;
        //                                prodTribNCMSalvar.ncm = item.PRODUTO_NCM;
        //                                prodTribNCMSalvar.auditadoPorNCM = 0;
        //                                prodTribNCMSalvar.Simp_Nacional = 1;
        //                                prodTribNCMSalvar.dataCad = DateTime.Now;
        //                                prodTribNCMSalvar.dataAlt = DateTime.Now;

        //                                try
        //                                {

        //                                    db.TributacaoNCM.Add(prodTribNCMSalvar);//objeto para ser salvo no banco
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch

        //                            }

        //                        }



        //                        for (int i = 0; i < ufDestinoTNCM.Count(); i++)
        //                        {
        //                            string dest = ufDestinoTNCM[i].ToString();

        //                            TributacaoSN tribProd = db.TributacaoSN.Where(x => x.idProduto == cadProd.Id && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest).FirstOrDefault();

        //                            if (tribProd == null)
        //                            {
        //                                TributacaoSN prodTribSalvar = new TributacaoSN()
        //                                {

        //                                    UF_Origem = item.UF_ORIGEM,
        //                                    idProduto = cadProd.Id,
        //                                    idSetor = 91,
        //                                    dataCad = DateTime.Now,
        //                                    dataAlt = DateTime.Now,
        //                                    UF_Destino = ufDestinoTNCM[i],
        //                                    auditadoPorNCM = 0

        //                                };
        //                                try
        //                                {

        //                                    db.TributacaoSN.Add(prodTribSalvar);//objeto para ser salvo no banco
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch

        //                            }
        //                        }
        //                        ////caso nao haja a tributação
        //                        //if (tribProd == null)
        //                        //{
        //                        //    for (int i = 0; i < ufDestino.Count(); i++)
        //                        //    {
        //                        //        TributacaoSN prodTribSalvar = new TributacaoSN()
        //                        //        {

        //                        //            UF_Origem = item.UF_ORIGEM,
        //                        //            idProduto = cadProd.Id,
        //                        //            idSetor = 91,
        //                        //            dataCad = DateTime.Now,
        //                        //            dataAlt = DateTime.Now,
        //                        //            UF_Destino = ufDestino[i],
        //                        //            auditadoPorNCM = 0

        //                        //        };
        //                        //        try
        //                        //        {

        //                        //            db.TributacaoSN.Add(prodTribSalvar);//objeto para ser salvo no banco
        //                        //            bd.TributacaoSN.Add(prodTribSalvar);//objeto para ser salvo no banco de comparação
        //                        //            db.SaveChanges();

        //                        //        }
        //                        //        catch (Exception e)
        //                        //        {
        //                        //            //erros e mensagens
        //                        //            if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                        //            {

        //                        //                _log.Error(e.InnerException.InnerException.Message);
        //                        //                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                        //            }

        //                        //            if (e.Message != null)
        //                        //            {

        //                        //                _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                        //                return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                        //            }

        //                        //            return BadRequest("ERRO AO SALVAR PRODUTO");
        //                        //        }//fim do catch


        //                        //    }




        //                        //}

        //                    }
        //                    else
        //                    {
        //                        //caso contrario tributado na tabela normal
        //                        //se não for simples nacional, o produto é tributado em tributacao_produtos
        //                        //verificar o id do produto: SE FOR NULO ELE BUSCA NO BANCO, SE NAO FOR ELE PASSA E USA O MESMO QUE FOI ATRIBUIDO ANTERIORMENTE

        //                        if (cadProd == null)
        //                        {
        //                            cadProd = db.Produtos.Where(x => x.codBarras == prodItem && x.CodBarrasGErado == codBarrasGerado).FirstOrDefault();
        //                        }

        //                        string[] ufDestinotNCM = item.UF_DESTINO.Split('|'); //pega os uf destino que veio no json


        //                        //retira o elemento vazio do array
        //                        ufDestinotNCM = ufDestinotNCM.Where(a => a != "").ToArray();

        //                        /*Salvar na tabela TributacaoNCM, caso nao exista*/
        //                        string prodItemNCM = item.PRODUTO_NCM; //PEGA O NCM DO ITEM


        //                        //verifica se é simples nacional realmente para o filtro
        //                        int simpN = int.Parse(empresa.simples_nacional.ToString());
        //                        //BUSCA PELO NCM NA TABELA
        //                        //TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM).FirstOrDefault();

        //                        for (int i = 0; i < ufDestinotNCM.Count(); i++)
        //                        {
        //                            string dest = ufDestinotNCM[i].ToString();
        //                            TributacaoNCM tribnaNCM = db.TributacaoNCM.Where(x => x.ncm == prodItemNCM && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest && x.Simp_Nacional == simpN).FirstOrDefault();
        //                            if (tribnaNCM == null)
        //                            {
        //                                TributacaoNCM prodTribNCMSalvar = new TributacaoNCM();

        //                                if (item.PRODUTO_CATEGORIA != null)
        //                                {
        //                                    prodTribNCMSalvar.categoria = int.Parse(item.PRODUTO_CATEGORIA); //JA VERIFICADO SE HA A CATEGORIA NA TABELA

        //                                }
        //                                else
        //                                {
        //                                    prodTribNCMSalvar.categoria = 2794; //se a cat vier nulo atribuir 2794(VERIFICAR)
        //                                }


        //                                prodTribNCMSalvar.UF_Origem = item.UF_ORIGEM;
        //                                prodTribNCMSalvar.UF_Destino = ufDestinotNCM[i];
        //                                prodTribNCMSalvar.cest = item.PRODUTO_CEST;
        //                                prodTribNCMSalvar.ncm = item.PRODUTO_NCM;
        //                                prodTribNCMSalvar.auditadoPorNCM = 0;
        //                                prodTribNCMSalvar.Simp_Nacional = 0;
        //                                prodTribNCMSalvar.dataCad = DateTime.Now;
        //                                prodTribNCMSalvar.dataAlt = DateTime.Now;

        //                                try
        //                                {

        //                                    db.TributacaoNCM.Add(prodTribNCMSalvar);//objeto para ser salvo no banco
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch
        //                            }
        //                        }



        //                        //Tributacao tribProd = db.Tributacoes.Where(x => x.idProduto == cadProd.Id).FirstOrDefault();

        //                        for (int i = 0; i < ufDestinotNCM.Count(); i++)
        //                        {

        //                            string dest = ufDestinotNCM[i].ToString();

        //                            Tributacao tribProd = db.Tributacoes.Where(x => x.idProduto == cadProd.Id && x.UF_Origem == item.UF_ORIGEM && x.UF_Destino == dest).FirstOrDefault();

        //                            if (tribProd == null)
        //                            {
        //                                Tributacao prodTribSalvar = new Tributacao()
        //                                {

        //                                    UF_Origem = item.UF_ORIGEM,
        //                                    idProduto = cadProd.Id,
        //                                    idSetor = 91,
        //                                    dataCad = DateTime.Now,
        //                                    dataAlt = DateTime.Now,
        //                                    UF_Destino = ufDestinotNCM[i],
        //                                    auditadoPorNCM = 0

        //                                };
        //                                try
        //                                {

        //                                    db.Tributacoes.Add(prodTribSalvar);//objeto para ser salvo no banco
        //                                                                       // bd.Tributacoes.Add(prodTribSalvar);//objeto para ser salvo no banco de comparação
        //                                    db.SaveChanges();

        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR PRODUTO: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR PRODUTO");
        //                                }//fim do catch

        //                            }

        //                        }



        //                    }

        //                    //contar os que vieram com codigo de barras 0
        //                    if (item.PRODUTO_COD_BARRAS == "0")
        //                    {
        //                        prodZerado++;
        //                    }

        //                    //Verificar em todos os destinos se o item foi tributado no cliente
        //                    string[] ufDestinoE = item.UF_DESTINO.Split('|');

        //                    //retira o elemento vazio do array deixando somente os id dos registros
        //                    ufDestinoE = ufDestinoE.Where(a => a != "").ToArray();

        //                    //where: where com o codigo de barras do produto e cnpj
        //                    /*aqui ele verifica se o produto ja contem no cnpj informado*/

        //                    for (int i = 0; i < ufDestinoE.Count(); i++)
        //                    {
        //                        string dest = ufDestinoE[i].ToString();

        //                        //where: where com o codigo de barras do produto e cnpj
        //                        /*aqui ele verifica se o produto ja contem no cnpj informado*/

        //                        var tribEmpresas2 = db.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID != 0); //select na tabela

        //                        //var tribEmpresas2 = tribEmpresas2.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest));
        //                        int contnumb = tribEmpresas2.Count();
        //                        //se vier algo da consulta acima
        //                        if (tribEmpresas2.Count() <= 0 && item.PRODUTO_COD_BARRAS != "0")
        //                        {
        //                            TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
        //                            //atribunido dados ao objeto
        //                            itemSalvar.CNPJ_EMPRESA = empresa.cnpj;
        //                            itemSalvar.PRODUTO_COD_BARRAS = item.PRODUTO_COD_BARRAS;
        //                            itemSalvar.COD_BARRAS_GERADO = codBarrasGerado;
        //                            itemSalvar.PRODUTO_DESCRICAO = item.PRODUTO_DESCRICAO;
        //                            itemSalvar.PRODUTO_CEST = item.PRODUTO_CEST;
        //                            itemSalvar.PRODUTO_NCM = item.PRODUTO_NCM;
        //                            itemSalvar.PRODUTO_CATEGORIA = item.PRODUTO_CATEGORIA;/*Ponto a analisar, pois vem do cliente descrição*/
        //                            itemSalvar.FECP = item.FECP;
        //                            itemSalvar.COD_NAT_RECEITA = item.COD_NAT_RECEITA;
        //                            itemSalvar.CST_ENTRADA_PIS_COFINS = item.CST_ENTRADA_PIS_COFINS;
        //                            itemSalvar.CST_SAIDA_PIS_COFINS = item.CST_SAIDA_PIS_COFINS;
        //                            itemSalvar.ALIQ_ENTRADA_PIS = item.ALIQ_ENTRADA_PIS;
        //                            itemSalvar.ALIQ_SAIDA_PIS = item.ALIQ_ENTRADA_PIS;
        //                            itemSalvar.ALIQ_ENTRADA_COFINS = item.ALIQ_ENTRADA_COFINS;
        //                            itemSalvar.ALIQ_SAIDA_COFINS = item.ALIQ_SAIDA_COFINS;
        //                            itemSalvar.CST_VENDA_ATA = item.CST_VENDA_ATA;
        //                            itemSalvar.ALIQ_ICMS_VENDA_ATA = item.ALIQ_ICMS_VENDA_ATA;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_ATA = item.ALIQ_ICMS_ST_VENDA_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA = item.RED_BASE_CALC_ICMS_VENDA_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA = item.RED_BASE_CALC_ICMS_ST_VENDA_ATA;
        //                            itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL = item.CST_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
        //                            itemSalvar.CST_VENDA_VAREJO_CONT = item.CST_VENDA_VAREJO_CONT;
        //                            itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT = item.ALIQ_ICMS_VENDA_VAREJO_CONT;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONT = item.ALIQ_ICMS_ST_VENDA_VAREJO_CONT;
        //                            itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT = item.RED_BASE_CALC_VENDA_VAREJO_CONT;
        //                            itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;
        //                            itemSalvar.CST_VENDA_VAREJO_CONS_FINAL = item.CST_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
        //                            itemSalvar.CST_COMPRA_DE_IND = item.CST_COMPRA_DE_IND;
        //                            itemSalvar.ALIQ_ICMS_COMP_DE_IND = item.ALIQ_ICMS_COMP_DE_IND;
        //                            itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND = item.ALIQ_ICMS_ST_COMP_DE_IND;
        //                            itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND = item.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;
        //                            itemSalvar.CST_COMPRA_DE_ATA = item.CST_COMPRA_DE_ATA;
        //                            itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA = item.ALIQ_ICMS_COMPRA_DE_ATA;
        //                            itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA = item.ALIQ_ICMS_ST_COMPRA_DE_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;
        //                            itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL = item.CST_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
        //                            itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
        //                            itemSalvar.CST_DA_NFE_DA_IND_FORN = item.CST_DA_NFE_DA_IND_FORN;
        //                            itemSalvar.CST_DA_NFE_DE_ATA_FORN = item.CST_DA_NFE_DE_ATA_FORN;
        //                            itemSalvar.CSOSNT_DANFE_DOS_NFOR = item.CSOSNT_DANFE_DOS_NFOR;
        //                            itemSalvar.ALIQ_ICMS_NFE = item.ALIQ_ICMS_NFE;
        //                            itemSalvar.ALIQ_ICMS_NFE_FOR_ATA = item.ALIQ_ICMS_NFE_FOR_ATA;
        //                            itemSalvar.ALIQ_ICMS_NFE_FOR_SN = item.ALIQ_ICMS_NFE_FOR_SN;
        //                            itemSalvar.TIPO_MVA = item.TIPO_MVA;
        //                            itemSalvar.VALOR_MVA_IND = item.VALOR_MVA_IND;
        //                            itemSalvar.INICIO_VIGENCIA_MVA = item.INICIO_VIGENCIA_MVA; //data
        //                            itemSalvar.FIM_VIGENCIA_MVA = item.FIM_VIGENCIA_MVA; //data
        //                            itemSalvar.CREDITO_OUTORGADO = item.CREDITO_OUTORGADO;
        //                            itemSalvar.VALOR_MVA_ATACADO = item.VALOR_MVA_ATACADO;
        //                            itemSalvar.REGIME_2560 = item.REGIME_2560;
        //                            itemSalvar.UF_ORIGEM = item.UF_ORIGEM;
        //                            itemSalvar.UF_DESTINO = ufDestinoE[i];
        //                            itemSalvar.PRODUTO_COD_INTERNO = item.PRODUTO_COD_INTERNO;
        //                            //data da inclusão/alteração
        //                            itemSalvar.DT_ALTERACAO = DateTime.Now;


        //                            //Verifica se o item veio ativo, caso venha null considera ativo
        //                            if (item.ATIVO == null)
        //                            {
        //                                itemSalvar.ATIVO = 1;
        //                            }
        //                            else
        //                            {
        //                                itemSalvar.ATIVO = sbyte.Parse(item.ATIVO);
        //                            }



        //                            //try catch para salvar no banco e na lista de retorno
        //                            try
        //                            {

        //                                db.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco
        //                                bd.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco de comparação
        //                                listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
        //                                db.SaveChanges();

        //                                cont++;
        //                            }
        //                            catch (Exception e)
        //                            {
        //                                //erros e mensagens
        //                                if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                {

        //                                    _log.Error(e.InnerException.InnerException.Message);
        //                                    return BadRequest("ERRO AO SALVAR ITEM: " + e.InnerException.InnerException.Message);
        //                                }

        //                                if (e.Message != null)
        //                                {

        //                                    _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                    return BadRequest("ERRO AO SALVAR ITEM: " + e.Message);
        //                                }

        //                                return BadRequest("ERRO AO SALVAR ITEM");
        //                            }//fim do catch

        //                        }
        //                        else
        //                        {
        //                            //se o codigo de barras não foi importado o entra na condição, ou seja o retorno do tribempresas2 é 0
        //                            //sendo zero o produto nao foi importado, agora ele será com todos os seus dados
        //                            //alteração 16092021->alem de nao ter encontrado nada no banco, count=0 o codigo de barras deve ser diferente de 0(zero)
        //                            //pegar o id desse registro
        //                            var idDoRegistros = db.TributacaoEmpresas.Where(s => s.PRODUTO_COD_BARRAS.Equals(item.PRODUTO_COD_BARRAS) && s.CNPJ_EMPRESA.Contains(cnpjFormatado) && s.UF_ORIGEM.Equals(item.UF_ORIGEM) && s.UF_DESTINO.Equals(dest)).Select(x => x.ID).FirstOrDefault();
        //                            if (idDoRegistros != 0)
        //                            {
        //                                TributacaoEmpresa itemSalvar = new TributacaoEmpresa();
        //                                itemSalvar = db.TributacaoEmpresas.Find(idDoRegistros);
        //                                itemSalvar.PRODUTO_DESCRICAO = (itemSalvar.PRODUTO_DESCRICAO != item.PRODUTO_DESCRICAO) ? ((item.PRODUTO_DESCRICAO != null) ? item.PRODUTO_DESCRICAO : itemSalvar.PRODUTO_DESCRICAO) : itemSalvar.PRODUTO_DESCRICAO;
        //                                itemSalvar.PRODUTO_CEST = (itemSalvar.PRODUTO_CEST != item.PRODUTO_CEST) ? ((item.PRODUTO_CEST != null) ? item.PRODUTO_CEST : itemSalvar.PRODUTO_CEST) : itemSalvar.PRODUTO_CEST;
        //                                itemSalvar.PRODUTO_NCM = (itemSalvar.PRODUTO_NCM != item.PRODUTO_NCM) ? ((item.PRODUTO_NCM != null) ? item.PRODUTO_NCM : itemSalvar.PRODUTO_NCM) : itemSalvar.PRODUTO_NCM;
        //                                itemSalvar.PRODUTO_CATEGORIA = (itemSalvar.PRODUTO_CATEGORIA != item.PRODUTO_CATEGORIA) ? ((item.PRODUTO_CATEGORIA != null) ? item.PRODUTO_CATEGORIA : itemSalvar.PRODUTO_CATEGORIA) : itemSalvar.PRODUTO_CATEGORIA;
        //                                itemSalvar.FECP = (itemSalvar.FECP != item.FECP) ? ((item.FECP != null) ? item.FECP : itemSalvar.FECP) : itemSalvar.FECP;
        //                                itemSalvar.COD_NAT_RECEITA = (itemSalvar.COD_NAT_RECEITA != item.COD_NAT_RECEITA) ? ((item.COD_NAT_RECEITA != null) ? item.COD_NAT_RECEITA : itemSalvar.COD_NAT_RECEITA) : itemSalvar.COD_NAT_RECEITA;

        //                                itemSalvar.CST_ENTRADA_PIS_COFINS = (itemSalvar.CST_ENTRADA_PIS_COFINS != item.CST_ENTRADA_PIS_COFINS) ? ((item.CST_ENTRADA_PIS_COFINS != null) ? item.CST_ENTRADA_PIS_COFINS : itemSalvar.CST_ENTRADA_PIS_COFINS) : itemSalvar.CST_ENTRADA_PIS_COFINS;
        //                                itemSalvar.CST_SAIDA_PIS_COFINS = (itemSalvar.CST_SAIDA_PIS_COFINS != item.CST_SAIDA_PIS_COFINS) ? ((item.CST_SAIDA_PIS_COFINS != null) ? item.CST_SAIDA_PIS_COFINS : itemSalvar.CST_SAIDA_PIS_COFINS) : itemSalvar.CST_SAIDA_PIS_COFINS;
        //                                itemSalvar.ALIQ_ENTRADA_PIS = (itemSalvar.ALIQ_ENTRADA_PIS != item.ALIQ_ENTRADA_PIS) ? ((item.ALIQ_ENTRADA_PIS != null) ? item.ALIQ_ENTRADA_PIS : itemSalvar.ALIQ_ENTRADA_PIS) : itemSalvar.ALIQ_ENTRADA_PIS;
        //                                itemSalvar.ALIQ_SAIDA_PIS = (itemSalvar.ALIQ_SAIDA_PIS != item.ALIQ_SAIDA_PIS) ? ((item.ALIQ_SAIDA_PIS != null) ? item.ALIQ_SAIDA_PIS : itemSalvar.ALIQ_SAIDA_PIS) : itemSalvar.ALIQ_SAIDA_PIS;
        //                                itemSalvar.ALIQ_ENTRADA_COFINS = (itemSalvar.ALIQ_ENTRADA_COFINS != item.ALIQ_ENTRADA_COFINS) ? ((item.ALIQ_ENTRADA_COFINS != null) ? item.ALIQ_ENTRADA_COFINS : itemSalvar.ALIQ_ENTRADA_COFINS) : itemSalvar.ALIQ_ENTRADA_COFINS;
        //                                itemSalvar.ALIQ_SAIDA_COFINS = (itemSalvar.ALIQ_SAIDA_COFINS != item.ALIQ_SAIDA_COFINS) ? ((item.ALIQ_SAIDA_COFINS != null) ? item.ALIQ_SAIDA_COFINS : itemSalvar.ALIQ_SAIDA_COFINS) : itemSalvar.ALIQ_SAIDA_COFINS;

        //                                itemSalvar.CST_VENDA_ATA = (itemSalvar.CST_VENDA_ATA != item.CST_VENDA_ATA) ? ((item.CST_VENDA_ATA != null) ? item.CST_VENDA_ATA : itemSalvar.CST_VENDA_ATA) : itemSalvar.CST_VENDA_ATA;
        //                                itemSalvar.ALIQ_ICMS_VENDA_ATA = (itemSalvar.ALIQ_ICMS_VENDA_ATA != item.ALIQ_ICMS_VENDA_ATA) ? ((item.ALIQ_ICMS_VENDA_ATA != null) ? item.ALIQ_ICMS_VENDA_ATA : itemSalvar.ALIQ_ICMS_VENDA_ATA) : itemSalvar.ALIQ_ICMS_VENDA_ATA;
        //                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA != item.ALIQ_ICMS_ST_VENDA_ATA) ? ((item.ALIQ_ICMS_ST_VENDA_ATA != null) ? item.ALIQ_ICMS_ST_VENDA_ATA : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA != item.RED_BASE_CALC_ICMS_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA;

        //                                itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL != item.CST_VENDA_ATA_SIMP_NACIONAL) ? ((item.CST_VENDA_ATA_SIMP_NACIONAL != null) ? item.CST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.CST_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL;


        //                                itemSalvar.CST_VENDA_VAREJO_CONT = (itemSalvar.CST_VENDA_VAREJO_CONT != item.CST_VENDA_VAREJO_CONT) ? ((item.CST_VENDA_VAREJO_CONT != null) ? item.CST_VENDA_VAREJO_CONT : itemSalvar.CST_VENDA_VAREJO_CONT) : itemSalvar.CST_VENDA_VAREJO_CONT;
        //                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT != item.ALIQ_ICMS_VENDA_VAREJO_CONT) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONT != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONT : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONT;
        //                                itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT != item.RED_BASE_CALC_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_VENDA_VAREJO_CONT;
        //                                itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT = (itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) ? ((item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null) ? item.RED_BASE_CALC_ST_VENDA_VAREJO_CONT : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT) : itemSalvar.RED_BASE_CALC_ST_VENDA_VAREJO_CONT;


        //                                itemSalvar.CST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.CST_VENDA_VAREJO_CONS_FINAL != item.CST_VENDA_VAREJO_CONS_FINAL) ? ((item.CST_VENDA_VAREJO_CONS_FINAL != null) ? item.CST_VENDA_VAREJO_CONS_FINAL : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.CST_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) ? ((item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null) ? item.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL;


        //                                itemSalvar.CST_COMPRA_DE_IND = (itemSalvar.CST_COMPRA_DE_IND != item.CST_COMPRA_DE_IND) ? ((item.CST_COMPRA_DE_IND != null) ? item.CST_COMPRA_DE_IND : itemSalvar.CST_COMPRA_DE_IND) : itemSalvar.CST_COMPRA_DE_IND;
        //                                itemSalvar.ALIQ_ICMS_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_COMP_DE_IND != item.ALIQ_ICMS_COMP_DE_IND) ? ((item.ALIQ_ICMS_COMP_DE_IND != null) ? item.ALIQ_ICMS_COMP_DE_IND : itemSalvar.ALIQ_ICMS_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_COMP_DE_IND;
        //                                itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND = (itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND != item.ALIQ_ICMS_ST_COMP_DE_IND) ? ((item.ALIQ_ICMS_ST_COMP_DE_IND != null) ? item.ALIQ_ICMS_ST_COMP_DE_IND : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND) : itemSalvar.ALIQ_ICMS_ST_COMP_DE_IND;
        //                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_IND;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND;

        //                                itemSalvar.CST_COMPRA_DE_ATA = (itemSalvar.CST_COMPRA_DE_ATA != item.CST_COMPRA_DE_ATA) ? ((item.CST_COMPRA_DE_ATA != null) ? item.CST_COMPRA_DE_ATA : itemSalvar.CST_COMPRA_DE_ATA) : itemSalvar.CST_COMPRA_DE_ATA;
        //                                itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA != item.ALIQ_ICMS_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_COMPRA_DE_ATA;
        //                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA != item.ALIQ_ICMS_ST_COMPRA_DE_ATA) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_ATA != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_ATA : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_DE_ATA;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA;

        //                                itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL != item.CST_COMPRA_DE_SIMP_NACIONAL) ? ((item.CST_COMPRA_DE_SIMP_NACIONAL != null) ? item.CST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.CST_COMPRA_DE_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL;
        //                                itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL;
        //                                itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL = (itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) ? ((item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null) ? item.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL) : itemSalvar.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL;


        //                                itemSalvar.CST_DA_NFE_DA_IND_FORN = (itemSalvar.CST_DA_NFE_DA_IND_FORN != item.CST_DA_NFE_DA_IND_FORN) ? ((item.CST_DA_NFE_DA_IND_FORN != null) ? item.CST_DA_NFE_DA_IND_FORN : itemSalvar.CST_DA_NFE_DA_IND_FORN) : itemSalvar.CST_DA_NFE_DA_IND_FORN;
        //                                itemSalvar.CST_DA_NFE_DE_ATA_FORN = (itemSalvar.CST_DA_NFE_DE_ATA_FORN != item.CST_DA_NFE_DE_ATA_FORN) ? ((item.CST_DA_NFE_DE_ATA_FORN != null) ? item.CST_DA_NFE_DE_ATA_FORN : itemSalvar.CST_DA_NFE_DE_ATA_FORN) : itemSalvar.CST_DA_NFE_DE_ATA_FORN;
        //                                itemSalvar.CSOSNT_DANFE_DOS_NFOR = (itemSalvar.CSOSNT_DANFE_DOS_NFOR != item.CSOSNT_DANFE_DOS_NFOR) ? ((item.CSOSNT_DANFE_DOS_NFOR != null) ? item.CSOSNT_DANFE_DOS_NFOR : itemSalvar.CSOSNT_DANFE_DOS_NFOR) : itemSalvar.CSOSNT_DANFE_DOS_NFOR;

        //                                itemSalvar.ALIQ_ICMS_NFE = (itemSalvar.ALIQ_ICMS_NFE != item.ALIQ_ICMS_NFE) ? ((item.ALIQ_ICMS_NFE != null) ? item.ALIQ_ICMS_NFE : itemSalvar.ALIQ_ICMS_NFE) : itemSalvar.ALIQ_ICMS_NFE;
        //                                itemSalvar.ALIQ_ICMS_NFE_FOR_ATA = (itemSalvar.ALIQ_ICMS_NFE_FOR_ATA != item.ALIQ_ICMS_NFE_FOR_ATA) ? ((item.ALIQ_ICMS_NFE_FOR_ATA != null) ? item.ALIQ_ICMS_NFE_FOR_ATA : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA) : itemSalvar.ALIQ_ICMS_NFE_FOR_ATA;
        //                                itemSalvar.ALIQ_ICMS_NFE_FOR_SN = (itemSalvar.ALIQ_ICMS_NFE_FOR_SN != item.ALIQ_ICMS_NFE_FOR_SN) ? ((item.ALIQ_ICMS_NFE_FOR_SN != null) ? item.ALIQ_ICMS_NFE_FOR_SN : itemSalvar.ALIQ_ICMS_NFE_FOR_SN) : itemSalvar.ALIQ_ICMS_NFE_FOR_SN;


        //                                itemSalvar.TIPO_MVA = (itemSalvar.TIPO_MVA != item.TIPO_MVA) ? ((item.TIPO_MVA != null) ? item.TIPO_MVA : itemSalvar.TIPO_MVA) : itemSalvar.TIPO_MVA;

        //                                itemSalvar.VALOR_MVA_IND = (itemSalvar.VALOR_MVA_IND != item.VALOR_MVA_IND) ? ((item.VALOR_MVA_IND != null) ? item.VALOR_MVA_IND : itemSalvar.VALOR_MVA_IND) : itemSalvar.VALOR_MVA_IND;

        //                                itemSalvar.INICIO_VIGENCIA_MVA = (itemSalvar.INICIO_VIGENCIA_MVA != item.INICIO_VIGENCIA_MVA) ? ((item.INICIO_VIGENCIA_MVA != null) ? item.INICIO_VIGENCIA_MVA : itemSalvar.INICIO_VIGENCIA_MVA) : itemSalvar.INICIO_VIGENCIA_MVA;

        //                                itemSalvar.FIM_VIGENCIA_MVA = (itemSalvar.FIM_VIGENCIA_MVA != item.FIM_VIGENCIA_MVA) ? ((item.FIM_VIGENCIA_MVA != null) ? item.FIM_VIGENCIA_MVA : itemSalvar.FIM_VIGENCIA_MVA) : itemSalvar.FIM_VIGENCIA_MVA;

        //                                itemSalvar.CREDITO_OUTORGADO = (itemSalvar.CREDITO_OUTORGADO != item.CREDITO_OUTORGADO) ? ((item.CREDITO_OUTORGADO != null) ? item.CREDITO_OUTORGADO : itemSalvar.CREDITO_OUTORGADO) : itemSalvar.CREDITO_OUTORGADO;

        //                                itemSalvar.VALOR_MVA_ATACADO = (itemSalvar.VALOR_MVA_ATACADO != item.VALOR_MVA_ATACADO) ? ((item.VALOR_MVA_ATACADO != null) ? item.VALOR_MVA_ATACADO : itemSalvar.VALOR_MVA_ATACADO) : itemSalvar.VALOR_MVA_ATACADO;

        //                                itemSalvar.REGIME_2560 = (itemSalvar.REGIME_2560 != item.REGIME_2560) ? ((item.REGIME_2560 != null) ? item.REGIME_2560 : itemSalvar.REGIME_2560) : itemSalvar.REGIME_2560;

        //                                itemSalvar.UF_ORIGEM = (itemSalvar.UF_ORIGEM != item.UF_ORIGEM) ? ((item.UF_ORIGEM != null) ? item.UF_ORIGEM : itemSalvar.UF_ORIGEM) : itemSalvar.UF_ORIGEM;

        //                                itemSalvar.UF_DESTINO = (itemSalvar.UF_DESTINO != ufDestinoE[i]) ? ((item.UF_DESTINO != null) ? ufDestinoE[i] : itemSalvar.UF_DESTINO) : itemSalvar.UF_DESTINO;

        //                                //data da inclusão/alteração
        //                                itemSalvar.DT_ALTERACAO = DateTime.Now;
        //                                //try catch para salvar no banco e na lista de retorno
        //                                try
        //                                {

        //                                    //db.TributacaoEmpresas.Add(itemSalvar);//objeto para ser salvo no banco
        //                                    listaSalvosTribEmpresa.Add(itemSalvar);//lista para retorno
        //                                    db.SaveChanges(); //SALVAR AS ALTERACOES
        //                                    contAlterados++;
        //                                }
        //                                catch (Exception e)
        //                                {
        //                                    //erros e mensagens
        //                                    if (e.InnerException != null && e.InnerException.InnerException != null && e.InnerException.InnerException.Message != null)
        //                                    {

        //                                        _log.Error(e.InnerException.InnerException.Message);
        //                                        return BadRequest("ERRO AO SALVAR ITEM: " + e.InnerException.InnerException.Message);
        //                                    }

        //                                    if (e.Message != null)
        //                                    {

        //                                        _log.Error("ERRO AO SALVAR itemRec " + e.Message);
        //                                        return BadRequest("ERRO AO SALVAR ITEM: " + e.Message);
        //                                    }

        //                                    return BadRequest("ERRO AO SALVAR ITEM");
        //                                }//fim do catch
        //                            }

        //                        }



        //                    }
        //                    codBarrasTamanho++; //quantidade de produtos que o codigo de barras era menor que 7
        //                } //fim do else tamanho codigo de barras

        //            } //fim do if do codigo barras nulo
        //            else
        //            {
        //                /*TO-DO : PRODUTOS QUE NÃO POSSUEM CODIGO DE BARRAS*/


        //                aux++; //soma um a cada vez que um item não possuir codigo de barras
        //            }//fim do else



        //            //}//fim segundo if estado origem e destino (verifica se foi o valor correto



        //        }//FIM DO ELSE DO ESTADO ORIGEM E DESTINO

        //    }//fim do foreach dos itens

        //    //se o contador de itens salvos vier zero, retorno
        //    //if (cont <= 0)
        //    //{
        //    //    if (auxEstado > 0)
        //    //    {
        //    //        var myError = new
        //    //        {
        //    //            sucess = "false",
        //    //            errors = "UF DE ORIGEM OU DESTINO INFORMADOS INCORRETAMENTE"
        //    //        };
        //    //        return BadRequest(myError.ToString());
        //    //    }
        //    //    else
        //    //    {
        //    //        var myError2 = new
        //    //        {
        //    //            sucess = "false",
        //    //            errors = "NENHUM PRODUTO IMPORTADO,  ESTÃO COM COD_BARRAS = NULL(VAZIOS) / COD_BARRAS igual a 0(zero) / COD_BARRAS com tamanho incorreto"
        //    //        };

        //    //        //return (IHttpActionResult)Request.CreateResponse(HttpStatusCode.BadRequest, myError);
        //    //        //return (HttpStatusCode.BadRequest, Json("email or password is null"));
        //    //        return BadRequest(myError2.ToString());
        //    //    }


        //    //}
        //    //else
        //    //{
        //    //    db.SaveChanges(); //salva caso o contador seja maior que zero
        //    //}
        //    if (contAlterados <= 0 && cont <= 0)
        //    {
        //        if (auxEstado > 0)
        //        {
        //            var myError = new
        //            {
        //                sucess = "false",
        //                errors = "UF DE ORIGEM OU DESTINO INFORMADOS INCORRETAMENTE"
        //            };
        //            return BadRequest(myError.ToString());
        //        }
        //        else
        //        {
        //            var myError2 = new
        //            {
        //                sucess = "false",
        //                errors = "NENHUM PRODUTO IMPORTADO,  ESTÃO COM COD_BARRAS = NULL(VAZIOS) / COD_BARRAS igual a 0(zero) / COD_BARRAS com tamanho incorreto"
        //            };

        //            //return (IHttpActionResult)Request.CreateResponse(HttpStatusCode.BadRequest, myError);
        //            //return (HttpStatusCode.BadRequest, Json("email or password is null"));
        //            return BadRequest(myError2.ToString());
        //        }
        //    }

        //    _log.Debug("FINAL DE PROCESSO COM " + cont + " ITENS SALVOS");

        //    //return Ok("Itens informados no JSON: " + itens.Count() + " - Itens salvos: " + cont + " - Itens sem código de barras : " + aux+ " - Item com COD_BARRAS = 0(zero) : "+ prodZerado.ToString()+" - Itens alterados : "+ contAlterados);

        //    //return Ok(new { sucess = "true", data = itens.ToArray(), paginaAtual = 1, totalPaginas = 25, totalItens = itens.Count(), tempaginaAnterior="false", temPaginaSeguinte = "false" });
        //    //return Ok(new { sucess = "true", data = itens.ToArray(),  totalItens = itens.Count() });
        //    return Ok(new { sucess = "true", itensSalvos = cont, ufOrigemDestinoIncorretos = auxEstado, semCodigoBarras = aux, itemCodigoBarrasZero = prodZerado.ToString(), itensAlterados = contAlterados, codBarrasTamanhoIncorreto = codBarrasTamanho, totalItens = itens.Count() }); ;

        //} //fim da action



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
