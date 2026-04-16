namespace InventoryControl.Integrations.Abstractions;

public class ExternalProductVariant
{
    public string ExternalId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
