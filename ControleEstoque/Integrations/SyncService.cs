using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ControleEstoque.Integrations;

/// <summary>
/// Platform-agnostic sync service. Works against IStoreIntegration and never
/// depends on a specific e-commerce adapter.
/// </summary>
public class SyncService
{
    private readonly IStoreIntegration _store;
    private readonly IProductRepository _productRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IntegrationConfig _config;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IStoreIntegration store,
        IProductRepository productRepo,
        IStockMovementRepository movementRepo,
        ICategoryRepository categoryRepo,
        IntegrationConfig config,
        ILogger<SyncService> logger)
    {
        _store = store;
        _productRepo = productRepo;
        _movementRepo = movementRepo;
        _categoryRepo = categoryRepo;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Pulls products from the external store and syncs SKU / ExternalId fields
    /// on matching local products.
    /// </summary>
    public async Task SyncProductsFromStoreAsync()
    {
        var externalProducts = await _store.GetProductsAsync();
        var localProducts = await _productRepo.GetAllAsync();

        foreach (var external in externalProducts)
        {
            // Match by SKU when available
            var local = localProducts.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Sku) && p.Sku == external.Sku);

            if (local is not null)
            {
                local.ExternalId = external.ExternalId;
                local.ExternalIdSource = _config.Platform;
                await _productRepo.UpdateAsync(local);
                _logger.LogInformation(
                    "Synced product {ProductName} (id={Id}) with external id {ExternalId} from {Platform}",
                    local.Name, local.Id, external.ExternalId, _config.Platform);
            }
            else
            {
                _logger.LogDebug(
                    "No local product matched external SKU '{Sku}' (externalId={ExternalId}).",
                    external.Sku, external.ExternalId);
            }
        }
    }

    /// <summary>
    /// Creates a local product in the external store, saving the returned ExternalId.
    /// </summary>
    public async Task PushProductToStoreAsync(int productId)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product is null)
        {
            _logger.LogWarning("Cannot push product id={ProductId}: not found.", productId);
            return;
        }

        var external = await _store.CreateProductAsync(
            product.Name,
            product.Description,
            product.SellingPrice,
            product.Sku,
            product.CurrentStock);

        if (external is null)
        {
            _logger.LogWarning("CreateProductAsync returned null for product id={ProductId}.", productId);
            return;
        }

        product.ExternalId = external.ExternalId;
        product.ExternalIdSource = _config.Platform;
        await _productRepo.UpdateAsync(product);

        _logger.LogInformation(
            "Pushed product {ProductName} (id={Id}) to store, externalId={ExternalId}.",
            product.Name, product.Id, external.ExternalId);
    }

    /// <summary>
    /// Syncs local categories to the external store, creating any that are missing (matched by name).
    /// </summary>
    public async Task SyncCategoriesToStoreAsync()
    {
        var localCategories = await _categoryRepo.GetAllAsync();
        var externalCategories = (await _store.GetCategoriesAsync()).ToList();

        foreach (var local in localCategories)
        {
            var existing = externalCategories.FirstOrDefault(e =>
                string.Equals(e.Name, local.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                if (local.ExternalId != existing.ExternalId)
                {
                    local.ExternalId = existing.ExternalId;
                    local.ExternalIdSource = _config.Platform;
                    await _categoryRepo.UpdateAsync(local);
                    _logger.LogInformation(
                        "Linked category '{Name}' (id={Id}) to existing external id {ExternalId}.",
                        local.Name, local.Id, existing.ExternalId);
                }
            }
            else
            {
                var created = await _store.CreateCategoryAsync(local.Name);
                if (created is not null)
                {
                    local.ExternalId = created.ExternalId;
                    local.ExternalIdSource = _config.Platform;
                    await _categoryRepo.UpdateAsync(local);
                    _logger.LogInformation(
                        "Created category '{Name}' (id={Id}) on store with externalId={ExternalId}.",
                        local.Name, local.Id, created.ExternalId);
                }
            }
        }
    }

    /// <summary>
    /// Pushes the current stock of a local product to the external store.
    /// </summary>
    public async Task PushStockToStoreAsync(int productId)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product?.ExternalId is null)
        {
            _logger.LogWarning(
                "Cannot push stock for product id={ProductId}: no ExternalId linked.", productId);
            return;
        }

        await _store.UpdateStockAsync(product.ExternalId, product.CurrentStock);
        _logger.LogInformation(
            "Pushed stock={Stock} for product {ProductName} (externalId={ExternalId}).",
            product.CurrentStock, product.Name, product.ExternalId);
    }

    /// <summary>
    /// Processes an incoming order from the external store: records a stock exit
    /// movement for each line item.
    /// </summary>
    public async Task ProcessOrderAsync(ExternalOrder order)
    {
        var localProducts = await _productRepo.GetAllAsync();

        foreach (var item in order.Items)
        {
            // Match by ExternalId or SKU
            var product = localProducts.FirstOrDefault(p =>
                p.ExternalId == item.ExternalProductId ||
                (!string.IsNullOrEmpty(p.Sku) && p.Sku == item.Sku));

            if (product is null)
            {
                _logger.LogWarning(
                    "Order {OrderId}: no local product matched externalProductId={ExternalProductId} / sku={Sku}. Item skipped.",
                    order.ExternalOrderId, item.ExternalProductId, item.Sku);
                continue;
            }

            if (product.CurrentStock < item.Quantity)
            {
                _logger.LogWarning(
                    "Order {OrderId}: insufficient stock for product {ProductName} (available={Available}, requested={Requested}). Item skipped.",
                    order.ExternalOrderId, product.Name, product.CurrentStock, item.Quantity);
                continue;
            }

            var movement = new StockMovement
            {
                ProductId = product.Id,
                Type = MovementType.Exit,
                Quantity = item.Quantity,
                Date = order.CreatedAt,
                ExitReason = ExitReason.Sale,
                UnitCost = item.UnitPrice,
                Notes = $"Order {order.ExternalOrderId} from external store"
            };

            await _movementRepo.AddAsync(movement);
            await _productRepo.UpdateStockAsync(product.Id, product.CurrentStock - item.Quantity);

            _logger.LogInformation(
                "Recorded exit of {Quantity} unit(s) of {ProductName} for order {OrderId}.",
                item.Quantity, product.Name, order.ExternalOrderId);
        }
    }
}
