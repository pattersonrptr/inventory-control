namespace InventoryControl.Domain.Products;

/// <summary>
/// Retries publish/unpublish calls for product mappings left in a non-Synced state
/// after a previous failure. Lives in Domain so Infrastructure background jobs can
/// depend on it without referencing Features.
/// </summary>
public interface IProductArchiveRetrier
{
    Task<int> RetryPendingSyncsAsync(CancellationToken ct = default);
}
