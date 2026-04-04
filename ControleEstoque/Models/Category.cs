using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The name is required.")]
    [StringLength(100, ErrorMessage = "The name must be at most 100 characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description must be at most 500 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    // ID of this category in an external store
    [StringLength(200)]
    [Display(Name = "External ID")]
    public string? ExternalId { get; set; }

    // Which platform the ExternalId comes from (e.g. "nuvemshop", "shopify")
    [StringLength(50)]
    [Display(Name = "External ID Source")]
    public string? ExternalIdSource { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
