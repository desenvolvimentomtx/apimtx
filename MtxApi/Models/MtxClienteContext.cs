using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace MtxApi.Models
{
    public class MtxClienteContext : DbContext
    {
        public MtxClienteContext() : base("name=MtxClienteContext")
        {
        }
        //acesso ao model Empresa do projeto Matriz
        public System.Data.Entity.DbSet<Models.Empresa> Empresas { get; set; }
        public System.Data.Entity.DbSet<Models.TributacaoEmpresa> TributacaoEmpresas { get; set; }
        public System.Data.Entity.DbSet<Models.CategoriaProduto> CategoriasProdutos { get; set; }
        public System.Data.Entity.DbSet<Models.Produto> Produtos { get; set; }
        public System.Data.Entity.DbSet<Models.Usuario> Usuarios { get; set; }
        public System.Data.Entity.DbSet<Models.Nivel> Niveis { get; set; }

        public virtual System.Data.Entity.DbSet<SoftwareHouse> SoftwareHouses { get; set; }
        public virtual System.Data.Entity.DbSet<Token> Tokens { get; set; }
        public virtual DbSet<Models.AnaliseTributaria> Analise_Tributaria { get; set; } //Vitor
        public virtual DbSet<Models.TtributacaoGeralViewSN> TributacaoViewSN { get; set; }
        
        public virtual DbSet<Models.CstPisCofinsEntrada> CstPisCofinsEntradas { get; set; }
        
        public virtual DbSet<Models.CstPisCofinsSaida> CstPisCofinsSaidas { get; set; }
        public virtual DbSet<Models.TributacaoSN> TributacaoSN { get; set; }
        
        public virtual DbSet<Models.Legislacao> Legislacoes { get; set; }

        public virtual DbSet<Models.NaturezaReceita> NaturezaReceitas { get; set; }

        public virtual DbSet<Models.ClassificacaoNatReceita> ClassificacaoNatReceitas { get; set; }

        public virtual DbSet<Models.SetorProdutos> SetorProdutos { get; set; }
        


        public virtual DbSet<Tributacao> Tributacoes { get; set; }
    }
}