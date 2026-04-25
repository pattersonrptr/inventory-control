using InventoryControl.Data;
using InventoryControl.Integrations;
using InventoryControl.Integrations.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InventoryControl.BackgroundServices;

/// <summary>
/// Hosted background service that periodically fetches new orders from all
/// enabled stores and processes them (recording stock exit movements).
/// Each store has its own sync interval (configurable via OrderSyncIntervalMinutes).
/// Tracks last processed time per store in the database to avoid re-fetching old orders.
/// </summary>
public class OrderSyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderSyncBackgroundService> _logger;
    private int _consecutiveFailures;

    public OrderSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Exponential backoff: BaseInterval * 2^failures, capped at MaxInterval.</summary>
    public static TimeSpan CalculateBackoffDelay(int consecutiveFailures)
    {
        if (consecutiveFailures <= 0) return BaseInterval;
        var multiplier = Math.Pow(2, consecutiveFailures);
        var delay = TimeSpan.FromMinutes(BaseInterval.TotalMinutes * multiplier);
        return delay > MaxInterval ? MaxInterval : delay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderSyncBackgroundService started.");

        // Delay the first run slightly so the app finishes starting up.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var succeeded = await RunSyncForAllStoresAsync(stoppingToken);
            _consecutiveFailures = succeeded ? 0 : _consecutiveFailures + 1;

            var delay = CalculateBackoffDelay(_consecutiveFailures);
            if (_consecutiveFailures > 0)
                _logger.LogWarning(
                    "OrderSyncBackgroundService: {Failures} consecutive failure(s); next retry in {Delay}.",
                    _consecutiveFailures, delay);

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<bool> RunSyncForAllStoresAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var registry = scope.ServiceProvider.GetRequiredService<PlatformRegistry>();
            var enabledStores = registry.GetEnabledStores();

            if (enabledStores.Count == 0)
            {
                _logger.LogDebug("OrderSyncBackgroundService: no enabled stores configured. Skipping.");
                return true;
            }

            foreach (var storeConfig in enabledStores)
            {
                if (stoppingToken.IsCancellationRequested) break;
                await RunSyncForStoreAsync(scope.ServiceProvider, storeConfig, stoppingToken);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OrderSyncBackgroundService: unhandled error during sync cycle.");
            return false;
        }
    }

    private async Task RunSyncForStoreAsync(
        IServiceProvider services,
        IntegrationConfig storeConfig,
        CancellationToken stoppingToken)
    {
        var syncStateKey = $"OrderSync_{storeConfig.Name}";
        var interval = TimeSpan.FromMinutes(storeConfig.OrderSyncIntervalMinutes > 0
            ? storeConfig.OrderSyncIntervalMinutes
            : 15);

        _logger.LogInformation(
            "OrderSyncBackgroundService: starting order sync for store '{Store}'.", storeConfig.Name);

        try
        {
            var registry = services.GetRequiredService<PlatformRegistry>();
            var syncFactory = services.GetRequiredService<SyncServiceFactory>();
            var dbContext = services.GetRequiredService<AppDbContext>();

            var storeIntegration = registry.CreateIntegration(storeConfig);
            var syncService = syncFactory.Create(storeConfig);

            // Use persisted last-processed timestamp; fall back to interval×2 for first run
            var syncState = await dbContext.SyncStates.FindAsync([syncStateKey], stoppingToken);
            var since = syncState?.LastProcessedAt ?? DateTime.UtcNow.Subtract(interval * 2);

            var orders = await storeIntegration.GetOrdersAsync(since);

            int processed = 0;
            int skipped = 0;
            int failed = 0;
            foreach (var order in orders)
            {
                if (stoppingToken.IsCancellationRequested) break;
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
                    _logger.LogError(ex,
                        "OrderSyncBackgroundService: failed to process order {OrderId} from store '{Store}'.",
                        order.ExternalOrderId, storeConfig.Name);
                    failed++;
                }
            }

            // Persist the current time as last processed for this store
            if (syncState is null)
            {
                syncState = new SyncState { Key = syncStateKey, LastProcessedAt = DateTime.UtcNow };
                dbContext.SyncStates.Add(syncState);
            }
            else
            {
                syncState.LastProcessedAt = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "OrderSyncBackgroundService: completed for store '{Store}'. Processed={Processed}, Skipped={Skipped}, Failed={Failed}.",
                storeConfig.Name, processed, skipped, failed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "OrderSyncBackgroundService: unhandled error during sync for store '{Store}'.",
                storeConfig.Name);
        }
    }
}
