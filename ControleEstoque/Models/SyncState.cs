using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.Models;

public class SyncState
{
    [Key]
    public string Key { get; set; } = string.Empty;

    public DateTime LastProcessedAt { get; set; }
}
