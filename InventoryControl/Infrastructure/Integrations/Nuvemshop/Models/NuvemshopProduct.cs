using System.Text.Json.Serialization;

namespace InventoryControl.Infrastructure.Integrations.Nuvemshop.Models;

public class NuvemshopProduct
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public Dictionary<string, string>? Name { get; set; }

    [JsonPropertyName("description")]
    public Dictionary<string, string>? Description { get; set; }

    [JsonPropertyName("variants")]
    public List<NuvemshopVariant> Variants { get; set; } = new();

    [JsonPropertyName("images")]
    public List<NuvemshopImage> Images { get; set; } = new();
}
