using MtxApi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;

namespace MtxApi.Controllers
{
    public class EmpresaController : ApiController
    {
        private MtxApiContext db = new MtxApiContext();

        // GET: api/Empresa
        public IQueryable<Empresa> GetEmpresas()
        {
            return db.Empresas;
        }

        //Retorna a empresa pelo ID
        // GET: api/EmpresaMtx/5
        [ResponseType(typeof(Empresa))]
        public IHttpActionResult GetEmpresa(int id)
        {
            Empresa empresa = db.Empresas.Find(id);
            if (empresa == null)
            {
                return BadRequest("EMPRESA NÃO ENCONTRADA");
            }

            return Ok(empresa);
        }


        //Retorna a empresa pelo CNPJ
        
        [Route("api/EmpresaCnpj/{cnpj}")]
        [ResponseType(typeof(Empresa))]
        public IHttpActionResult GetEmpresaCnpj(string cnpj)
        {
            //pegar o header que é a chave
            var chaveToken = HttpContext.Current.Request.Headers["chave"];
            if(chaveToken == null)
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
            Empresa empresa = db.Empresas.FirstOrDefault(x => x.cnpj.Equals(cnpjFormatado));
            var empresaChave = db.Empresas.ToList();

            if (empresa == null)
            {
                return BadRequest("EMPRESA NÃO ENCONTRADA");
            }
            else
            {
                empresaChave = empresaChave.Where(x => x.SoftwareHouse.Chave.Equals(chaveToken.ToString())).ToList();
                if(empresaChave.Count == 0)
                {
                    return BadRequest("CHAVE/TOKEN INFORMADA NÃO CORRESPONDENTE, VERIFIQUE ESSE DADO E TENTE NOVAMENTE");
                }
                else
                {
                    var empresaUsuListar2 = db.Empresas.Where(x => x.cnpj.Equals(cnpjFormatado)).Select(x => new {
                        x.id,
                        x.cnpj,
                        x.razacaosocial,
                        x.fantasia,
                        x.logradouro,
                        x.numero,
                        x.cep,
                        x.complemento,
                        x.cidade,
                        x.estado,
                        x.telefone,
                        x.usuario_admin_inicial,
                        x.SoftwareHouse.Nome_SoftwareHouse
                    }).ToList();

                    return Ok(empresaUsuListar2);
                }
                
            }

            
        }

        /*Incluir registro de nova empresa*/
        [Route("api/EmpresaSalvar/{cnpj}")]
        public IHttpActionResult PostEmpresaSalvar(string cnpj, List<EmpresaJson>dados)
        {
            //pegar o header que é a chave
            var chaveToken = HttpContext.Current.Request.Headers["chave"];

            if (chaveToken == null)
            {
                return BadRequest("AUTENTICAÇÃO INCORRETA. CHAVE NECESSÁRIA");
            }

            //VERIFICA A QTD DE EMPRESAS NA REQUISIÇÃO
            if (dados.Count > 1)
            {
                return BadRequest("SOMENTE UMA EMPRESA POR REQUISIÇÃO");
            }


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
                else
                {
                    bool cnp = FormataCnpj.ValidaCNPJ(cnpj);

                    if (!cnp)
                    {
                        return BadRequest("CNPJ PASSADO COMO PARÂMETRO ESTÁ INCORRETO");
                    }
                }
            }

            //verificar se os dados informados no json estão nulos
            if (dados == null)
            {
                return BadRequest("JSON SEM DADOS DA EMPRESA PARA CADASTRO");
            }
          

            //formatando a string
            string cnpjFormatado = FormataCnpj.FormatarCNPJ(cnpj);

            //objeto para procurar empresa no banco
            Empresa empresa = db.Empresas.FirstOrDefault(x => x.cnpj.Equals(cnpjFormatado));

           
            
            //objeto para ser salvo no banco
            Empresa empresaSalvar = new Empresa();
            
