using InventoryControl.Domain.Products;
using InventoryControl.Infrastructure.Integrations.Abstractions;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryControl.Infrastructure.Integrations;

public interface IProductImageDownloader
{
    Task<int> DownloadAndSaveAsync(int productId, IEnumerable<ExternalImage> images, CancellationToken ct = default);
}

public class ProductImageDownloader : IProductImageDownloader
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };
    private const long MaxBytes = 8 * 1024 * 1024;

    private readonly IHttpClientFactory _httpFactory;
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;
    private readonly ILogger<ProductImageDownloader> _logger;

    public ProductImageDownloader(
        IHttpClientFactory httpFactory,
        IWebHostEnvironment env,
        AppDbContext db,
        ILogger<ProductImageDownloader> logger)
    {
        _httpFactory = httpFactory;
        _env = env;
        _db = db;
        _logger = logger;
    }

    public async Task<int> DownloadAndSaveAsync(int productId, IEnumerable<ExternalImage> images, CancellationToken ct = default)
    {
        var imageList = images.ToList();
        if (imageList.Count == 0) return 0;

        var existingByExtId = await _db.ProductImages
            .Where(pi => pi.ProductId == productId && pi.ExternalImageId != null)
            .ToDictionaryAsync(pi => pi.ExternalImageId!, ct);

        var hasAnyExisting = await _db.ProductImages.AnyAsync(pi => pi.ProductId == productId, ct);

        var uploadsDir = Path.Combine(_env.WebRootPath, "images", "products");
        Directory.CreateDirectory(uploadsDir);

        var http = _httpFactory.CreateClient("ProductImageDownloader");
        http.Timeout = TimeSpan.FromSeconds(30);

        var saved = 0;
        foreach (var img in imageList.OrderBy(i => i.Position))
        {
            if (existingByExtId.ContainsKey(img.ExternalId))
                continue;

            try
            {
                using var response = await http.GetAsync(img.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!AllowedContentTypes.Contains(contentType))
                {
                    _logger.LogWarning(
                        "Skipping image {Url} — unsupported content type '{ContentType}'.",
                        img.Url, contentType);
                    continue;
                }

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength is > MaxBytes)
                {
                    _logger.LogWarning(
                        "Skipping image {Url} — size {Size} exceeds limit {Limit}.",
                        img.Url, contentLength, MaxBytes);
                    continue;
                }

                var ext = ExtensionFromContentType(contentType);
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                await using (var src = await response.Content.ReadAsStreamAsync(ct))
                await using (var dst = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    await src.CopyToAsync(dst, ct);

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MaxBytes)
                {
                    File.Delete(filePath);
                    _logger.LogWarning(
                        "Discarding image {Url} after download — size {Size} exceeds limit {Limit}.",
                        img.Url, fileInfo.Length, MaxBytes);
                    continue;
                }

                _db.ProductImages.Add(new ProductImage
                {
                    ProductId = productId,
                    ImagePath = $"/images/products/{fileName}",
                    DisplayOrder = img.Position,
                    IsPrimary = !hasAnyExisting && saved == 0,
                    ExternalImageId = img.ExternalId,
                    ExternalUrl = img.Url
                });
                saved++;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                _logger.LogWarning(ex,
                    "Failed to download image {Url} for product {ProductId}. Skipping.",
                    img.Url, productId);
            }
        }

        if (saved > 0) await _db.SaveChangesAsync(ct);
        return saved;
    }

    private static string ExtensionFromContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin"
    };
}
