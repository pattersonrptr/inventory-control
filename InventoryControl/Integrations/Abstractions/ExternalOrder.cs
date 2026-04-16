namespace InventoryControl.Integrations.Abstractions;

public class ExternalOrder
{
    public string ExternalOrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ExternalOrderItem> Items { get; set; } = new();
}
