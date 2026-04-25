using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Domain.Products;

public class ProductExternalMapping
{
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

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
