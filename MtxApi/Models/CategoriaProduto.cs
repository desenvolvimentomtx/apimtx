using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MtxApi.Models
{
    [Table("categorias_produto")]
    public class CategoriaProduto
    {
        [Key]
        public int id { get; set; }

        [Display(Name = "Descrição")]
        [Column("CategoriaDescricao")]
        public string descricao { get; set; }

        public virtual ICollection<Produto> produtos { get; set; }
    }
}