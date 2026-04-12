using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Integrations.Nuvemshop.Models;

namespace ControleEstoque.Integrations.Nuvemshop;

/// <summary>
/// Adapter that maps Nuvemshop API responses to the generic integration contracts.
/// </summary>
public class NuvemshopIntegration : IStoreIntegration
{
    private readonly NuvemshopClient _client;

    public NuvemshopIntegration(NuvemshopClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ExternalProduct>> GetProductsAsync()
    {
        var nuvemshopProducts = await _client.GetProductsAsync();

        return nuvemshopProducts.Select(p => new ExternalProduct
        {
            ExternalId = p.Id.ToString(),
            Name = p.Name?.GetValueOrDefault("en") ?? p.Name?.GetValueOrDefault("pt") ?? p.Name?.Values.FirstOrDefault() ?? string.Empty,
            Description = p.Description?.GetValueOrDefault("en") ?? p.Description?.GetValueOrDefault("pt") ?? p.Description?.Values.FirstOrDefault(),
            Sku = p.Variants.FirstOrDefault()?.Sku ?? string.Empty,
            Price = decimal.TryParse(p.Variants.FirstOrDefault()?.Price, out var price) ? price : 0m,
            Stock = p.Variants.FirstOrDefault()?.Stock ?? 0,
            Variants = p.Variants.Select(v => new ExternalProductVariant
            {
                ExternalId = v.Id.ToString(),
                Sku = v.Sku,
                Price = decimal.TryParse(v.Price, out var vPrice) ? vPrice : 0m,
                Stock = v.Stock ?? 0
            }).ToList()
        });
    }

    public async Task UpdateStockAsync(string externalProductId, int quantity)
    {
        if (!long.TryParse(externalProductId, out var productId)) return;

        var variants = await _client.GetVariantsAsync(productId);
        var variant = variants.FirstOrDefault();
        if (variant is null) return;

        await _client.UpdateVariantStockAsync(productId, variant.Id, quantity);
    }

    public async Task<IEnumerable<ExternalOrder>> GetOrdersAsync(DateTime since)
    {
        var orders = await _client.GetOrdersAsync(since);
        return orders.Select(MapOrder);
    }

    public async Task<ExternalOrder?> GetOrderAsync(string externalOrderId)
    {
        if (!long.TryParse(externalOrderId, out var orderId)) return null;
        var order = await _client.GetOrderAsync(orderId);
        return order is null ? null : MapOrder(order);
    }

    public async Task<ExternalProduct?> CreateProductAsync(string name, string? description, decimal price, string? sku, int stock)
    {
        var created = await _client.CreateProductAsync(name, description, price, sku, stock);
        if (created is null) return null;
        return new ExternalProduct
        {
            ExternalId = created.Id.ToString(),
            Name = created.Name?.GetValueOrDefault("pt") ?? name,
            Description = created.Description?.GetValueOrDefault("pt"),
            Sku = created.Variants.FirstOrDefault()?.Sku ?? sku ?? string.Empty,
            Price = price,
            Stock = stock,
            Variants = created.Variants.Select(v => new ExternalProductVariant
            {
                ExternalId = v.Id.ToString(),
                Sku = v.Sku,
                Price = decimal.TryParse(v.Price, out var vPrice) ? vPrice : price,
                Stock = v.Stock ?? stock
            }).ToList()
        };
    }

    public async Task<IEnumerable<ExternalCategory>> GetCategoriesAsync()
    {
        var cats = await _client.GetCategoriesAsync();
        return cats.Select(c => new ExternalCategory
        {
            ExternalId = c.Id.ToString(),
            Name = c.Name?.GetValueOrDefault("pt") ?? c.Name?.Values.FirstOrDefault() ?? string.Empty
        });
    }

    public async Task<ExternalCategory?> CreateCategoryAsync(string name)
    {
        var created = await _client.CreateCategoryAsync(name);
        if (created is null) return null;
        return new ExternalCategory
        {
            ExternalId = created.Id.ToString(),
            Name = created.Name?.GetValueOrDefault("pt") ?? name
        };
    }

    private static ExternalOrder MapOrder(NuvemshopOrder o) => new()
    {
        ExternalOrderId = o.Id.ToString(),
        Status = o.Status,
        PaymentStatus = o.PaymentStatus,
        CreatedAt = o.CreatedAt,
        Items = o.Products.Select(p => new ExternalOrderItem
        {
            ExternalProductId = p.ProductId.ToString(),
            Sku = p.Sku,
            Quantity = p.Quantity,
            UnitPrice = decimal.TryParse(p.Price, out var price) ? price : 0m
        }).ToList()
    };
}
