using System.Text.Json.Serialization;

namespace InventoryControl.Infrastructure.Integrations.Nuvemshop.Models;

public class NuvemshopOrderProduct
{
    [JsonPropertyName("product_id")]
    public long ProductId { get; set; }

    [JsonPropertyName("variant_id")]
    public long VariantId { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";
}
