using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }

    [Display(Name = "Categoria Pai")]
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    // ID of this category in an external store
    [StringLength(200)]
    [Display(Name = "ID Externo")]
    public string? ExternalId { get; set; }

    // Which platform the ExternalId comes from (e.g. "nuvemshop", "shopify")
    [StringLength(50)]
    [Display(Name = "Origem do ID Externo")]
    public string? ExternalIdSource { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public string FullName => Parent is not null ? $"{Parent.Name} > {Name}" : Name;
}
