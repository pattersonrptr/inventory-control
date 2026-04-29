using System.Text.Json.Serialization;

namespace InventoryControl.Infrastructure.Integrations.Nuvemshop.Models;

public class NuvemshopImage
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("src")]
    public string? Src { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}
