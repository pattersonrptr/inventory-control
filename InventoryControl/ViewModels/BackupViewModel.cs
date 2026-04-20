namespace InventoryControl.ViewModels;

public class BackupViewModel
{
    public bool IsRcloneConfigured { get; set; }
    public string? ConfiguredRemotes { get; set; }
    public string RemotePath { get; set; } = "";
    public bool RcloneAvailable { get; set; }
}
