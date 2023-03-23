using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MtxApi.Models
{
    [Table("produtos")]
    public class Produto
    {
        [Key]
        public int Id { get; set; }

       
        [Column("Cod_Barras")]
        public long? codBarras { get; set; }

        [Column("Descricao")]
        public string descricao { get; set; }

        [Column("Cest")]
        public string cest { get; set; }

        [Column("NCM")]
        public string ncm { get; set; }

        [Column("DataCad")]
        public DateTime? dataCad { get; set; }

        [Column("DataAlt")]
        public DateTime? dataAlt { get; set; }

        [ForeignKey("categoriaProduto")]
        [Column("Id_Categoria")]
        public int? idCategoria { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual CategoriaProduto categoriaProduto { get; set; }//relacionamento com a categoria

        [Column("Status")]
        public Nullable<sbyte> status { get; set; }

        [Column("AuditadoNCM")]
        public Nullable<sbyte> auditadoNCM { get; set; }

        [Column("Cod_Barras_Gerado")]
        public string CodBarrasGErado { get; set; }
    }
}