            //verifica se a empresa já está cadastrada
            if (empresa == null)
            {
                
                //percorrer os dados informados
                foreach (EmpresaJson dado in dados)
                {
                    //validar a chave: pega a chave que vai no body e compara com a chave que vai no header

                    //software house
                    //verificar a qtd de digitos
                    if (dado.CNPJ_SOFTWAREHOUSE.Length != 14)
                    {
                        return BadRequest("CNPJ SOFTWARE HOUSE PASSADO ESTÁ INCORRETO");
                    }
                    else
                    {
                        bool cnp = FormataCnpj.ValidaCNPJ(dado.CNPJ_SOFTWAREHOUSE);

                        if (!cnp)
                        {
                            return BadRequest("CNPJ SOFTWARE HOUSE PASSADO ESTÁ INCORRETO");
                        }
                    }

                    List<SoftwareHouse> softHouse = new List<SoftwareHouse>();
                    softHouse = db.SoftwareHouses.ToList();
                    softHouse = softHouse.Where(x => x.Cnpj.Equals(FormataCnpj.FormatarCNPJ(dado.CNPJ_SOFTWAREHOUSE))).ToList();

                    if(softHouse.Count <= 0)
                    {
                        return BadRequest("SOFTWARE HOUSE NÃO ENCONTRADO, VERIFIQUE O CNPJ E TENTE NOVAMENTE");
                    }

                    //token
                    softHouse = softHouse.Where(x => x.Chave.Equals(chaveToken.ToString())).ToList();

                    if(softHouse.Count <= 0)
                    {
                        return BadRequest("CHAVE/TOKEN INFORMADA NÃO CORRESPONDENTE, VERIFIQUE ESSE DADO E TENTE NOVAMENTE");
                    }

                    
                    var idSoftwareHouse = (from s in db.SoftwareHouses where s.Chave.Equals(chaveToken.ToString())  select s.Id).FirstOrDefault(); //select na tabela

                    //usuário inciial
                    List<Usuario> userProc = new List<Usuario>();
                    userProc = db.Usuarios.ToList();
                    userProc = userProc.Where(x => x.email.Equals(dado.USUARIO_ADMIN_INICIAL)).ToList();
                    
                    if(userProc.Count > 0)
                    {
                        return BadRequest("USUARIO ADMIN INICIAL JÁ EXISTE, POR FAVOR INFORME OUTRO");
                    }
                    //verifica se o cnpj informado no json está nulo
                    if (dado.CNPJ_EMPRESA == null)
                    {
                        return BadRequest("CNPJ NO JSON INVÁLIDO OU AUSENTE");
                    }
                    else
                    {
                        //compara o cnpj informado no json com o informado na url da requisição
                        if (dado.CNPJ_EMPRESA != cnpjFormatado)
                        {
                            if (dado.CNPJ_EMPRESA != cnpj)
                            {
                                return BadRequest("O CNPJ INFORMADO NO PARAMETRO DIFERE DO INFORMADO NO JSON. VERIFIQUE E TENTE NOVAMENTE");
                            }
                        }
                    }
                    //verifica se o usuario passou informacao
                    if(dado.USUARIO_ADMIN_INICIAL == null)
                    {
                        return BadRequest("FAVOR INFORMAR UM USUARIO ADMIN INICIAL VÁLIDO: e-mail válido.");

                    }

                    //verifica se o email contem carcteres válidos
                    if (!IsValidEmail(dado.USUARIO_ADMIN_INICIAL.ToLower())) 
                    {
                        return BadRequest("Formato de e-mail para usuario inicial inválido.");
                    }

                    //tenta salvar os dados
                    try
                    {
                        //vindos do json
                        empresaSalvar.cnpj = FormataCnpj.FormatarCNPJ(dado.CNPJ_EMPRESA);
                        empresaSalvar.razacaosocial = dado.RAZAO_SOCIAL;
                        empresaSalvar.fantasia = dado.FANTASIA;
                        empresaSalvar.logradouro = dado.LOGRADOURO;
                        empresaSalvar.numero = dado.NUMERO;
                        empresaSalvar.cep = dado.CEP;
                        empresaSalvar.complemento = dado.COMPLEMENTO;
                        empresaSalvar.cidade = dado.CIDADE;
                        empresaSalvar.estado = dado.ESTADO;
                        empresaSalvar.telefone = dado.TELEFONE;
                        empresaSalvar.email = dado.EMAIL.ToLower();
                        empresaSalvar.idSofwareHouse = idSoftwareHouse;
                        empresaSalvar.usuario_admin_inicial = dado.USUARIO_ADMIN_INICIAL.ToLower();

                        //automaticos
                        empresaSalvar.datacad = DateTime.Now;
                        empresaSalvar.dataalt = DateTime.Now;
                        empresaSalvar.ativo = 1;

                        //adiciona o objeto ao contexto do banco
                        db.Empresas.Add(empresaSalvar);

                        //salva a empresa
                        int i = db.SaveChanges();
                        if (i > 0)
                        {
                          
                            //salvar usuario
                            //buscar empresa
                            //objeto para procurar empresa no banco
                            Empresa empresaUsu = db.Empresas.FirstOrDefault(x => x.cnpj.Equals(cnpjFormatado));
                            //objeto para ser salvo no banco
                            Usuario usuarioSalvar = new Usuario();
                            usuarioSalvar.idEmpresa = empresaUsu.id;
                            usuarioSalvar.nome = "adminempresatemp_" + cnpj; //nome do usuario adminEmpresa+cnpj
                            usuarioSalvar.senha = cnpj.ToString() + "adminempresatemp";//senha é o usuario invertido
                            usuarioSalvar.idNivel = 5; //nivel administrativo
                            usuarioSalvar.ativo = 1;
                            usuarioSalvar.dataAlt = DateTime.Now;
                            usuarioSalvar.dataCad = DateTime.Now;
                            usuarioSalvar.email = empresaUsu.usuario_admin_inicial;//usuario
                            usuarioSalvar.primeiro_acesso = 1;

                            db.Usuarios.Add(usuarioSalvar);


                            ////testar envio de email
                            SmtpClient smtp = new SmtpClient();

                            //smtp.Host = "smtp.gmail.com";
                            smtp.Host = "smtpout.secureserver.net";
                            smtp.Port = 587;
                            smtp.EnableSsl = false;
                            
                            smtp.UseDefaultCredentials = false;

                            smtp.Credentials = new System.Net.NetworkCredential("suporte@precisomtx.com.br", "MTX@12345");
                            //smtp.Credentials = new System.Net.NetworkCredential("desenvolvimentomtx@gmail.com", "kzplodtqicuytgpa");



                            MailMessage mail = new System.Net.Mail.MailMessage();
                            //mail.From = new System.Net.Mail.MailAddress("desenvolvimentomtx@gmail.com");
                            mail.From = new System.Net.Mail.MailAddress("suporte@precisomtx.com.br");

                            if (!string.IsNullOrWhiteSpace(usuarioSalvar.email.ToLower()))
                            {
                                mail.To.Add(new System.Net.Mail.MailAddress(usuarioSalvar.email));
                            }
                            else
                            {
                                db.Empresas.Remove(empresaUsu);
                                db.SaveChanges();
                                return BadRequest("Favor informar um e-mail válido.");
                            }
                            mail.Subject = "Senha Provisória - PrecisoMtx";
                            mail.Body = "Segue informações de usuário e senha provisórios:\n ";
                            mail.Body += "Usuário: " + usuarioSalvar.email + "\n";
                            mail.Body += "Senha: " + usuarioSalvar.senha + "\n";
                            mail.Body += "Obs.: A senha será alterada no primeiro acesso \n";
                            mail.Body += "Acesse: " + "http://18.223.22.3/Home/Login" + " para o primeiro Login \n";


                            //envio de email
                            try
                            {

                                try
                                {
                                    smtp.Send(mail);
                                    db.SaveChanges();
                                }
                                catch (SmtpFailedRecipientException ex)
                                {
                                    db.Empresas.Remove(empresaUsu);
                                    db.SaveChanges();
                                    return BadRequest("Problemas ao enviar autenticação, por favor verifique o email do usuario admin inicial. : ERRO : " + ex.ToString());
                                }




                            }
                            catch (SmtpException e)
                            {
                                //se nao enviou o email a empresa precisa ser deletada
                                db.Empresas.Remove(empresaUsu);
                                db.SaveChanges();
                                return BadRequest("Problemas ao enviar autenticação, por favor verifique o email do usuario admin inicial.");
                            }



                            //retornar a empresa com o usuário cadastradado

                            var empresaUsuListar2 = db.Empresas.Where(x => x.cnpj.Equals(cnpjFormatado)).Select(x=> new {x.id, x.cnpj, x.razacaosocial, x.fantasia, x.logradouro, x.numero,
                               x.cep, x.complemento, x.cidade, x.estado, x.telefone, x.usuario_admin_inicial }).ToList();


                            //return Ok(empresaUsuListar);
                            return Ok(new { sucess = "true", data = empresaUsuListar2});
                        }

                        
                    }
                    catch(Exception e)
                    {
                        return BadRequest("ERRO AO SALVAR A EMPRESA, VERIFIQUE OS DADOS E TENTE NOVAMENTE"+e.ToString());
                    }
                }//FIM FOREACH
            }
            else
            {
                return BadRequest("EMPRESA JÁ CADASTRADA NA BASE DE DADOS");

            }

            return Ok();

        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Normalize the domain
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper, RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Examines the domain part of the email and normalizes it.
                string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    string domainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + domainName;
                }
            }
            catch (RegexMatchTimeoutException e)
            {
                return false;
            }
            catch (ArgumentException e)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
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
