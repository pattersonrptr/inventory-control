using InventoryControl.Data;
using InventoryControl.Integrations.Abstractions;
using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryControl.Integrations;

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
    private readonly IProcessedOrderRepository _processedOrderRepo;
    private readonly AppDbContext _dbContext;
    private readonly IntegrationConfig _config;
    private readonly ILogger<SyncService> _logger;

    /// <summary>
    /// Order statuses that indicate confirmed payment and should trigger stock deduction.
    /// Nuvemshop uses separate fields: 'status' (open/closed/cancelled) and
    /// 'payment_status' (pending/authorized/paid/voided/refunded/abandoned).
    /// We check payment_status for confirmation and status to exclude cancelled orders.
    /// </summary>
    private static readonly HashSet<string> ConfirmedPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "paid", "authorized"
    };

    public SyncService(
        IStoreIntegration store,
        IProductRepository productRepo,
        IStockMovementRepository movementRepo,
        ICategoryRepository categoryRepo,
        IProcessedOrderRepository processedOrderRepo,
        AppDbContext dbContext,
        IntegrationConfig config,
        ILogger<SyncService> logger)
    {
        _store = store;
        _productRepo = productRepo;
        _movementRepo = movementRepo;
        _categoryRepo = categoryRepo;
        _processedOrderRepo = processedOrderRepo;
        _dbContext = dbContext;
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
                await UpsertProductMappingAsync(local.Id, external.ExternalId);
                _logger.LogInformation(
                    "Synced product {ProductName} (id={Id}) with external id {ExternalId} from store '{StoreName}'.",
                    local.Name, local.Id, external.ExternalId, _config.Name);
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

        await UpsertProductMappingAsync(product.Id, external.ExternalId);

        _logger.LogInformation(
            "Pushed product {ProductName} (id={Id}) to store '{StoreName}', externalId={ExternalId}.",
            product.Name, product.Id, _config.Name, external.ExternalId);
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
                var currentMapping = await _dbContext.CategoryExternalMappings
                    .FirstOrDefaultAsync(m => m.CategoryId == local.Id && m.StoreName == _config.Name);

                if (currentMapping is null || currentMapping.ExternalId != existing.ExternalId)
                {
                    await UpsertCategoryMappingAsync(local.Id, existing.ExternalId);
                    _logger.LogInformation(
                        "Linked category '{Name}' (id={Id}) to existing external id {ExternalId} on store '{StoreName}'.",
                        local.Name, local.Id, existing.ExternalId, _config.Name);
                }
            }
            else
            {
                var created = await _store.CreateCategoryAsync(local.Name);
                if (created is not null)
                {
                    await UpsertCategoryMappingAsync(local.Id, created.ExternalId);
                    _logger.LogInformation(
                        "Created category '{Name}' (id={Id}) on store '{StoreName}' with externalId={ExternalId}.",
                        local.Name, local.Id, _config.Name, created.ExternalId);
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
        if (product is null)
        {
            _logger.LogWarning(
                "Cannot push stock for product id={ProductId}: not found.", productId);
            return;
        }

        var mapping = await _dbContext.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == productId && m.StoreName == _config.Name);

        if (mapping is null)
        {
            _logger.LogWarning(
                "Cannot push stock for product id={ProductId}: no external mapping for store '{StoreName}'.",
                productId, _config.Name);
            return;
        }

        await _store.UpdateStockAsync(mapping.ExternalId, product.CurrentStock);
        _logger.LogInformation(
            "Pushed stock={Stock} for product {ProductName} (externalId={ExternalId}) to store '{StoreName}'.",
            product.CurrentStock, product.Name, mapping.ExternalId, _config.Name);
    }

    /// <summary>
    /// Payment statuses that indicate a refund and should trigger stock reversal.
    /// </summary>
    private static readonly HashSet<string> RefundedPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "refunded", "voided"
    };

    /// <summary>
    /// Processes an incoming order from the external store: records a stock exit
    /// movement for each line item. Skips orders that have already been processed
    /// or that do not have a confirmed payment status.
    /// If a previously processed order is now refunded/voided, reverses the stock
    /// by creating entry movements.
    /// </summary>
    public async Task<bool> ProcessOrderAsync(ExternalOrder order)
    {
        // Check if order was already processed
        var existingRecord = await _processedOrderRepo.GetByExternalOrderIdAsync(order.ExternalOrderId);

        if (existingRecord is not null)
        {
            // Check for refund: previously confirmed payment now refunded/voided
            if (ConfirmedPaymentStatuses.Contains(existingRecord.PaymentStatus)
                && RefundedPaymentStatuses.Contains(order.PaymentStatus))
            {
                return await ProcessRefundAsync(order, existingRecord);
            }

            _logger.LogInformation(
                "Order {OrderId} already processed. Skipping.",
                order.ExternalOrderId);
            return false;
        }

        // Status filter: skip cancelled orders and orders without confirmed payment
        if (string.Equals(order.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Order {OrderId} is cancelled. Skipping.",
                order.ExternalOrderId);
            return false;
        }

        if (!ConfirmedPaymentStatuses.Contains(order.PaymentStatus))
        {
            _logger.LogInformation(
                "Order {OrderId} has payment_status '{PaymentStatus}' (not confirmed). Skipping.",
                order.ExternalOrderId, order.PaymentStatus);
            return false;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var localProducts = await _productRepo.GetAllAsync();
            var storeMappings = await _dbContext.ProductExternalMappings
                .Where(m => m.StoreName == _config.Name)
                .ToDictionaryAsync(m => m.ExternalId, m => m.ProductId);

            foreach (var item in order.Items)
            {
                var product = FindMatchingProduct(localProducts, item, storeMappings);

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

            // Mark order as processed to prevent reprocessing
            await _processedOrderRepo.AddAsync(new ProcessedOrder
            {
                ExternalOrderId = order.ExternalOrderId,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                ProcessedAt = DateTime.Now
            });

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Reverses stock for a previously processed order that has been refunded/voided.
    /// Creates entry movements to restore the stock for each line item.
    /// </summary>
    private async Task<bool> ProcessRefundAsync(ExternalOrder order, ProcessedOrder existingRecord)
    {
        _logger.LogInformation(
            "Order {OrderId} was refunded (payment_status changed from '{OldStatus}' to '{NewStatus}'). Reversing stock.",
            order.ExternalOrderId, existingRecord.PaymentStatus, order.PaymentStatus);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var localProducts = await _productRepo.GetAllAsync();
            var storeMappings = await _dbContext.ProductExternalMappings
                .Where(m => m.StoreName == _config.Name)
                .ToDictionaryAsync(m => m.ExternalId, m => m.ProductId);

            foreach (var item in order.Items)
            {
                var product = FindMatchingProduct(localProducts, item, storeMappings);

                if (product is null)
                {
                    _logger.LogWarning(
                        "Refund {OrderId}: no local product matched externalProductId={ExternalProductId} / sku={Sku}. Item skipped.",
                        order.ExternalOrderId, item.ExternalProductId, item.Sku);
                    continue;
                }

                var movement = new StockMovement
                {
                    ProductId = product.Id,
                    Type = MovementType.Entry,
                    Quantity = item.Quantity,
                    Date = DateTime.Now,
                    UnitCost = item.UnitPrice,
                    Notes = $"Refund for order {order.ExternalOrderId} from external store"
                };

                await _movementRepo.AddAsync(movement);
                await _productRepo.UpdateStockAsync(product.Id, product.CurrentStock + item.Quantity);

                _logger.LogInformation(
                    "Reversed {Quantity} unit(s) of {ProductName} for refunded order {OrderId}.",
                    item.Quantity, product.Name, order.ExternalOrderId);
            }

            // Update the processed order record with the new payment status
            existingRecord.PaymentStatus = order.PaymentStatus;
            await _processedOrderRepo.UpdateAsync(existingRecord);

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static Product? FindMatchingProduct(
        IEnumerable<Product> localProducts,
        ExternalOrderItem item,
        Dictionary<string, int> storeMappings)
    {
        // Match by external ID via mapping table
        if (!string.IsNullOrEmpty(item.ExternalProductId)
            && storeMappings.TryGetValue(item.ExternalProductId, out var mappedProductId))
        {
            var byMapping = localProducts.FirstOrDefault(p => p.Id == mappedProductId);
            if (byMapping is not null) return byMapping;
        }

        // Fallback: match by SKU
        return localProducts.FirstOrDefault(p =>
            !string.IsNullOrEmpty(p.Sku) && p.Sku == item.Sku);
    }

    private async Task UpsertProductMappingAsync(int productId, string externalId)
    {
        var mapping = await _dbContext.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == productId && m.StoreName == _config.Name);

        if (mapping is not null)
        {
            mapping.ExternalId = externalId;
            mapping.Platform = _config.Platform;
        }
        else
        {
            _dbContext.ProductExternalMappings.Add(new ProductExternalMapping
            {
                ProductId = productId,
                StoreName = _config.Name,
                ExternalId = externalId,
                Platform = _config.Platform
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task UpsertCategoryMappingAsync(int categoryId, string externalId)
    {
        var mapping = await _dbContext.CategoryExternalMappings
            .FirstOrDefaultAsync(m => m.CategoryId == categoryId && m.StoreName == _config.Name);

        if (mapping is not null)
        {
            mapping.ExternalId = externalId;
            mapping.Platform = _config.Platform;
        }
        else
        {
            _dbContext.CategoryExternalMappings.Add(new CategoryExternalMapping
            {
                CategoryId = categoryId,
                StoreName = _config.Name,
                ExternalId = externalId,
                Platform = _config.Platform
            });
        }

        await _dbContext.SaveChangesAsync();
    }
}
