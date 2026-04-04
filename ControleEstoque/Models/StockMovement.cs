using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControleEstoque.Models;

public class StockMovement
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The product is required.")]
    [Display(Name = "Product")]
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required]
    [Display(Name = "Type")]
    public MovementType Type { get; set; }

    [Required(ErrorMessage = "The quantity is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "The quantity must be greater than zero.")]
    [Display(Name = "Quantity")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "The date is required.")]
    [Display(Name = "Date")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Exit Reason")]
    public ExitReason? ExitReason { get; set; }

    [Display(Name = "Supplier")]
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Range(0, double.MaxValue, ErrorMessage = "The unit cost cannot be negative.")]
    [Display(Name = "Unit Cost")]
    [DataType(DataType.Currency)]
    public decimal? UnitCost { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}
