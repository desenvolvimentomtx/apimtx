using MtxApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MtxApi.Controllers
{
    public class HomeApiController : Controller
    {
        readonly private MtxApiContext db = new MtxApiContext();

        List<AnaliseTributaria> analise = new List<AnaliseTributaria>();

        Usuario usuario;
        Empresa empresa;

        // GET: HomeApi
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult EmpresaTributacao(string cnpj)
        {
            if (cnpj.Length != 14)
            {
                ViewBag.ErroMensagem="CNPJ PASSADO COMO PARÂMETRO ESTÁ INCORRETO";
                return View();
            }
            else
            {
                string cnpjFormatado = FormataCnpj.FormatarCNPJ(cnpj);
                this.empresa = (from a in db.Empresas where a.cnpj == cnpjFormatado select a).FirstOrDefault(); //empresa
            }
           
            
            if(empresa == null)
            {
                ViewBag.ErroMensagem = "Não há empresa cadastrada para esse cnpj";
            }
            else
            {
                this.usuario = (from a in db.Usuarios where a.idEmpresa == empresa.id select a).FirstOrDefault();

                if (usuario == null)
                {
                    ViewBag.ErroMensagem = "Não há usuarios cadastrados para empresa";
                }
                else
                {
                    ViewBag.RazaoSocial = empresa.razacaosocial;
                    ViewBag.Fantasia = empresa.fantasia;
                    ViewBag.Cnpj = empresa.cnpj;
                    ViewBag.Cidade = empresa.cidade;
                    ViewBag.Estado = empresa.estado;

                    //Analise
                    this.analise = (from a in db.Analise_Tributaria where a.CNPJ_EMPRESA == empresa.cnpj select a).ToList();

                    ViewBag.TotalIcmsVarejoCF = this.analise.Count();
                    /*OBS: 22072021: ACERTADO COMPARAÇÃO DE IGUALDADE: RETIRAR OS NULOS*/
                    /*Aliq ICMS Venda Varejo Consumidor Final - ok*/
                    ViewBag.AlqICMSVarejoCFMaior = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL > a.Aliq_Icms_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqICMSVarejoCFMenor = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL < a.Aliq_Icms_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqICMSVarejoCFIgual = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL == a.Aliq_Icms_Venda_Varejo_Cons_Final_INTERNO && a.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null);
                    ViewBag.AlqICMSVarejoCFNullaInterno = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL != null && a.Aliq_Icms_Venda_Varejo_Cons_Final_INTERNO == null); //onde nao for nulo no cliente mas no mtx sim
                    ViewBag.AlqICMSVarejoCFNullaExterno = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONS_FINAL == null);
                   


                    /*Aliq ICMS ST Venda Varejo Consumidor Final - ok*/
                    ViewBag.AlqICMSSTVarejoCFMaior = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL > a.Aliq_Icms_ST_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqICMSSTVarejoCFMenor = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL < a.Aliq_Icms_ST_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqICMSSTVarejoCFIgual = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL == a.Aliq_Icms_ST_Venda_Varejo_Cons_Final_INTERNO && a.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null);
                    ViewBag.AlqICMSSTVarejoCFNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null && a.Aliq_Icms_ST_Venda_Varejo_Cons_Final_INTERNO == null);
                    ViewBag.AlqICMSSTVarejoCFNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONS_FINAL == null);

                    /*Aliq ICMS Venda Varejo Contribuinte - ok*/
                    ViewBag.AlqICMSVendaVContMaior = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONT > a.Aliq_Icms_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqICMSVendaVContMenor = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONT < a.Aliq_Icms_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqICMSVendaVContIguais = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONT == a.Aliq_Icms_Venda_Varejo_Cont_INTERNO && a.ALIQ_ICMS_VENDA_VAREJO_CONT != null);
                    ViewBag.AlqICMSVendaVContNulasInternos = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONT != null && a.Aliq_Icms_Venda_Varejo_Cont_INTERNO == null);
                    ViewBag.AlqICMSVendaVContNulasExternos = this.analise.Count(a => a.ALIQ_ICMS_VENDA_VAREJO_CONT == null);

                    /*Aliq ICMS ST Venda Varejo Contribuinte - ok*/
                    ViewBag.AlqICMSSTVendaVContMaior = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONT > a.Aliq_Icms_ST_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqICMSSTVendaVContMenor = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONT < a.Aliq_Icms_ST_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqICMSSTVendaVContIguais = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONT == a.Aliq_Icms_ST_Venda_Varejo_Cont_INTERNO && a.ALIQ_ICMS_ST_VENDA_VAREJO_CONT != null);
                    ViewBag.AlqICMSSTVendaVContNulasInternos = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONT != null && a.Aliq_Icms_ST_Venda_Varejo_Cont_INTERNO == null);
                    ViewBag.AlqICMSSTVendaVContNulasExternos = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_VAREJO_CONT == null);


                    /*Aliq ICMS venda ATA - ok*/
                    ViewBag.AlqICMSVataMaior = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA > a.Aliq_Icms_Venda_Ata_Cont_INTERNO);
                    ViewBag.AlqICMSVataMenor = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA < a.Aliq_Icms_Venda_Ata_Cont_INTERNO);
                    ViewBag.AlqICMSVataIgual = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA == a.Aliq_Icms_Venda_Ata_Cont_INTERNO && a.ALIQ_ICMS_VENDA_ATA != null);
                    ViewBag.AlqICMSVataNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA != null && a.Aliq_Icms_Venda_Ata_Cont_INTERNO == null);
                    ViewBag.AlqICMSVataNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA == null);

                    /*Aliq ICMS ST venda ATA - ok*/
                    ViewBag.AlqICMSSTVataMaior = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA > a.Aliq_Icms_ST_Venda_Ata_Cont_INTERNO);
                    ViewBag.AlqICMSSTVataMenor = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA < a.Aliq_Icms_ST_Venda_Ata_Cont_INTERNO);
                    ViewBag.AlqICMSSTVataIgual = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA == a.Aliq_Icms_ST_Venda_Ata_Cont_INTERNO && a.ALIQ_ICMS_ST_VENDA_ATA != null);
                    ViewBag.AlqICMSSTVataNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA != null && a.Aliq_Icms_ST_Venda_Ata_Cont_INTERNO == null);
                    ViewBag.AlqICMSSTVataNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA == null);


                    /*Aliq ICMS Vendo no atacado para Simples Nacional - ok*/
                    ViewBag.AliqICMSVendaAtaSimpNacionalMaior = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL > a.Aliq_Icms_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSVendaAtaSimpNacionalMenor = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL < a.Aliq_Icms_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSVendaAtaSimpNacionalIgual = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL == a.Aliq_Icms_Venda_Ata_Simp_Nacional_INTERNO && a.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null);
                    ViewBag.AliqICMSVendaAtaSimpNacionalNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL != null && a.Aliq_Icms_Venda_Ata_Simp_Nacional_INTERNO == null);
                    ViewBag.AliqICMSVendaAtaSimpNacionalNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_VENDA_ATA_SIMP_NACIONAL == null);


                    /*Aliq ICMS ST Venda no atacado para Simples Nacional - ok*/
                    ViewBag.AliqICMSSTVendaAtaSimpNacionalMaior = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL > a.Aliq_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSSTVendaAtaSimpNacionalMenor = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL < a.Aliq_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSSTVendaAtaSimpNacionalIgual = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL == a.Aliq_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO && a.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null);
                    ViewBag.AliqICMSSTVendaAtaSimpNacionalNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null && a.Aliq_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO == null);
                    ViewBag.AliqICMSVendaAtaSimpNacionalNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_ST_VENDA_ATA_SIMP_NACIONAL == null);

                    /*Redução base de calculo*/
                    /*Aliq Redução da Base Calc ICMS venda CF*/
                    ViewBag.AlqRBCIcmsCFMaior = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL > a.Red_Base_Calc_Icms_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqRBCIcmsCFMenor = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL < a.Red_Base_Calc_Icms_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqRBCIcmsCFIgual = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL == a.Red_Base_Calc_Icms_Venda_Varejo_Cons_Final_INTERNO && a.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null);
                    ViewBag.AlqRBCIcmsCFNullaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL != null && a.Red_Base_Calc_Icms_Venda_Varejo_Cons_Final_INTERNO == null);
                    ViewBag.AlqRBCIcmsCFNullaExterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_VAREJO_CONS_FINAL == null);

                    /*Aliq Redução Base Calc ICMS ST venda CF*/
                    ViewBag.AlqRBCIcmsSTCFMaior = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL > a.Red_Base_Calc_Icms_ST_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqRBCIcmsSTCFMenor = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL < a.Red_Base_Calc_Icms_ST_Venda_Varejo_Cons_Final_INTERNO);
                    ViewBag.AlqRBCIcmsSTCFIguais = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL == a.Red_Base_Calc_Icms_ST_Venda_Varejo_Cons_Final_INTERNO && a.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null);
                    ViewBag.AlqRBCIcmsSTCFNullaInternos = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL != null && a.Red_Base_Calc_Icms_ST_Venda_Varejo_Cons_Final_INTERNO == null);
                    ViewBag.AlqRBCIcmsSTCFNullaExternos = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_VAREJO_CONS_FINAL == null);

                    /*Reedução Base de Calculo venda varejo contribuinte*/
                    ViewBag.AlqRDBCICMSVendaVarContMarior = this.analise.Count(a => a.RED_BASE_CALC_VENDA_VAREJO_CONT > a.Red_Base_Calc_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqRDBCICMSVendaVarContMenor = this.analise.Count(a => a.RED_BASE_CALC_VENDA_VAREJO_CONT < a.Red_Base_Calc_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqRDBCICMSVendaVarContIguais = this.analise.Count(a => a.RED_BASE_CALC_VENDA_VAREJO_CONT == a.Red_Base_Calc_Venda_Varejo_Cont_INTERNO && a.RED_BASE_CALC_VENDA_VAREJO_CONT != null);
                    ViewBag.AlqRDBCICMSVendaVarContNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_VENDA_VAREJO_CONT != null && a.Red_Base_Calc_Venda_Varejo_Cont_INTERNO == null);
                    ViewBag.AlqRDBCICMSVendaVarContNulaExterno = this.analise.Count(a => a.RED_BASE_CALC_VENDA_VAREJO_CONT == null);

                    /*Reedução Base de Calculo ST venda varejo contribuinte*/
                    ViewBag.AlqRDBCICMSSTVendaVarContMarior = this.analise.Count(a => a.RED_BASE_CALC_ST_VENDA_VAREJO_CONT > a.Red_Base_Calc_ST_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqRDBCICMSSTVendaVarContMenor = this.analise.Count(a => a.RED_BASE_CALC_ST_VENDA_VAREJO_CONT < a.Red_Base_Calc_ST_Venda_Varejo_Cont_INTERNO);
                    ViewBag.AlqRDBCICMSSTVendaVarContIgual = this.analise.Count(a => a.RED_BASE_CALC_ST_VENDA_VAREJO_CONT == a.Red_Base_Calc_ST_Venda_Varejo_Cont_INTERNO && a.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null);
                    ViewBag.AlqRDBCICMSSTVendaVarContNulaInterna = this.analise.Count(a => a.RED_BASE_CALC_ST_VENDA_VAREJO_CONT != null && a.Red_Base_Calc_ST_Venda_Varejo_Cont_INTERNO == null);
                    ViewBag.AlqRDBCICMSSTVendaVarContNulaExterna = this.analise.Count(a => a.RED_BASE_CALC_ST_VENDA_VAREJO_CONT == null);


                    /*Red Base Calc  ICMS  venda ATA PARA CONTRIBUINTE*/
                    ViewBag.RedBaseCalcICMSVataMaior = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA > a.Red_Base_Calc_Icms_Venda_Ata_Cont_INTERNO);
                    ViewBag.RedBaseCalcICMSVataMenor = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA < a.Red_Base_Calc_Icms_Venda_Ata_Cont_INTERNO);
                    ViewBag.RedBaseCalcICMSVataIgual = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA == a.Red_Base_Calc_Icms_Venda_Ata_Cont_INTERNO && a.RED_BASE_CALC_ICMS_VENDA_ATA != null);
                    ViewBag.RedBaseCalcICMSVataNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA != null && a.Red_Base_Calc_Icms_Venda_Ata_Cont_INTERNO == null);
                    ViewBag.RedBaseCalcICMSVataNulaExterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA == null);

                    /*Red Base Calc  ICMS ST  venda ATA PARA CONTRIBUINTE*/
                    ViewBag.RedBaseCalcICMSSTVataMaior = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA > a.Red_Base_Calc_Icms_ST_Venda_Ata_Cont_INTERNO);
                    ViewBag.RedBaseCalcICMSSTVataMenor = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA < a.Red_Base_Calc_Icms_ST_Venda_Ata_Cont_INTERNO);
                    ViewBag.RedBaseCalcICMSSTVataIgual = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA == a.Red_Base_Calc_Icms_ST_Venda_Ata_Cont_INTERNO && a.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null);
                    ViewBag.RedBaseCalcICMSSTVataNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA != null && a.Red_Base_Calc_Icms_ST_Venda_Ata_Cont_INTERNO == null);
                    ViewBag.RedBaseCalcICMSSTVataNulaExterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA == null);

                    /*Redução base de calc ICMS venda no atacado para Simples Nacional*/
                    ViewBag.RedBaseCalcICMSVATASimpNacionalMaior = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL > a.Red_Base_Calc_Icms_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSVATASimpNacionalMenor = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL < a.Red_Base_Calc_Icms_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSVATASimpNacionalIgual = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL == a.Red_Base_Calc_Icms_Venda_Ata_Simp_Nacional_INTERNO && a.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null);
                    ViewBag.RedBaseCalcICMSVATASimpNacionalNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL != null && a.Red_Base_Calc_Icms_Venda_Ata_Simp_Nacional_INTERNO == null);
                    ViewBag.RedBaseCalcICMSVATASimpNacionalNulaExterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_VENDA_ATA_SIMP_NACIONAL == null);

                    /*Redução base de calc ICMS ST venda no atacado para Simples Nacional*/
                    ViewBag.RedBaseCalcICMSSTVATASimpNacionalMaior = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL > a.Red_Base_Calc_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSSTVATASimpNacionalMenor = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL < a.Red_Base_Calc_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSSTVATASimpNacionalIgual = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL == a.Red_Base_Calc_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO && a.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null);
                    ViewBag.RedBaseCalcICMSSTVATASimpNacionalNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL != null && a.Red_Base_Calc_Icms_ST_Venda_Ata_Simp_Nacional_INTERNO == null);
                    ViewBag.RedBaseCalcICMSSTVATASimpNacionalNulaExterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_VENDA_ATA_SIMP_NACIONAL == null);

                    /*ICMS DE ENTRADA*/
                    /*Aliquota ICMS Compra de Industria*/
                    ViewBag.AliqICMSCompINDMaior = this.analise.Count(a => a.ALIQ_ICMS_COMP_DE_IND > a.Aliq_Icms_Comp_de_Ind_INTERNO);
                    ViewBag.AliqICMSCompINDMenor = this.analise.Count(a => a.ALIQ_ICMS_COMP_DE_IND < a.Aliq_Icms_Comp_de_Ind_INTERNO);
                    ViewBag.AliqICMSCompINDIgual = this.analise.Count(a => a.ALIQ_ICMS_COMP_DE_IND == a.Aliq_Icms_Comp_de_Ind_INTERNO && a.ALIQ_ICMS_COMP_DE_IND != null);
                    ViewBag.AliqICMSCompINDNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_COMP_DE_IND == null);
                    ViewBag.AliqICMSCompIndNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_COMP_DE_IND != null && a.Aliq_Icms_Comp_de_Ind_INTERNO == null); //onde não for nulo no cliente e nulo no mtx



                    /*Aliquota ICMS ST Compra de Industria*/
                    ViewBag.AliqICMSSTCompINDMaior = this.analise.Count(a => a.ALIQ_ICMS_ST_COMP_DE_IND > a.Aliq_Icms_ST_Comp_de_Ind_INTERNO);
                    ViewBag.AliqICMSSTCompINDMenor = this.analise.Count(a => a.ALIQ_ICMS_ST_COMP_DE_IND < a.Aliq_Icms_ST_Comp_de_Ind_INTERNO);
                    ViewBag.AliqICMSSTCompINDIgual = this.analise.Count(a => a.ALIQ_ICMS_ST_COMP_DE_IND == a.Aliq_Icms_ST_Comp_de_Ind_INTERNO && a.ALIQ_ICMS_ST_COMP_DE_IND != null);
                    ViewBag.AliqICMSSTCompINDNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_ST_COMP_DE_IND == null);
                    ViewBag.AliqICMSSTCompIndNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_ST_COMP_DE_IND != null && a.Aliq_Icms_ST_Compra_de_Ata_INTERNO == null);



                    /*Aliquota ICMS Compra de Atacado*/
                    ViewBag.AliqICMSCompATAMaior = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_ATA > a.Aliq_Icms_Compra_de_Ata_INTERNO);
                    ViewBag.AliqICMSCompATAMenor = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_ATA < a.Aliq_Icms_Compra_de_Ata_INTERNO);
                    ViewBag.AliqICMSCompATAIgual = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_ATA == a.Aliq_Icms_Compra_de_Ata_INTERNO && a.ALIQ_ICMS_COMPRA_DE_ATA != null);
                    ViewBag.AliqICMSCompATANulaInterno = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_ATA != null && a.Aliq_Icms_Compra_de_Ata_INTERNO == null);
                    ViewBag.AliqICMSCompATANulaExterno = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_ATA == null);


                    /*Aliquota ICMS ST Compra de Atacado*/
                    ViewBag.AliqICMSSTCompATAMaior = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_ATA > a.Aliq_Icms_ST_Compra_de_Ata_INTERNO);
                    ViewBag.AliqICMSSTCompATAMenor = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_ATA < a.Aliq_Icms_ST_Compra_de_Ata_INTERNO);
                    ViewBag.AliqICMSSTCompATAIgual = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_ATA == a.Aliq_Icms_ST_Compra_de_Ata_INTERNO && a.ALIQ_ICMS_ST_COMPRA_DE_ATA != null);
                    ViewBag.AliqICMSSTCompATANulaInterno = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_ATA !=null && a.Aliq_Icms_ST_Compra_de_Ata_INTERNO == null);
                    ViewBag.AliqICMSSTCompATANulaExterno = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_ATA == null);

                    /*Aliquota ICMS Compra de Simples nacional*/
                    ViewBag.AliqICMSCompSNMaior = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL > a.Aliq_Icms_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSCompSNMenor = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL < a.Aliq_Icms_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSCompSNIgual = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL == a.Aliq_Icms_Compra_de_Simp_Nacional_INTERNO && a.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null);
                    ViewBag.AliqICMSCompSNNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL != null && a.Aliq_Icms_Compra_de_Simp_Nacional_INTERNO == null);
                    ViewBag.AliqICMSCompSNNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_COMPRA_DE_SIMP_NACIONAL == null);


                    /*Aliquota ICMS ST Compra de Simples nacional*/
                    ViewBag.AliqICMSSTCompSNMaior = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL > a.Aliq_Icms_ST_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSSTCompSNMenor = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL < a.Aliq_Icms_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.AliqICMSSTCompSNIgual = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL == a.Aliq_Icms_Compra_de_Simp_Nacional_INTERNO && a.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null);
                    ViewBag.AliqICMSSTCompSNNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null && a.Aliq_Icms_ST_Compra_de_Simp_Nacional_INTERNO == null);
                    ViewBag.AliqICMSSTCompSNNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_ST_COMPRA_DE_SIMP_NACIONAL == null);


                    /*Aliquota ICMS NFE INDUSTRIA*/
                    ViewBag.AliqICMSNFEIndMaior = this.analise.Count(a => a.ALIQ_ICMS_NFE > a.Aliq_Icms_NFE_INTERNO);
                    ViewBag.AliqICMSNFEIndMenor = this.analise.Count(a => a.ALIQ_ICMS_NFE < a.Aliq_Icms_NFE_INTERNO);
                    ViewBag.AliqICMSNFEIndIguais = this.analise.Count(a => a.ALIQ_ICMS_NFE == a.Aliq_Icms_NFE_INTERNO && a.ALIQ_ICMS_NFE != null);
                    ViewBag.AliqICMSNFEIndNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_NFE != null && a.Aliq_Icms_NFE_INTERNO == null);
                    ViewBag.AliqICMSNFEIndNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_NFE == null);

                    /*Aliquota ICMS NFE SIMPLES NACIONAL*/
                    ViewBag.AliqICMSNFESNMaior = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_SN > a.Aliq_Icms_NFE_For_SN_INTERNO);
                    ViewBag.AliqICMSNFESNMenor = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_SN > a.Aliq_Icms_NFE_For_SN_INTERNO);
                    ViewBag.AliqICMSNFESNIguais = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_SN == a.Aliq_Icms_NFE_For_SN_INTERNO && a.ALIQ_ICMS_NFE_FOR_SN != null);
                    ViewBag.AliqICMSNFESNNullaInterno = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_SN != null && a.Aliq_Icms_NFE_For_SN_INTERNO == null);
                    ViewBag.AliqICMSNFESNNullaExterno = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_SN == null);

                    /*Aliquota ICMS NFE ATACADO*/
                    ViewBag.AliqICMSNFEAtaMaior = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_ATA > a.Aliq_Icms_NFE_For_Ata_INTERNO);
                    ViewBag.AliqICMSNFEAtaMenor = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_ATA < a.Aliq_Icms_NFE_For_Ata_INTERNO);
                    ViewBag.AliqICMSNFEAtaIgual = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_ATA == a.Aliq_Icms_NFE_For_Ata_INTERNO && a.ALIQ_ICMS_NFE_FOR_ATA != null);
                    ViewBag.AliqICMSNFEAtaNulaInterno = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_ATA != null && a.Aliq_Icms_NFE_For_Ata_INTERNO == null);
                    ViewBag.AliqICMSNFEAtaNulaExterno = this.analise.Count(a => a.ALIQ_ICMS_NFE_FOR_ATA == null);


                    /*Redução Base de Calcuulo ICMs de Entrada*/
                    /*Red bae calc ICMS Compra de Industria*/
                    ViewBag.RedBaseCalcICMSCompINDMaior = this.analise.Count(a => a.RED_BASE_CALC_ICMS_COMPRA_DE_IND > a.Red_Base_Calc_Icms_Compra_de_Ind_INTERNO);
                    ViewBag.RedBaseCalcICMSCompINDMenor = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_DE_IND < a.Red_Base_Calc_Icms_Compra_de_Ind_INTERNO);
                    ViewBag.RedBaseCalcICMSCompINDIgual = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_DE_IND == a.Red_Base_Calc_Icms_Compra_de_Ind_INTERNO && a.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null);
                    ViewBag.RedBaseCalcICMSCompINDNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_COMPRA_DE_IND != null && a.Red_Base_Calc_Icms_Compra_de_Ind_INTERNO == null);
                    ViewBag.RedBaseCalcICMSCompINDNulaExterno = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_DE_IND == null);


                    /*Red bae calc ICMS ST Compra de Industria*/
                    ViewBag.RedBaseCalcICMSSTCompINDMaior = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND > a.Red_Base_Calc_Icms_ST_Compra_de_Ind_INTERNO);
                    ViewBag.RedBaseCalcICMSSTCompINDMenor = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND < a.Red_Base_Calc_Icms_ST_Compra_de_Ind_INTERNO);
                    ViewBag.RedBaseCalcICMSSTCompINDIgual = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND == a.Red_Base_Calc_Icms_ST_Compra_de_Ind_INTERNO && a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null);
                    ViewBag.RedBaseCalcICMSSTCompINDNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND != null &&  a.Red_Base_Calc_Icms_ST_Compra_de_Ind_INTERNO == null);
                    ViewBag.RedBaseCalcICMSSTCompINDNulaExterno = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_IND == null);

                    /*Red Base Calc ICMS Compra de Atacado*/
                    ViewBag.RedBaseCalcICMSCompATAMaior = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_DE_ATA > a.Red_Base_Calc_Icms_Compra_de_Ata_INTERNO);
                    ViewBag.RedBaseCalcICMSCompATAMenor = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_DE_ATA < a.Red_Base_Calc_Icms_Compra_de_Ata_INTERNO);
                    ViewBag.RedBaseCalcICMSCompATAIgual = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_DE_ATA == a.Red_Base_Calc_Icms_Compra_de_Ata_INTERNO && a.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null);
                    ViewBag.RedBaseCalcICMSCompATANulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_COMPRA_DE_ATA != null && a.Red_Base_Calc_Icms_Compra_de_Ata_INTERNO == null);
                    ViewBag.RedBaseCalcICMSCompATANulaExterno = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_DE_ATA == null);


                    /*Red Base Calc ICMS ST Compra de Atacado*/
                    ViewBag.RedBaseCalcICMSSTCompATAMaior = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA > a.Red_Base_Calc_Icms_ST_Compra_de_Ata_INTERNO);
                    ViewBag.RedBaseCalcICMSSTCompATAMenor = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA < a.Red_Base_Calc_Icms_ST_Compra_de_Ata_INTERNO);
                    ViewBag.RedBaseCalcICMSSTCompATAIgual = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA == a.Red_Base_Calc_Icms_ST_Compra_de_Ata_INTERNO && a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null);
                    ViewBag.RedBaseCalcICMSSTCompATANulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA != null && a.Red_Base_Calc_Icms_ST_Compra_de_Ata_INTERNO == null);
                    ViewBag.RedBaseCalcICMSSTCompATANulaExterno = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_ATA == null);

                    /*Red Base Calc ICMS Compra de Simples Nacional*/
                    ViewBag.RedBaseCalcICMSCompSNMaior = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL > a.Red_Base_Calc_Icms_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSCompSNMenor = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL < a.Red_Base_Calc_Icms_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSCompSNIgual = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL == a.Red_Base_Calc_Icms_Compra_de_Simp_Nacional_INTERNO && a.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null);
                    ViewBag.RedBaseCalcICMSCompSNNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL != null && a.Red_Base_Calc_Icms_Compra_de_Simp_Nacional_INTERNO == null);
                    ViewBag.RedBaseCalcICMSCompSNNulaExterno = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_COMPRA_SIMP_NACIONAL == null);

                    /*Red Base Calc ICMS ST Compra de Simples Nacional*/
                    ViewBag.RedBaseCalcICMSSTCompSNMaior = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL > a.Red_Base_Calc_Icms_ST_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSSTCompSNMenor = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL < a.Red_Base_Calc_Icms_ST_Compra_de_Simp_Nacional_INTERNO);
                    ViewBag.RedBaseCalcICMSSTCompSNIgual = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL == a.Red_Base_Calc_Icms_ST_Compra_de_Simp_Nacional_INTERNO && a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null);
                    ViewBag.RedBaseCalcICMSSTCompSNNulaInterno = this.analise.Count(a => a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL != null &&  a.Red_Base_Calc_Icms_ST_Compra_de_Simp_Nacional_INTERNO == null);
                    ViewBag.RedBaseCalcICMSSTCompSNNulaExterno = this.analise.Count(a =>   a.RED_BASE_CALC_ICMS_ST_COMPRA_DE_SIMP_NACIONAL == null);


                    /*Pis Cofins*/
                    ViewBag.AlqEPMaior =this.analise.Count(a =>  a.ALIQ_ENTRADA_PIS > a.Aliq_Ent_Pis_INTERNO);
                    ViewBag.AlqEPMenor =this.analise.Count(a =>  a.ALIQ_ENTRADA_PIS < a.Aliq_Ent_Pis_INTERNO);
                    ViewBag.AlqEPIgual =this.analise.Count(a =>  a.ALIQ_ENTRADA_PIS == a.Aliq_Ent_Pis_INTERNO && a.ALIQ_ENTRADA_PIS != null);
                    ViewBag.AlqEPNulaInterno =this.analise.Count(a => a.ALIQ_ENTRADA_PIS != null && a.Aliq_Ent_Pis_INTERNO == null);
                    ViewBag.AlqEPNulaCliente =this.analise.Count(a =>  a.ALIQ_ENTRADA_PIS == null);

                    /*Aiquota Saida PIS*/
                    ViewBag.AlqSPMaior =this.analise.Count(a =>  a.ALIQ_SAIDA_PIS > a.Aliq_Saida_Pis_INTERNO);
                    ViewBag.AlqSPMenor =this.analise.Count(a =>  a.ALIQ_SAIDA_PIS < a.Aliq_Saida_Pis_INTERNO);
                    ViewBag.AlqSPIguais =this.analise.Count(a =>  a.ALIQ_SAIDA_PIS == a.Aliq_Saida_Pis_INTERNO && a.ALIQ_SAIDA_PIS != null);
                    ViewBag.AlqSPNulaInterno =this.analise.Count(a => a.ALIQ_SAIDA_PIS != null && a.Aliq_Saida_Pis_INTERNO == null);
                    ViewBag.AlqSPNulaCliente =this.analise.Count(a =>  a.ALIQ_SAIDA_PIS == null);

                    /*Cofins*/
                    /*AlqEntradaCofins*/
                    ViewBag.AlqEntradaCofinsMaior =this.analise.Count(a =>  a.ALIQ_ENTRADA_COFINS > a.Aliq_Ent_Cofins_INTERNO);
                    ViewBag.AlqEntradaCofinsMenor =this.analise.Count(a =>  a.ALIQ_ENTRADA_COFINS < a.Aliq_Ent_Cofins_INTERNO);
                    ViewBag.AlqEntradaCofinsIguais =this.analise.Count(a =>  a.ALIQ_ENTRADA_COFINS == a.Aliq_Ent_Cofins_INTERNO && a.ALIQ_ENTRADA_COFINS != null);
                    ViewBag.AliqEntradaCofinsNullasInternas =this.analise.Count(a => a.ALIQ_ENTRADA_COFINS != null && a.Aliq_Ent_Cofins_INTERNO == null);
                    ViewBag.AliqEntradaCofinsNullasExternas =this.analise.Count(a =>  a.ALIQ_ENTRADA_COFINS == null);

                    /*Aliquota saida cofins*/
                    ViewBag.AlqSaidaCofinsMaior =this.analise.Count(a =>  a.ALIQ_SAIDA_COFINS > a.Aliq_Saida_Cofins_INTERNO);
                    ViewBag.AlqSaidaCofinsMenor =this.analise.Count(a =>  a.ALIQ_SAIDA_COFINS < a.Aliq_Saida_Cofins_INTERNO);
                    ViewBag.AlqSaidaCofinsIguais =this.analise.Count(a =>  a.ALIQ_SAIDA_COFINS == a.Aliq_Saida_Cofins_INTERNO && a.ALIQ_SAIDA_COFINS != null);
                    ViewBag.AlqSCNullaInterna =this.analise.Count(a => a.ALIQ_SAIDA_COFINS != null && a.Aliq_Saida_Cofins_INTERNO == null);
                    ViewBag.AlqSCNullaCliente =this.analise.Count(a =>  a.ALIQ_SAIDA_COFINS == null);



                    /*CST de Saída*/
                    /*Entrada PIS Cofins (ACERTADO ALTERAÇÃO: TIRAR OS NULOS DA CONTA)
             Dessa forma ele compara os dois registros, se em um deles o valor
            for nulo ele retira da contagem, assim somente os registros realmente
            diferentes são analisados e a contagem de nulos no cliente e nulos na
            matriz ficará correta
            A comparação de igualdade acontece o mesmo, ele deve tirar os registros
            que forem nulos tanto no cliente quanto no mtx
             */
                    ViewBag.CstEntradaPisCofinsNulaCliente = this.analise.Count(a => a.CST_ENTRADA_PIS_COFINS == null);
                    ViewBag.CstEntradaPisCofinsNulaMtx = this.analise.Count(a => a.Cst_Entrada_PisCofins_INTERNO == null);
                    ViewBag.CstEntradaPisCofinsIgual = this.analise.Count(a => a.CST_ENTRADA_PIS_COFINS == a.Cst_Entrada_PisCofins_INTERNO && a.CST_ENTRADA_PIS_COFINS != null && a.Cst_Entrada_PisCofins_INTERNO != null);
                    ViewBag.CstEntradaPisCofinsDife = this.analise.Count(a => a.CST_ENTRADA_PIS_COFINS != a.Cst_Entrada_PisCofins_INTERNO && a.CST_ENTRADA_PIS_COFINS != null && a.Cst_Entrada_PisCofins_INTERNO != null);

                    /*Saída PIS Cofins*/
                    ViewBag.CstSaidaPisCofinsNulaCliente = this.analise.Count(a =>  a.CST_SAIDA_PIS_COFINS == null);
                    ViewBag.CstSaidaPisCofinsNulaMtx = this.analise.Count(a =>  a.Cst_Saida_PisCofins_INTERNO == null);
                    ViewBag.CstSaidaPisCofinsIgual = this.analise.Count(a =>  a.CST_SAIDA_PIS_COFINS == a.Cst_Saida_PisCofins_INTERNO && a.CST_SAIDA_PIS_COFINS != null && a.Cst_Saida_PisCofins_INTERNO != null);
                    ViewBag.CstSaidaPisCofinsDife = this.analise.Count(a =>  a.CST_SAIDA_PIS_COFINS != a.Cst_Saida_PisCofins_INTERNO && a.CST_SAIDA_PIS_COFINS != null && a.Cst_Saida_PisCofins_INTERNO != null);

                    /*CST Venda Varejo Consumidor Final*/
                    ViewBag.CstVendaVarejoCFNulaCliente = this.analise.Count(a =>  a.CST_VENDA_VAREJO_CONS_FINAL == null);
                    ViewBag.CstVendaVarejoCFNulaMtx = this.analise.Count(a =>  a.Cst_Venda_Varejo_Cons_Final_INTERNO == null);
                    ViewBag.CstVendaVarejoCFIgual = this.analise.Count(a =>  a.CST_VENDA_VAREJO_CONS_FINAL == a.Cst_Venda_Varejo_Cons_Final_INTERNO && a.CST_VENDA_VAREJO_CONS_FINAL != null && a.Cst_Venda_Varejo_Cons_Final_INTERNO != null);
                    ViewBag.CstVendaVarejoCFDif = this.analise.Count(a =>  a.CST_VENDA_VAREJO_CONS_FINAL != a.Cst_Venda_Varejo_Cons_Final_INTERNO && a.CST_VENDA_VAREJO_CONS_FINAL != null && a.Cst_Venda_Varejo_Cons_Final_INTERNO != null);

                    /*CST Venda Varejo Contribuinte*/
                    ViewBag.CstVendaVarejoContNulaCliente = this.analise.Count(a =>  a.CST_VENDA_VAREJO_CONT == null);
                    ViewBag.CstVendaVarejoContNulaMtx = this.analise.Count(a =>  a.Cst_Venda_Varejo_Cont_INTERNO == null);
                    ViewBag.CstVendaVarejoContIgual = this.analise.Count(a =>  a.CST_VENDA_VAREJO_CONT == a.Cst_Venda_Varejo_Cont_INTERNO && a.CST_VENDA_VAREJO_CONT != null && a.Cst_Venda_Varejo_Cont_INTERNO != null);
                    ViewBag.CstVendaVarejoContDif = this.analise.Count(a =>  a.CST_VENDA_VAREJO_CONT != a.Cst_Venda_Varejo_Cont_INTERNO && a.CST_VENDA_VAREJO_CONT != null && a.Cst_Venda_Varejo_Cont_INTERNO != null);


                    /*CST Venda Atacado Contribuinte*/
                    ViewBag.CstVendaAtaContNulaCliente = this.analise.Count(a =>  a.CST_VENDA_ATA == null);
                    ViewBag.CstVendaAtaContNulaMtx = this.analise.Count(a =>  a.Cst_Venda_Ata_Cont_INTERNO == null);
                    ViewBag.CstVendaAtaContIgual = this.analise.Count(a =>  a.CST_VENDA_ATA == a.Cst_Venda_Ata_Cont_INTERNO && a.CST_VENDA_ATA != null && a.Cst_Venda_Ata_Cont_INTERNO != null);
                    ViewBag.CstVendaAtaContDif = this.analise.Count(a =>  a.CST_VENDA_ATA != a.Cst_Venda_Ata_Cont_INTERNO && a.CST_VENDA_ATA != null && a.Cst_Venda_Ata_Cont_INTERNO != null);


                    /*CST Venda Atacado Simples Nacional*/
                    ViewBag.CstVendaAtaSNNulaCliente = this.analise.Count(a =>  a.CST_VENDA_ATA_SIMP_NACIONAL == null);
                    ViewBag.CstVendaAtaSNNulaMtx = this.analise.Count(a =>  a.Cst_Venda_Ata_Simp_Nacional_INTERNO == null);
                    ViewBag.CstVendaAtaSNIgual = this.analise.Count(a =>  a.CST_VENDA_ATA_SIMP_NACIONAL == a.Cst_Venda_Ata_Simp_Nacional_INTERNO && a.CST_VENDA_ATA_SIMP_NACIONAL != null && a.Cst_Venda_Ata_Simp_Nacional_INTERNO != null);
                    ViewBag.CstVendaAtaSNDif = this.analise.Count(a =>  a.CST_VENDA_ATA_SIMP_NACIONAL != a.Cst_Venda_Ata_Simp_Nacional_INTERNO && a.CST_VENDA_ATA_SIMP_NACIONAL != null && a.Cst_Venda_Ata_Simp_Nacional_INTERNO != null);


                    /*Cst de Entradas - compras*/
                    /*Entrada PIS Cofins (ACERTADO ALTERAÇÃO: TIRAR OS NULOS DA CONTA)
             Dessa forma ele compara os dois registros, se em um deles o valor
            for nulo ele retira da contagem, assim somente os registros realmente
            diferentes são analisados e a contagem de nulos no cliente e nulos na
            matriz ficará correta
            A comparação de igualdade acontece o mesmo, ele deve tirar os registros
            que forem nulos tanto no cliente quanto no mtx
             */

                    /*CST Compra de industria*/
                    ViewBag.CstCompraIndustriaNulaCliente =this.analise.Count(a =>  a.CST_COMPRA_DE_IND == null); //nula do cliente
                    ViewBag.CstCompraIndustriaNulaMtx =this.analise.Count(a =>  a.Cst_Compra_de_Ind_INTERNO == null); //nula no mtx
                    ViewBag.CstCompraIndustriaIgual =this.analise.Count(a =>  a.CST_COMPRA_DE_IND == a.Cst_Compra_de_Ind_INTERNO && a.CST_COMPRA_DE_IND != null && a.Cst_Compra_de_Ind_INTERNO != null); //compara tirando os nulos
                    ViewBag.CstCompraIndustriaDif =this.analise.Count(a =>  a.CST_COMPRA_DE_IND != a.Cst_Compra_de_Ind_INTERNO && a.CST_COMPRA_DE_IND != null && a.Cst_Compra_de_Ind_INTERNO != null);

                    /*CST Compra de Atacado*/
                    ViewBag.CstCompraAtacadoNulaCliente =this.analise.Count(a =>  a.CST_COMPRA_DE_ATA == null); //nula do cliente
                    ViewBag.CstCompraAtacadoNulaMtx =this.analise.Count(a =>  a.Cst_Compra_de_Ata_INTERNO == null); //nula no mtx
                    ViewBag.CstCompraAtacadoIgual =this.analise.Count(a =>  a.CST_COMPRA_DE_ATA == a.Cst_Compra_de_Ata_INTERNO && a.CST_COMPRA_DE_ATA != null && a.Cst_Compra_de_Ata_INTERNO != null); //compara tirando os nulos
                    ViewBag.CstCompraAtacadoDif =this.analise.Count(a =>  a.CST_COMPRA_DE_ATA != a.Cst_Compra_de_Ata_INTERNO && a.CST_COMPRA_DE_ATA != null && a.Cst_Compra_de_Ata_INTERNO != null);

                    /*CST Compra de Simples nacional*/
                    ViewBag.CstCompraSNNulaCliente =this.analise.Count(a =>  a.CST_COMPRA_DE_SIMP_NACIONAL == null); //nula do cliente
                    ViewBag.CstCompraSNNulaMtx =this.analise.Count(a =>  a.Cst_Compra_de_Simp_Nacional_INTERNO == null); //nula no mtx
                    ViewBag.CstCompraSNIgual =this.analise.Count(a =>  a.CST_COMPRA_DE_SIMP_NACIONAL == a.Cst_Compra_de_Simp_Nacional_INTERNO && a.CST_COMPRA_DE_SIMP_NACIONAL != null && a.Cst_Compra_de_Simp_Nacional_INTERNO != null); //compara tirando os nulos
                    ViewBag.CstCompraSNDif =this.analise.Count(a =>  a.CST_COMPRA_DE_SIMP_NACIONAL != a.Cst_Compra_de_Simp_Nacional_INTERNO && a.CST_COMPRA_DE_SIMP_NACIONAL != null && a.Cst_Compra_de_Simp_Nacional_INTERNO != null);


                    /*Tres juntos: CST_NFE_IND, CST_NFE_ATA, CST_NFE_SN*/
                    /*CST NFE Industria*/
                    ViewBag.CstNFEIndNulaCliente =this.analise.Count(a =>  a.CST_DA_NFE_DA_IND_FORN == null); //nula do cliente
                    ViewBag.CstNFEIndNulaMtx =this.analise.Count(a =>  a.Cst_da_Nfe_da_Ind_FORN_INTERNO == null); //nula no mtx
                    ViewBag.CstNFEIndIgual =this.analise.Count(a =>  a.CST_DA_NFE_DA_IND_FORN == a.Cst_da_Nfe_da_Ind_FORN_INTERNO && a.CST_DA_NFE_DA_IND_FORN != null && a.Cst_da_Nfe_da_Ind_FORN_INTERNO != null); //compara tirando os nulos
                    ViewBag.CstNFEIndDif =this.analise.Count(a =>  a.CST_DA_NFE_DA_IND_FORN != a.Cst_da_Nfe_da_Ind_FORN_INTERNO && a.CST_DA_NFE_DA_IND_FORN != null && a.Cst_da_Nfe_da_Ind_FORN_INTERNO != null);

                    /*CST NFE Atacado*/
                    ViewBag.CstNFEAtaNulaCliente =this.analise.Count(a =>  a.CST_DA_NFE_DE_ATA_FORN == null); //nula do cliente
                    ViewBag.CstNFEAtaNulaMtx =this.analise.Count(a =>  a.Cst_da_Nfe_de_Ata_FORn_INTERNO == null); //nula no mtx
                    ViewBag.CstNFEAtaIgual =this.analise.Count(a =>  a.CST_DA_NFE_DE_ATA_FORN == a.Cst_da_Nfe_de_Ata_FORn_INTERNO && a.CST_DA_NFE_DE_ATA_FORN != null && a.Cst_da_Nfe_de_Ata_FORn_INTERNO != null); //compara tirando os nulos
                    ViewBag.CstNFEAtaDif =this.analise.Count(a =>  a.CST_DA_NFE_DE_ATA_FORN != a.Cst_da_Nfe_de_Ata_FORn_INTERNO && a.CST_DA_NFE_DE_ATA_FORN != null && a.Cst_da_Nfe_de_Ata_FORn_INTERNO != null);

                    /*CSosnt NFE For Simples Nacional*/
                    ViewBag.CstNFESNNulaCliente =this.analise.Count(a =>  a.CSOSNT_DANFE_DOS_NFOR == null); //nula do cliente
                    ViewBag.CstNFESNNulaMtx =this.analise.Count(a =>  a.CsosntdaNfedoSnFOR_INTERNO == null); //nula no mtx
                    ViewBag.CstNFESNIgual =this.analise.Count(a =>  a.CSOSNT_DANFE_DOS_NFOR == a.CsosntdaNfedoSnFOR_INTERNO && a.CSOSNT_DANFE_DOS_NFOR != null && a.CsosntdaNfedoSnFOR_INTERNO != null); //compara tirando os nulos
                    ViewBag.CstNFESNDif =this.analise.Count(a =>  a.CSOSNT_DANFE_DOS_NFOR != a.CsosntdaNfedoSnFOR_INTERNO && a.CSOSNT_DANFE_DOS_NFOR != null && a.CsosntdaNfedoSnFOR_INTERNO != null);

                    /*Analise de Produtos*/
                    /*Descrição*/
                    int iguais = this.analise.Count(a => a.PRODUTO_DESCRICAO == a.Descricao_INTERNO);
                    int nulas = this.analise.Count(a => a.PRODUTO_DESCRICAO == "" || a.PRODUTO_DESCRICAO == null);
                    iguais = iguais - nulas;

                    ViewBag.ProdDescIguais = iguais;
                    ViewBag.ProdDescNull = nulas;
                    ViewBag.ProdDescDif = this.analise.Count(a => a.PRODUTO_DESCRICAO != a.Descricao_INTERNO);

                    /*Cest*/
                    int comCest = this.analise.Count(a => a.PRODUTO_CEST != null); //possuem cest
                    int semCest = this.analise.Count(a => a.PRODUTO_CEST == null); // não possuem cest
                                                                              //iguais = iguais - nulas;

                    ViewBag.ProdCESTCom = comCest;
                    ViewBag.ProdCESTSem = semCest;
                    //ViewBag.ProdCESTDif = analise.Count(a => a.PRODUTO_CEST != a.Cest_INTERNO);

                    /*Ncm*/
                    int comNCM = this.analise.Count(a => a.PRODUTO_NCM != null); //possuem cest
                    int semNCM = this.analise.Count(a => a.PRODUTO_NCM == null); //possuem cest
                                                                            

                    ViewBag.ProdNCMCom = comNCM;
                    ViewBag.ProdNCMSem = semNCM;
                   


                }
            }
            


            //this.analise = (from a in db.Analise_Tributaria where a.CNPJ_EMPRESA == empresa.cnpj select a).ToList();

            return View();
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