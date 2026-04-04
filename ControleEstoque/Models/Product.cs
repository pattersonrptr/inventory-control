using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControleEstoque.Models;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The name is required.")]
    [StringLength(200, ErrorMessage = "The name must be at most 200 characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description must be at most 500 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "The cost price is required.")]
    [Column(TypeName = "decimal(10,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "The cost price must be greater than zero.")]
    [Display(Name = "Cost Price")]
    [DataType(DataType.Currency)]
    public decimal CostPrice { get; set; }

    [Required(ErrorMessage = "The selling price is required.")]
    [Column(TypeName = "decimal(10,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "The selling price must be greater than zero.")]
    [Display(Name = "Selling Price")]
    [DataType(DataType.Currency)]
    public decimal SellingPrice { get; set; }

    [Display(Name = "Current Stock")]
    public int CurrentStock { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "The minimum stock cannot be negative.")]
    [Display(Name = "Minimum Stock")]
    public int MinimumStock { get; set; }

    [Required(ErrorMessage = "The category is required.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    [Required(ErrorMessage = "The supplier is required.")]
    [Display(Name = "Supplier")]
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    // Universal product code — platform-agnostic
    [StringLength(100)]
    [Display(Name = "SKU")]
    public string? Sku { get; set; }

    // ID of this product in an external store
    [StringLength(200)]
    [Display(Name = "External ID")]
    public string? ExternalId { get; set; }

    // Which platform the ExternalId comes from (e.g. "nuvemshop", "shopify")
    [StringLength(50)]
    [Display(Name = "External ID Source")]
    public string? ExternalIdSource { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    [NotMapped]
    public bool IsBelowMinimumStock => CurrentStock <= MinimumStock;
}
