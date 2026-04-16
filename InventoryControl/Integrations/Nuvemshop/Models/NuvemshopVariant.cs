using System.Text.Json.Serialization;

namespace InventoryControl.Integrations.Nuvemshop.Models;

public class NuvemshopVariant
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";

    [JsonPropertyName("stock")]
    public int? Stock { get; set; }
}
