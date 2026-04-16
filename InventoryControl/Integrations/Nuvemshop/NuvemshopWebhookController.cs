using InventoryControl.Integrations.Abstractions;
using InventoryControl.Integrations.Nuvemshop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Integrations.Nuvemshop;

// Webhooks are called by external platforms — authentication is not applicable.
[AllowAnonymous]
[ApiController]
[Route("api/webhooks/nuvemshop")]
public class NuvemshopWebhookController : ControllerBase
{
    private readonly PlatformRegistry _registry;
    private readonly SyncServiceFactory _syncFactory;
    private readonly ILogger<NuvemshopWebhookController> _logger;

    public NuvemshopWebhookController(
        PlatformRegistry registry,
        SyncServiceFactory syncFactory,
        ILogger<NuvemshopWebhookController> logger)
    {
        _registry = registry;
        _syncFactory = syncFactory;
        _logger = logger;
    }

    // Webhooks are server-to-server calls — CSRF tokens are not applicable.
    // Request authenticity is validated by comparing the payload store_id with
    // a configured store's StoreId.
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Receive([FromBody] NuvemshopWebhookPayload payload)
    {
        // Find the matching store by the payload's store_id
        var storeConfig = _registry.GetStoreByPlatformStoreId(payload.StoreId.ToString());
        if (storeConfig is null)
        {
            _logger.LogWarning(
                "Webhook rejected: store_id={StoreId} does not match any configured store.",
                payload.StoreId);
            return Unauthorized();
        }

        _logger.LogInformation(
            "Webhook received for store '{Store}': event={Event}, id={Id}",
            storeConfig.Name, payload.Event, payload.Id);

        try
        {
            // Handle paid/fulfilled orders: trigger stock exit movement
            if (payload.Event is "order/paid" or "order/fulfilled")
            {
                var storeIntegration = _registry.CreateIntegration(storeConfig);
                var syncService = _syncFactory.Create(storeConfig);

                var order = await storeIntegration.GetOrderAsync(payload.Id.ToString());
                if (order is not null)
                {
                    await syncService.ProcessOrderAsync(order);
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Webhook processing failed for store '{Store}', event={Event}, id={Id}.",
                storeConfig.Name, payload.Event, payload.Id);
            return StatusCode(500, new { error = "WebhookProcessingError", message = "Failed to process webhook event." });
        }
    }
}
