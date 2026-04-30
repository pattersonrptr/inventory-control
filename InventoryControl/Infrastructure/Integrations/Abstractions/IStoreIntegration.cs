namespace InventoryControl.Infrastructure.Integrations.Abstractions;

public interface IStoreIntegration
{
    Task<IEnumerable<ExternalProduct>> GetProductsAsync();
    Task UpdateStockAsync(string externalProductId, int quantity);
    Task<IEnumerable<ExternalOrder>> GetOrdersAsync(DateTime since);
    Task<ExternalOrder?> GetOrderAsync(string externalOrderId);
    Task<ExternalProduct?> CreateProductAsync(string name, string? description, decimal price, string? sku, int stock);
    Task<IEnumerable<ExternalCategory>> GetCategoriesAsync();
    Task<ExternalCategory?> CreateCategoryAsync(string name);
    Task SetProductPublishedAsync(string externalProductId, bool published);

    /// <summary>
    /// Uploads a single image to the external product. Content is the raw image bytes;
    /// the integration is responsible for any encoding (e.g. base64) required by the platform.
    /// Returns the persisted external image (with the platform-assigned id and CDN URL),
    /// or null when the upload could not be completed.
    /// </summary>
    Task<ExternalImage?> UploadProductImageAsync(
        string externalProductId,
        byte[] content,
        string fileName,
        int position,
        CancellationToken ct = default);
}
