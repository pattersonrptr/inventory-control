using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Models;

public class SyncState
{
    [Key]
    public string Key { get; set; } = string.Empty;

    public DateTime LastProcessedAt { get; set; }
}
