namespace ControleEstoque.Integrations.Abstractions;

public class ExternalOrderItem
{
    public string ExternalProductId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
