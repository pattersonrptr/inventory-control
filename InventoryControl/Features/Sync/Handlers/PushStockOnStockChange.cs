using InventoryControl.Domain.Products.Events;
using InventoryControl.Infrastructure.Events;
using InventoryControl.Infrastructure.Integrations;

namespace InventoryControl.Features.Sync.Handlers;

public class PushStockOnStockChange : IDomainEventHandler<StockChanged>
{
    private readonly SyncService? _syncService;
    private readonly ILogger<PushStockOnStockChange> _logger;

    public PushStockOnStockChange(
        ILogger<PushStockOnStockChange> logger,
        SyncService? syncService = null)
    {
        _logger = logger;
        _syncService = syncService;
    }

    public async Task HandleAsync(StockChanged @event, CancellationToken ct)
    {
        if (_syncService is null) return;

        await _syncService.PushStockToStoreAsync(@event.ProductId);
        _logger.LogInformation(
            "Auto-pushed stock for product id={ProductId} after StockChanged event.",
            @event.ProductId);
    }
}
