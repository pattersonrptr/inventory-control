using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Domain.Integrations;

public class SyncState
{
    [Key]
    public string Key { get; set; } = string.Empty;

    public DateTime LastProcessedAt { get; set; }
}
