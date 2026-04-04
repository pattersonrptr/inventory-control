namespace ControleEstoque.Integrations.Abstractions;

public interface IStoreIntegration
{
    Task<IEnumerable<ExternalProduct>> GetProductsAsync();
    Task UpdateStockAsync(string externalProductId, int quantity);
    Task<IEnumerable<ExternalOrder>> GetOrdersAsync(DateTime since);
    Task<ExternalOrder?> GetOrderAsync(string externalOrderId);
    Task<ExternalProduct?> CreateProductAsync(string name, string? description, decimal price, string? sku, int stock);
    Task<IEnumerable<ExternalCategory>> GetCategoriesAsync();
    Task<ExternalCategory?> CreateCategoryAsync(string name);
}
