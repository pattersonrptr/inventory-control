using InventoryControl.Data;
using InventoryControl.Integrations;
using InventoryControl.Integrations.Abstractions;
using InventoryControl.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly SyncServiceFactory _syncFactory;
    private readonly PlatformRegistry _registry;
    private readonly IProductRepository _productRepo;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SyncController> _logger;

    public SyncController(
        SyncServiceFactory syncFactory,
        PlatformRegistry registry,
        IProductRepository productRepo,
        AppDbContext dbContext,
        ILogger<SyncController> logger)
    {
        _syncFactory = syncFactory;
        _registry = registry;
        _productRepo = productRepo;
        _dbContext = dbContext;
        _logger = logger;
    }

    private IActionResult ResolveStore(string? storeName, out IntegrationConfig config, out SyncService syncService)
    {
        config = null!;
        syncService = null!;

        if (string.IsNullOrWhiteSpace(storeName))
        {
            var enabledStores = _registry.GetEnabledStores();
            if (enabledStores.Count == 0)
                return BadRequest(new { error = "NoStoreConfigured", message = "No enabled stores configured." });
            if (enabledStores.Count > 1)
                return BadRequest(new { error = "StoreRequired", message = "Multiple stores configured. Provide ?store=<name> to select one." });
            config = enabledStores[0];
        }
        else
        {
            config = _registry.GetStoreByName(storeName)!;
            if (config is null)
                return NotFound(new { error = "StoreNotFound", message = $"Store '{storeName}' not found." });
            if (!config.Enabled)
                return BadRequest(new { error = "StoreDisabled", message = $"Store '{storeName}' is disabled." });
        }

        syncService = _syncFactory.Create(config);
        return null!;
    }

    /// <summary>
    /// Pulls products from the external store and matches them with local
    /// products by SKU, updating ExternalId and ExternalIdSource.
    /// </summary>
    [HttpPost("products")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SyncProducts([FromQuery] string? store)
    {
        var error = ResolveStore(store, out var config, out var syncService);
        if (error is not null) return error;

        _logger.LogInformation("Manual product sync triggered for store '{Store}'.", config.Name);
        try
        {
            await syncService.SyncProductsFromStoreAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Product sync failed for store '{Store}': external API unreachable.", config.Name);
            return StatusCode(502, new { error = "ExternalApiError", message = "Product sync failed: could not reach the external store API.", status = 502 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product sync failed for store '{Store}'.", config.Name);
            return StatusCode(500, new { error = "InternalError", message = "Product sync failed due to an internal error.", status = 500 });
        }

        return Ok(new { message = $"Product sync completed for store '{config.Name}'." });
    }

    /// <summary>
    /// Pushes the current stock of a local product to the external store.
    /// Returns 404 if the product does not exist or has no linked external ID.
    /// </summary>
    [HttpPost("push-stock/{productId:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PushStock(int productId, [FromQuery] string? store)
    {
        var error = ResolveStore(store, out var config, out var syncService);
        if (error is not null) return error;

        var product = await _productRepo.GetByIdAsync(productId);
        if (product is null)
        {
            return NotFound(new { message = $"Product {productId} not found." });
        }

        var hasMapping = await _dbContext.ProductExternalMappings
            .AnyAsync(m => m.ProductId == productId && m.StoreName == config.Name);

        if (!hasMapping)
        {
            return NotFound(new { message = $"Product {productId} has no linked external ID for store '{config.Name}'. Run a product sync first." });
        }

        _logger.LogInformation("Manual push-stock triggered for product id={ProductId} on store '{Store}'.", productId, config.Name);
        try
        {
            await syncService.PushStockToStoreAsync(productId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Push-stock failed for product id={ProductId} on store '{Store}': external API unreachable.", productId, config.Name);
            return StatusCode(502, new { error = "ExternalApiError", message = $"Failed to push stock for product {productId}: could not reach the external store API.", status = 502 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push-stock failed for product id={ProductId} on store '{Store}'.", productId, config.Name);
            return StatusCode(500, new { error = "InternalError", message = $"Failed to push stock for product {productId} due to an internal error.", status = 500 });
        }

        return Ok(new { message = $"Stock for product {productId} pushed to store '{config.Name}'." });
    }

    /// <summary>
    /// Fetches orders from the external store created since the given date
    /// and processes each one (recording stock exit movements).
    /// Defaults to the past 24 hours when no date is provided.
    /// </summary>
    [HttpPost("orders")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ProcessOrders([FromQuery] DateTime? since, [FromQuery] string? store)
    {
        var error = ResolveStore(store, out var config, out var syncService);
        if (error is not null) return error;

        var sinceDate = since?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-1);
        _logger.LogInformation("Manual order processing triggered since {Since} for store '{Store}'.", sinceDate, config.Name);

        var storeIntegration = _registry.CreateIntegration(config);
        IEnumerable<ExternalOrder> orders;
        try
        {
            orders = await storeIntegration.GetOrdersAsync(sinceDate);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch orders from store '{Store}': external API unreachable.", config.Name);
            return StatusCode(502, new { error = "ExternalApiError", message = "Failed to fetch orders from the store: could not reach the external store API.", status = 502 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch orders from store '{Store}'.", config.Name);
            return StatusCode(500, new { error = "InternalError", message = "Failed to fetch orders from the store due to an internal error.", status = 500 });
        }

        var orderList = orders.ToList();
        if (orderList.Count == 0)
            return Ok(new { message = $"No orders found since {sinceDate:O} for store '{config.Name}'." });

        int processed = 0;
        int skipped = 0;
        int failed = 0;
        foreach (var order in orderList)
        {
            try
            {
                var wasProcessed = await syncService.ProcessOrderAsync(order);
                if (wasProcessed)
                    processed++;
                else
                    skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process order {OrderId} from store '{Store}'.", order.ExternalOrderId, config.Name);
                failed++;
            }
        }

        return Ok(new { message = $"Processed {processed} order(s) since {sinceDate:O} for store '{config.Name}'. Skipped {skipped}.", failed });
    }

    /// <summary>
    /// Creates a local product in the external store and saves the returned ExternalId.
    /// </summary>
    [HttpPost("push-product/{productId:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PushProduct(int productId, [FromQuery] string? store)
    {
        var error = ResolveStore(store, out var config, out var syncService);
        if (error is not null) return error;

        var product = await _productRepo.GetByIdAsync(productId);
        if (product is null)
            return NotFound(new { message = $"Product {productId} not found." });

        var existingMapping = await _dbContext.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == productId && m.StoreName == config.Name);

        if (existingMapping is not null)
            return Conflict(new { message = $"Product {productId} is already linked to external id {existingMapping.ExternalId} on store '{config.Name}'." });

        _logger.LogInformation("Push-product triggered for product id={ProductId} on store '{Store}'.", productId, config.Name);
        try
        {
            await syncService.PushProductToStoreAsync(productId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Push-product failed for product id={ProductId} on store '{Store}': external API unreachable.", productId, config.Name);
            return StatusCode(502, new { error = "ExternalApiError", message = $"Failed to push product {productId}: could not reach the external store API.", status = 502 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push-product failed for product id={ProductId} on store '{Store}'.", productId, config.Name);
            return StatusCode(500, new { error = "InternalError", message = $"Failed to push product {productId} due to an internal error.", status = 500 });
        }

        return Ok(new { message = $"Product {productId} pushed to store '{config.Name}'." });
    }

    /// <summary>
    /// Syncs local categories to the external store, creating any that are missing.
    /// </summary>
    [HttpPost("categories")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SyncCategories([FromQuery] string? store)
    {
        var error = ResolveStore(store, out var config, out var syncService);
        if (error is not null) return error;

        _logger.LogInformation("Manual category sync triggered for store '{Store}'.", config.Name);
        try
        {
            await syncService.SyncCategoriesToStoreAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Category sync failed for store '{Store}': external API unreachable.", config.Name);
            return StatusCode(502, new { error = "ExternalApiError", message = "Category sync failed: could not reach the external store API.", status = 502 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Category sync failed for store '{Store}'.", config.Name);
            return StatusCode(500, new { error = "InternalError", message = "Category sync failed due to an internal error.", status = 500 });
        }

        return Ok(new { message = $"Category sync completed for store '{config.Name}'." });
    }

    [HttpGet("stores")]
    [IgnoreAntiforgeryToken]
    public IActionResult ListStores()
    {
        var stores = _registry.GetAllStores().Select(s => new
        {
            s.Name,
            s.Platform,
            s.Enabled,
            s.StoreUrl
        });
        return Ok(stores);
    }
}
