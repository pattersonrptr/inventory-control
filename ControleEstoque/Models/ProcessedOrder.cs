using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.Models;

public class ProcessedOrder
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string ExternalOrderId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; } = DateTime.Now;
}
