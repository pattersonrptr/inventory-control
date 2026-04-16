using InventoryControl.Data;
using InventoryControl.Integrations.Abstractions;
using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
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

            foreach (var item in order.Items)
            {
                var product = FindMatchingProduct(localProducts, item);

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

            foreach (var item in order.Items)
            {
                var product = FindMatchingProduct(localProducts, item);

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

    private static Product? FindMatchingProduct(IEnumerable<Product> localProducts, ExternalOrderItem item)
    {
        return localProducts.FirstOrDefault(p =>
            p.ExternalId == item.ExternalProductId ||
            (!string.IsNullOrEmpty(p.Sku) && p.Sku == item.Sku));
    }
}
