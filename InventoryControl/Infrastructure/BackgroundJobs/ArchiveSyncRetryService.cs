using InventoryControl.Domain.Products;

namespace InventoryControl.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically retries publish/unpublish calls on external stores for product mappings
/// that were left in PendingArchive/PendingUnarchive due to a previous sync failure.
/// </summary>
public class ArchiveSyncRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArchiveSyncRetryService> _logger;
    private readonly TimeSpan _interval;

    public ArchiveSyncRetryService(
        IServiceScopeFactory scopeFactory,
        ILogger<ArchiveSyncRetryService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var minutes = configuration.GetValue<int?>("ArchiveSync:RetryIntervalMinutes") ?? 15;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ArchiveSyncRetryService started. Retry interval: {Interval}.", _interval);

        // Small delay so we don't compete with startup work
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RetryPendingAsync(stoppingToken);

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RetryPendingAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var retrier = scope.ServiceProvider.GetRequiredService<IProductArchiveRetrier>();

            var resolved = await retrier.RetryPendingSyncsAsync(ct);

            if (resolved > 0)
                _logger.LogInformation(
                    "ArchiveSyncRetryService: resolved {Count} pending mapping(s).", resolved);
            else
                _logger.LogDebug("ArchiveSyncRetryService: no pending mappings or none could be resolved.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "ArchiveSyncRetryService: error during retry.");
        }
    }
}
