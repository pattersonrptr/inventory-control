using System.Text.Json.Serialization;

namespace InventoryControl.Infrastructure.Integrations.Nuvemshop.Models;

public class NuvemshopCategory
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public Dictionary<string, string>? Name { get; set; }
}
