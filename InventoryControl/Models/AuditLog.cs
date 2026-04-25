using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Models;

public class AuditLog
{
    public long Id { get; set; }

    [Required]
    [StringLength(256)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? EntityId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [StringLength(4000)]
    public string? OldValues { get; set; }

    [StringLength(4000)]
    public string? NewValues { get; set; }
}
