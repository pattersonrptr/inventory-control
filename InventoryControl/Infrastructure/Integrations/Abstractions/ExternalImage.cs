namespace InventoryControl.Infrastructure.Integrations.Abstractions;

public class ExternalImage
{
    public string ExternalId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Position { get; set; }
}
