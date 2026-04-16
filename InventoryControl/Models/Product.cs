using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryControl.Models;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(200, ErrorMessage = "O nome deve ter no máximo 200 caracteres.")]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "O preço de custo é obrigatório.")]
    [Column(TypeName = "decimal(10,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O preço de custo deve ser maior que zero.")]
    [Display(Name = "Preço de Custo")]
    [DataType(DataType.Currency)]
    public decimal CostPrice { get; set; }

    [Required(ErrorMessage = "O preço de venda é obrigatório.")]
    [Column(TypeName = "decimal(10,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O preço de venda deve ser maior que zero.")]
    [Display(Name = "Preço de Venda")]
    [DataType(DataType.Currency)]
    public decimal SellingPrice { get; set; }

    [Display(Name = "Estoque Atual")]
    public int CurrentStock { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O estoque mínimo não pode ser negativo.")]
    [Display(Name = "Estoque Mínimo")]
    public int MinimumStock { get; set; }

    [Required(ErrorMessage = "A categoria é obrigatória.")]
    [Display(Name = "Categoria")]
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    [Required(ErrorMessage = "O fornecedor é obrigatório.")]
    [Display(Name = "Fornecedor")]
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    // Universal product code — platform-agnostic
    [StringLength(100)]
    [Display(Name = "SKU")]
    public string? Sku { get; set; }

    // ID of this product in an external store
    [StringLength(200)]
    [Display(Name = "ID Externo")]
    public string? ExternalId { get; set; }

    // Which platform the ExternalId comes from (e.g. "nuvemshop", "shopify")
    [StringLength(50)]
    [Display(Name = "Origem do ID Externo")]
    public string? ExternalIdSource { get; set; }

    [StringLength(100)]
    [Display(Name = "Marca")]
    public string? Brand { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    [NotMapped]
    [Display(Name = "Imagem")]
    public string? PrimaryImagePath => Images?.FirstOrDefault(i => i.IsPrimary)?.ImagePath
                                       ?? Images?.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImagePath;

    [NotMapped]
    public bool IsBelowMinimumStock => CurrentStock <= MinimumStock;
}
