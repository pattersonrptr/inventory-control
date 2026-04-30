using InventoryControl.Domain.Products;
using InventoryControl.Infrastructure.Integrations.Abstractions;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryControl.Infrastructure.Integrations;

public interface IProductImageUploader
{
    Task<int> UploadPendingAsync(
        int productId,
        IStoreIntegration integration,
        string externalProductId,
        CancellationToken ct = default);
}

/// <summary>
/// Reads local product images from disk and pushes the ones that have no
/// ExternalImageId yet to the external store. Persists the platform-assigned
/// id and URL back so subsequent runs are no-ops (idempotent).
/// </summary>
public class ProductImageUploader : IProductImageUploader
{
    private const long MaxBytes = 8 * 1024 * 1024;

    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;
    private readonly ILogger<ProductImageUploader> _logger;

    public ProductImageUploader(
        IWebHostEnvironment env,
        AppDbContext db,
        ILogger<ProductImageUploader> logger)
    {
        _env = env;
        _db = db;
        _logger = logger;
    }

    public async Task<int> UploadPendingAsync(
        int productId,
        IStoreIntegration integration,
        string externalProductId,
        CancellationToken ct = default)
    {
        var pending = await _db.ProductImages
            .Where(pi => pi.ProductId == productId && pi.ExternalImageId == null)
            .OrderBy(pi => pi.DisplayOrder)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        var uploaded = 0;
        foreach (var image in pending)
        {
            var fullPath = Path.Combine(_env.WebRootPath, image.ImagePath.TrimStart('/'));
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning(
                    "Cannot upload image id={ImageId}: file not found at {Path}.",
                    image.Id, fullPath);
                continue;
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > MaxBytes)
            {
                _logger.LogWarning(
                    "Skipping image id={ImageId} — size {Size} exceeds limit {Limit}.",
                    image.Id, fileInfo.Length, MaxBytes);
                continue;
            }

            try
            {
                var bytes = await File.ReadAllBytesAsync(fullPath, ct);
                var fileName = Path.GetFileName(fullPath);

                var result = await integration.UploadProductImageAsync(
                    externalProductId, bytes, fileName, image.DisplayOrder, ct);

                if (result is null)
                {
                    _logger.LogWarning(
                        "Upload of image id={ImageId} returned null for product {ProductId}.",
                        image.Id, productId);
                    continue;
                }

                image.ExternalImageId = result.ExternalId;
                image.ExternalUrl = result.Url;
                uploaded++;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                _logger.LogWarning(ex,
                    "Failed to upload image id={ImageId} for product {ProductId}. Continuing with the rest.",
                    image.Id, productId);
            }
        }

        if (uploaded > 0) await _db.SaveChangesAsync(ct);
        return uploaded;
    }
}
