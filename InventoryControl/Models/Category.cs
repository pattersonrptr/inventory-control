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

    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<CategoryExternalMapping> ExternalMappings { get; set; } = new List<CategoryExternalMapping>();

    public string FullName => Parent is not null ? $"{Parent.Name} > {Name}" : Name;
}
