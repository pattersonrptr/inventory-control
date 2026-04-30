using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Domain.Products;

public class ProductImage
{
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required(ErrorMessage = "O caminho da imagem é obrigatório.")]
    [StringLength(500)]
    [Display(Name = "Imagem")]
    public string ImagePath { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Texto Alternativo")]
    public string? AltText { get; set; }

    [Display(Name = "Ordem de Exibição")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Imagem Principal")]
    public bool IsPrimary { get; set; }

    [StringLength(64)]
    public string? ExternalImageId { get; set; }

    [StringLength(500)]
    public string? ExternalUrl { get; set; }
}
