using InventoryControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily background service that deletes AuditLog entries older than the configured
/// retention period. Prevents unbounded database growth without losing recent history.
/// Configure via "AuditLog:RetentionDays" (default: 90). Set to 0 to disable cleanup.
/// </summary>
public class AuditLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogCleanupService> _logger;
    private readonly int _retentionDays;

    public AuditLogCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = configuration.GetValue<int?>("AuditLog:RetentionDays") ?? 90;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_retentionDays <= 0)
        {
            _logger.LogInformation(
                "AuditLogCleanupService: retention disabled (RetentionDays={RetentionDays}). Exiting.",
                _retentionDays);
            return;
        }

        _logger.LogInformation(
            "AuditLogCleanupService started. Retention={RetentionDays} days. Cleanup runs daily at 02:00.",
            _retentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync(stoppingToken);

            // Schedule next run at 02:00 the following day
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(2);
            var delay = nextRun - now;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

            var deleted = await db.AuditLogs
                .Where(a => a.Timestamp < cutoff)
                .ExecuteDeleteAsync(stoppingToken);

            if (deleted > 0)
                _logger.LogInformation(
                    "AuditLogCleanupService: deleted {Count} audit log entries older than {CutoffDate:yyyy-MM-dd}.",
                    deleted, cutoff);
            else
                _logger.LogDebug(
                    "AuditLogCleanupService: no entries to delete (cutoff={CutoffDate:yyyy-MM-dd}).", cutoff);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AuditLogCleanupService: error during cleanup.");
        }
    }
}
