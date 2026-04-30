using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Smoke tests covering the archive/unarchive feature end-to-end.
/// Uses the API surface (no external store configured), so archive operations
/// succeed locally and report fullySynced=true (no mappings to push).
/// </summary>
public class ProductArchiveIntegrationTests : IClassFixture<ApiKeyWebAppFactory>
{
    private readonly ApiKeyWebAppFactory _factory;
    private readonly HttpClient _client;

    public ProductArchiveIntegrationTests(ApiKeyWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);
    }

    [Fact]
    public async Task Archive_ExistingProduct_SetsIsArchivedTrue()
    {
        var (productId, _) = await SeedProductAsync();

        var response = await _client.PostAsync($"/api/v1/products/{productId}/archive", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("fullySynced").GetBoolean());

        var product = await GetProductFromDbAsync(productId);
        Assert.True(product.IsArchived);
        Assert.NotNull(product.ArchivedAt);
    }

    [Fact]
    public async Task Archive_NonExistingProduct_Returns404()
    {
        var response = await _client.PostAsync("/api/v1/products/999999/archive", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unarchive_ArchivedProduct_RestoresActiveState()
    {
        var (productId, _) = await SeedProductAsync();
        await _client.PostAsync($"/api/v1/products/{productId}/archive", null);

        var response = await _client.PostAsync($"/api/v1/products/{productId}/unarchive", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await GetProductFromDbAsync(productId);
        Assert.False(product.IsArchived);
        Assert.Null(product.ArchivedAt);
    }

    [Fact]
    public async Task GetAll_Default_ExcludesArchivedProducts()
    {
        var (activeId, activeName) = await SeedProductAsync();
        var (archivedId, archivedName) = await SeedProductAsync();
        await _client.PostAsync($"/api/v1/products/{archivedId}/archive", null);

        var response = await _client.GetAsync("/api/v1/products?pageSize=100");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(activeName, body);
        Assert.DoesNotContain(archivedName, body);
    }

    [Fact]
    public async Task GetAll_IncludeArchivedTrue_ListsArchivedProducts()
    {
        var (archivedId, archivedName) = await SeedProductAsync();
        await _client.PostAsync($"/api/v1/products/{archivedId}/archive", null);

        var response = await _client.GetAsync("/api/v1/products?pageSize=100&includeArchived=true");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(archivedName, body);
    }

    [Fact]
    public async Task StockEntry_OnArchivedProduct_Returns409Conflict()
    {
        var (productId, _) = await SeedProductAsync();
        await _client.PostAsync($"/api/v1/products/{productId}/archive", null);

        var response = await _client.PostAsJsonAsync("/api/v1/stock-movements/entry", new
        {
            ProductId = productId,
            Quantity = 5
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ProductArchived", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task StockExit_OnArchivedProduct_Returns409Conflict()
    {
        var (productId, _) = await SeedProductAsync(initialStock: 10);
        await _client.PostAsync($"/api/v1/products/{productId}/archive", null);

        var response = await _client.PostAsJsonAsync("/api/v1/stock-movements/exit", new
        {
            ProductId = productId,
            Quantity = 1
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ProductArchived", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task StockEntry_AfterUnarchive_Succeeds()
    {
        var (productId, _) = await SeedProductAsync();
        await _client.PostAsync($"/api/v1/products/{productId}/archive", null);
        await _client.PostAsync($"/api/v1/products/{productId}/unarchive", null);

        var response = await _client.PostAsJsonAsync("/api/v1/stock-movements/entry", new
        {
            ProductId = productId,
            Quantity = 7
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await GetProductFromDbAsync(productId);
        Assert.Equal(7, product.CurrentStock);
    }

    [Fact]
    public async Task BelowMinimum_ExcludesArchivedProducts()
    {
        var (productId, name) = await SeedProductAsync(minimumStock: 10, initialStock: 0);
        await _client.PostAsync($"/api/v1/products/{productId}/archive", null);

        var response = await _client.GetAsync("/api/v1/products/below-minimum");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(name, body);
    }

    private async Task<(int Id, string Name)> SeedProductAsync(
        int minimumStock = 0, int initialStock = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = new Category { Name = "Cat_" + Guid.NewGuid() };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var name = "Archive_" + Guid.NewGuid();
        var product = new Product
        {
            Name = name,
            CostPrice = 10m,
            SellingPrice = 20m,
            CurrentStock = initialStock,
            MinimumStock = minimumStock,
            CategoryId = category.Id
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, name);
    }

    private async Task<Product> GetProductFromDbAsync(int productId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.AsNoTracking().FirstAsync(p => p.Id == productId);
    }
}
