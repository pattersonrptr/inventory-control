using System.Text.Json.Serialization;

namespace InventoryControl.Integrations.Nuvemshop.Models;

public class NuvemshopWebhookPayload
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("store_id")]
    public long StoreId { get; set; }

    [JsonPropertyName("id")]
    public long Id { get; set; }
}
