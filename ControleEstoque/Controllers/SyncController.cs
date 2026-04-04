using ControleEstoque.Integrations;
using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly SyncService _syncService;
    private readonly IProductRepository _productRepo;
    private readonly IStoreIntegration _store;
    private readonly ILogger<SyncController> _logger;

    public SyncController(
        SyncService syncService,
        IProductRepository productRepo,
        IStoreIntegration store,
        ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _productRepo = productRepo;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Pulls products from the external store and matches them with local
    /// products by SKU, updating ExternalId and ExternalIdSource.
    /// </summary>
    [HttpPost("products")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SyncProducts()
    {
        _logger.LogInformation("Manual product sync triggered.");
        try
        {
            await _syncService.SyncProductsFromStoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product sync failed.");
            return StatusCode(502, new { message = "Product sync failed. Check the server logs for details." });
        }

        return Ok(new { message = "Product sync completed." });
    }

    /// <summary>
    /// Pushes the current stock of a local product to the external store.
    /// Returns 404 if the product does not exist or has no linked external ID.
    /// </summary>
    [HttpPost("push-stock/{productId:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PushStock(int productId)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product is null)
        {
            return NotFound(new { message = $"Product {productId} not found." });
        }

        if (product.ExternalId is null)
        {
            return NotFound(new { message = $"Product {productId} has no linked external ID. Run a product sync first." });
        }

        _logger.LogInformation("Manual push-stock triggered for product id={ProductId}.", productId);
        try
        {
            await _syncService.PushStockToStoreAsync(productId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push-stock failed for product id={ProductId}.", productId);
            return StatusCode(502, new { message = $"Failed to push stock for product {productId}. Check the server logs for details." });
        }

        return Ok(new { message = $"Stock for product {productId} pushed to store." });
    }

    /// <summary>
    /// Fetches orders from the external store created since the given date
    /// and processes each one (recording stock exit movements).
    /// Defaults to the past 24 hours when no date is provided.
    /// </summary>
    [HttpPost("orders")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ProcessOrders([FromQuery] DateTime? since)
    {
        var sinceDate = since?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-1);
        _logger.LogInformation("Manual order processing triggered since {Since}.", sinceDate);

        IEnumerable<ExternalOrder> orders;
        try
        {
            orders = await _store.GetOrdersAsync(sinceDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch orders from the store.");
            return StatusCode(502, new { message = "Failed to fetch orders from the store. Check the server logs for details." });
        }

        var orderList = orders.ToList();
        if (orderList.Count == 0)
            return Ok(new { message = $"No orders found since {sinceDate:O}." });

        int processed = 0;
        int failed = 0;
        foreach (var order in orderList)
        {
            try
            {
                await _syncService.ProcessOrderAsync(order);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process order {OrderId}.", order.ExternalOrderId);
                failed++;
            }
        }

        return Ok(new { message = $"Processed {processed} order(s) since {sinceDate:O}.", failed });
    }

    /// <summary>
    /// Creates a local product in the external store and saves the returned ExternalId.
    /// </summary>
    [HttpPost("push-product/{productId:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PushProduct(int productId)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product is null)
            return NotFound(new { message = $"Product {productId} not found." });

        if (product.ExternalId is not null)
            return Conflict(new { message = $"Product {productId} is already linked to external id {product.ExternalId}." });

        _logger.LogInformation("Push-product triggered for product id={ProductId}.", productId);
        try
        {
            await _syncService.PushProductToStoreAsync(productId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push-product failed for product id={ProductId}.", productId);
            return StatusCode(502, new { message = $"Failed to push product {productId} to store. Check the server logs for details." });
        }

        return Ok(new { message = $"Product {productId} pushed to store." });
    }

    /// <summary>
    /// Syncs local categories to the external store, creating any that are missing.
    /// </summary>
    [HttpPost("categories")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SyncCategories()
    {
        _logger.LogInformation("Manual category sync triggered.");
        try
        {
            await _syncService.SyncCategoriesToStoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Category sync failed.");
            return StatusCode(502, new { message = "Category sync failed. Check the server logs for details." });
        }

        return Ok(new { message = "Category sync completed." });
    }
}
