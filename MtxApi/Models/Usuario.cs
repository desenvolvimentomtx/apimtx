using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace MtxApi.Models
{
    [Table("usuario")]
    public class Usuario
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int id { get; set; }

        [Column("nome")]
        public string nome { get; set; }

       
        [RegularExpression(@"\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*", ErrorMessage = "Insira uma e-mail válido")]
        [Column("email")]
        public string email { get; set; }

       
        [Column("sexo")]
        public string sexo { get; set; }

       
        [Column("logradouro")]
        public string logradouro { get; set; }

      
        [Column("numero")]
        public string numero { get; set; }

       
        [Column("cep")]
        public string cep { get; set; }

       
        [Column("senha")]
        public string senha { get; set; }

        [Column("ativo")]
        public sbyte ativo { get; set; }

        [Column("datacad")]
        public DateTime dataCad { get; set; }

        [Column("dataalt")]
        public DateTime dataAlt { get; set; }

       
        [Column("idnivel")]
        [ForeignKey("nivel")]
        public int idNivel { get; set; }

        [Column("telefone")]
        public string telefone { get; set; }

       
        [Column("cidade")]
        public string cidade { get; set; }

       
        [Column("estado")]
        public string estado { get; set; }

        
        [Column("primeiro_acesso")]
        public sbyte primeiro_acesso { get; set; }

        [ForeignKey("empresa")]
        [Column("idempresa")]
        public int idEmpresa { get; set; }

        [JsonIgnore]
        public virtual Empresa empresa { get; set; }

        [JsonIgnore]
        public virtual Nivel nivel { get; set; }



    }
}