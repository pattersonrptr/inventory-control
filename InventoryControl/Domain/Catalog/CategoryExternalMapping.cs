using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Domain.Catalog;

public class CategoryExternalMapping
{
    public int Id { get; set; }

    [Required]
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string StoreName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ExternalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Platform { get; set; } = string.Empty;
}
