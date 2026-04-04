using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Integrations.Nuvemshop.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Integrations.Nuvemshop;

[ApiController]
[Route("api/webhooks/nuvemshop")]
public class NuvemshopWebhookController : ControllerBase
{
    private readonly SyncService _syncService;
    private readonly IStoreIntegration _store;
    private readonly IntegrationConfig _config;
    private readonly ILogger<NuvemshopWebhookController> _logger;

    public NuvemshopWebhookController(
        SyncService syncService,
        IStoreIntegration store,
        IntegrationConfig config,
        ILogger<NuvemshopWebhookController> logger)
    {
        _syncService = syncService;
        _store = store;
        _config = config;
        _logger = logger;
    }

    // Webhooks are server-to-server calls — CSRF tokens are not applicable.
    // Request authenticity is validated by comparing the payload store_id with
    // the configured StoreId (see body below).
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Receive([FromBody] NuvemshopWebhookPayload payload)
    {
        // Validate that the request comes from the configured Nuvemshop store
        // by comparing the payload store_id with the configured StoreId.
        if (!string.IsNullOrEmpty(_config.StoreId) &&
            payload.StoreId.ToString() != _config.StoreId)
        {
            _logger.LogWarning(
                "Webhook rejected: store_id={StoreId} does not match configured store.",
                payload.StoreId);
            return Unauthorized();
        }

        _logger.LogInformation("Webhook received: event={Event}, id={Id}", payload.Event, payload.Id);

        // Handle paid/fulfilled orders: trigger stock exit movement
        if (payload.Event is "order/paid" or "order/fulfilled")
        {
            var order = await _store.GetOrderAsync(payload.Id.ToString());
            if (order is not null)
            {
                await _syncService.ProcessOrderAsync(order);
            }
        }

        return Ok();
    }
}
