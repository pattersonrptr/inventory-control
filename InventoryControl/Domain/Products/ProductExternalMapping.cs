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

    public ExternalSyncStatus SyncStatus { get; set; } = ExternalSyncStatus.Synced;

    [MaxLength(1000)]
    public string? LastSyncError { get; set; }

    public DateTime? LastSyncAttemptAt { get; set; }

    public bool HasConflict { get; set; }

    [MaxLength(2000)]
    public string? ConflictDetails { get; set; }
}
