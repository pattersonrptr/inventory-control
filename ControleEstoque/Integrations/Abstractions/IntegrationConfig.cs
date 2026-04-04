namespace ControleEstoque.Integrations.Abstractions;

public class IntegrationConfig
{
    public bool Enabled { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string? StoreUrl { get; set; }
    public string? AccessToken { get; set; }
    public string? StoreId { get; set; }
}
