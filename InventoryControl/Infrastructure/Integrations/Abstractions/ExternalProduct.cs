namespace InventoryControl.Infrastructure.Integrations.Abstractions;

public class ExternalProduct
{
    public string ExternalId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public List<ExternalProductVariant> Variants { get; set; } = new();
}
