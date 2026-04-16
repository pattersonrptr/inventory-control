using ControleEstoque.Data;
using ControleEstoque.Integrations;
using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControleEstoque.BackgroundServices;

/// <summary>
/// Hosted background service that periodically fetches new orders from the
/// external store and processes them (recording stock exit movements).
/// Runs every 15 minutes by default (configurable via Integration:OrderSyncIntervalMinutes).
/// Tracks last processed time in the database to avoid re-fetching old orders.
/// </summary>
public class OrderSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderSyncBackgroundService> _logger;
    private readonly TimeSpan _interval;
    private const string SyncStateKey = "OrderSync";

    public OrderSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderSyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = configuration.GetValue<int?>("Integration:OrderSyncIntervalMinutes") ?? 15;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OrderSyncBackgroundService started. Interval: {Interval}.", _interval);

        // Delay the first run slightly so the app finishes starting up.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderSyncBackgroundService: starting order sync run.");
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IStoreIntegration>();
            var syncService = scope.ServiceProvider.GetRequiredService<SyncService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Use persisted last-processed timestamp; fall back to interval×2 for first run
            var syncState = await dbContext.SyncStates.FindAsync([SyncStateKey], stoppingToken);
            var since = syncState?.LastProcessedAt ?? DateTime.UtcNow.Subtract(_interval * 2);

            var orders = await store.GetOrdersAsync(since);

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
                        "OrderSyncBackgroundService: failed to process order {OrderId}.",
                        order.ExternalOrderId);
                    failed++;
                }
            }

            // Persist the current time as last processed
            if (syncState is null)
            {
                syncState = new SyncState { Key = SyncStateKey, LastProcessedAt = DateTime.UtcNow };
                dbContext.SyncStates.Add(syncState);
            }
            else
            {
                syncState.LastProcessedAt = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "OrderSyncBackgroundService: completed. Processed={Processed}, Skipped={Skipped}, Failed={Failed}.",
                processed, skipped, failed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OrderSyncBackgroundService: unhandled error during sync run.");
        }
    }
}
