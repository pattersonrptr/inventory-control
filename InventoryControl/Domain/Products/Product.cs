using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventoryControl.Domain.Catalog;
using InventoryControl.Domain.Products.Events;
using InventoryControl.Domain.Shared;
using InventoryControl.Domain.Stock;

namespace InventoryControl.Domain.Products;

public class Product : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();


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

    [StringLength(100)]
    [Display(Name = "SKU")]
    public string? Sku { get; set; }

    [StringLength(100)]
    [Display(Name = "Marca")]
    public string? Brand { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductExternalMapping> ExternalMappings { get; set; } = new List<ProductExternalMapping>();

    [NotMapped]
    [Display(Name = "Imagem")]
    public string? PrimaryImagePath => Images?.FirstOrDefault(i => i.IsPrimary)?.ImagePath
                                       ?? Images?.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImagePath;

    [NotMapped]
    public bool IsBelowMinimumStock => CurrentStock <= MinimumStock;

    [NotMapped]
    public decimal Margin => SellingPrice > 0
        ? Math.Round((SellingPrice - CostPrice) / SellingPrice * 100, 2)
        : 0m;

    public void ApplyEntry(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        CurrentStock += quantity;
        _domainEvents.Add(new StockChanged(Id, CurrentStock));
    }

    public void ApplyExit(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (quantity > CurrentStock)
            throw new InsufficientStockException(Name, CurrentStock, quantity);

        var wasBelowMinimum = IsBelowMinimumStock;
        CurrentStock -= quantity;

        _domainEvents.Add(new StockChanged(Id, CurrentStock));

        if (!wasBelowMinimum && IsBelowMinimumStock)
            _domainEvents.Add(new ProductWentBelowMinimum(Id, Name, CurrentStock, MinimumStock));
    }
}
