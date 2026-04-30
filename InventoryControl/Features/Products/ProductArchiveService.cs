using InventoryControl.Domain.Products;
using InventoryControl.Infrastructure;
using InventoryControl.Infrastructure.Integrations;
using InventoryControl.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Features.Products;

public class ProductArchiveService : IProductArchiveRetrier
{
    private readonly AppDbContext _dbContext;
    private readonly PlatformRegistry _registry;
    private readonly IClock _clock;
    private readonly ILogger<ProductArchiveService> _logger;

    public ProductArchiveService(
        AppDbContext dbContext,
        PlatformRegistry registry,
        IClock clock,
        ILogger<ProductArchiveService> logger)
    {
        _dbContext = dbContext;
        _registry = registry;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ArchiveResult> ArchiveAsync(int productId)
        => await ChangeArchiveStateAsync(productId, archive: true);

    public async Task<ArchiveResult> UnarchiveAsync(int productId)
        => await ChangeArchiveStateAsync(productId, archive: false);

    /// <summary>
    /// Retries the publish/unpublish call for any mapping with a non-Synced status.
    /// Used by the background retry job and the manual "Re-sync now" action.
    /// </summary>
    public async Task<int> RetryPendingSyncsAsync(CancellationToken ct = default)
    {
        var pending = await _dbContext.ProductExternalMappings
            .Where(m => m.SyncStatus != ExternalSyncStatus.Synced)
            .ToListAsync(ct);

        var resolved = 0;
        foreach (var mapping in pending)
        {
            var desiredPublished = mapping.SyncStatus == ExternalSyncStatus.PendingUnarchive;
            if (await TrySyncMappingAsync(mapping, desiredPublished))
                resolved++;
        }

        if (resolved > 0)
            await _dbContext.SaveChangesAsync(ct);

        return resolved;
    }

    private async Task<ArchiveResult> ChangeArchiveStateAsync(int productId, bool archive)
    {
        var product = await _dbContext.Products
            .Include(p => p.ExternalMappings)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product is null)
            return ArchiveResult.NotFound();

        if (archive)
            product.Archive(_clock.UtcNow);
        else
            product.Unarchive();

        var failedStores = new List<string>();
        foreach (var mapping in product.ExternalMappings)
        {
            var desiredPublished = !archive;
            if (!await TrySyncMappingAsync(mapping, desiredPublished))
                failedStores.Add(mapping.StoreName);
        }

        await _dbContext.SaveChangesAsync();
        return ArchiveResult.Success(failedStores);
    }

    private async Task<bool> TrySyncMappingAsync(ProductExternalMapping mapping, bool desiredPublished)
    {
        var store = _registry.GetStoreByName(mapping.StoreName);
        if (store is null || !store.Enabled)
        {
            // Store no longer configured — leave the flag so the user can resolve it.
            mapping.SyncStatus = desiredPublished ? ExternalSyncStatus.PendingUnarchive : ExternalSyncStatus.PendingArchive;
            mapping.LastSyncError = $"Store '{mapping.StoreName}' is not configured or disabled.";
            mapping.LastSyncAttemptAt = _clock.UtcNow;
            return false;
        }

        try
        {
            var integration = _registry.CreateIntegration(store);
            await integration.SetProductPublishedAsync(mapping.ExternalId, desiredPublished);

            mapping.SyncStatus = ExternalSyncStatus.Synced;
            mapping.LastSyncError = null;
            mapping.LastSyncAttemptAt = _clock.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            mapping.SyncStatus = desiredPublished ? ExternalSyncStatus.PendingUnarchive : ExternalSyncStatus.PendingArchive;
            mapping.LastSyncError = ex.Message;
            mapping.LastSyncAttemptAt = _clock.UtcNow;
            _logger.LogWarning(ex,
                "Failed to {Action} product mapping (productId={ProductId}, store={Store}, externalId={ExternalId}). Marked as pending.",
                desiredPublished ? "unarchive" : "archive",
                mapping.ProductId, mapping.StoreName, mapping.ExternalId);
            return false;
        }
    }
}

public sealed record ArchiveResult(bool Found, IReadOnlyList<string> FailedStores)
{
    public bool Succeeded => Found;
    public bool FullySynced => Found && FailedStores.Count == 0;

    public static ArchiveResult NotFound() => new(false, Array.Empty<string>());
    public static ArchiveResult Success(IEnumerable<string> failedStores)
        => new(true, failedStores.ToList());
}
