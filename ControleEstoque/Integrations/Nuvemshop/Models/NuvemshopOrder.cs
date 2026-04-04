using System.Text.Json.Serialization;

namespace ControleEstoque.Integrations.Nuvemshop.Models;

public class NuvemshopOrder
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("products")]
    public List<NuvemshopOrderProduct> Products { get; set; } = new();
}
