using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventoryControl.Domain.Catalog;
using InventoryControl.Domain.Products;

namespace InventoryControl.Domain.Stock;

public class StockMovement
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O produto é obrigatório.")]
    [Display(Name = "Produto")]
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required]
    [Display(Name = "Tipo")]
    public MovementType Type { get; set; }

    [Required(ErrorMessage = "A quantidade é obrigatória.")]
    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    [Display(Name = "Quantidade")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "A data é obrigatória.")]
    [Display(Name = "Data")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Motivo da Saída")]
    public ExitReason? ExitReason { get; set; }

    [Display(Name = "Fornecedor")]
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Range(0, double.MaxValue, ErrorMessage = "O custo unitário não pode ser negativo.")]
    [Display(Name = "Custo Unitário")]
    [DataType(DataType.Currency)]
    public decimal? UnitCost { get; set; }

    [StringLength(500)]
    [Display(Name = "Observações")]
    public string? Notes { get; set; }
}
