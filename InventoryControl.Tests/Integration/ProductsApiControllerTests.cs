using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

public class ProductsApiControllerTests : IClassFixture<ApiKeyWebAppFactory>
{
    private readonly ApiKeyWebAppFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ProductsApiControllerTests(ApiKeyWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);
    }

    // ── GET /api/v1/products ─────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Returns200WithPagedResult()
    {
        var response = await _client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("items", out _));
        Assert.True(doc.RootElement.TryGetProperty("totalCount", out _));
    }

    [Fact]
    public async Task GetAll_CreatedProduct_AppearsInList()
    {
        var categoryId = await SeedCategoryAsync();
        var name = "ListProduct_" + Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/v1/products", new
        {
            Name = name,
            CostPrice = 10m,
            SellingPrice = 20m,
            MinimumStock = 0,
            CategoryId = categoryId
        });

        var response = await _client.GetAsync("/api/v1/products?pageSize=100");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(name, body);
    }

    // ── GET /api/v1/products/{id} ────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingProduct_Returns200WithMargin()
    {
        var categoryId = await SeedCategoryAsync();
        var created = await CreateProductAsync(categoryId, costPrice: 60m, sellingPrice: 100m);
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.GetAsync($"/api/v1/products/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, doc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(40m, doc.RootElement.GetProperty("margin").GetDecimal());
    }

    [Fact]
    public async Task GetById_NonExisting_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/products/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/v1/products/below-minimum ──────────────────────────────

    [Fact]
    public async Task GetBelowMinimum_ProductWithLowStock_IsIncluded()
    {
        var categoryId = await SeedCategoryAsync();
        var name = "LowStock_" + Guid.NewGuid();

        // Create with CurrentStock=0, MinimumStock=5 via DB seed (stock defaults to 0)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product
        {
            Name = name,
            CostPrice = 5m,
            SellingPrice = 10m,
            CurrentStock = 0,
            MinimumStock = 5,
            CategoryId = categoryId
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/v1/products/below-minimum");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(name, body);
    }

    // ── POST /api/v1/products ────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidProduct_Returns201WithLocationHeader()
    {
        var categoryId = await SeedCategoryAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            Name = "NewProduct_" + Guid.NewGuid(),
            CostPrice = 10m,
            SellingPrice = 25m,
            MinimumStock = 2,
            CategoryId = categoryId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task Create_MissingName_Returns400()
    {
        var categoryId = await SeedCategoryAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            Name = "",
            CostPrice = 10m,
            SellingPrice = 20m,
            CategoryId = categoryId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NegativeCostPrice_Returns400()
    {
        var categoryId = await SeedCategoryAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            Name = "Bad",
            CostPrice = -1m,
            SellingPrice = 20m,
            CategoryId = categoryId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── PUT /api/v1/products/{id} ────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingProduct_Returns200WithUpdatedName()
    {
        var categoryId = await SeedCategoryAsync();
        var created = await CreateProductAsync(categoryId);
        var id = created.GetProperty("id").GetInt32();
        var updatedName = "Updated_" + Guid.NewGuid();

        var response = await _client.PutAsJsonAsync($"/api/v1/products/{id}", new
        {
            Name = updatedName,
            CostPrice = 15m,
            SellingPrice = 30m,
            MinimumStock = 1,
            CategoryId = categoryId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(updatedName, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_NonExisting_Returns404()
    {
        var categoryId = await SeedCategoryAsync();

        var response = await _client.PutAsJsonAsync("/api/v1/products/999999", new
        {
            Name = "X",
            CostPrice = 1m,
            SellingPrice = 2m,
            MinimumStock = 0,
            CategoryId = categoryId
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /api/v1/products/{id} ─────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingProduct_Returns204()
    {
        var categoryId = await SeedCategoryAsync();
        var created = await CreateProductAsync(categoryId);
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.DeleteAsync($"/api/v1/products/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExisting_Returns404()
    {
        var response = await _client.DeleteAsync("/api/v1/products/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGetById_Returns404()
    {
        var categoryId = await SeedCategoryAsync();
        var created = await CreateProductAsync(categoryId);
        var id = created.GetProperty("id").GetInt32();

        await _client.DeleteAsync($"/api/v1/products/{id}");
        var response = await _client.GetAsync($"/api/v1/products/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PATCH /api/v1/products/{id}/stock ───────────────────────────────

    [Fact]
    public async Task UpdateStock_ExistingProduct_Returns200WithNewQuantity()
    {
        var categoryId = await SeedCategoryAsync();
        var created = await CreateProductAsync(categoryId);
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.PatchAsJsonAsync($"/api/v1/products/{id}/stock", new { Quantity = 42 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(42, doc.RootElement.GetProperty("newQuantity").GetInt32());
    }

    [Fact]
    public async Task UpdateStock_NonExisting_Returns404()
    {
        var response = await _client.PatchAsJsonAsync("/api/v1/products/999999/stock", new { Quantity = 10 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<int> SeedCategoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category { Name = "Cat_" + Guid.NewGuid() };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return cat.Id;
    }

    private async Task<JsonElement> CreateProductAsync(int categoryId, decimal costPrice = 10m, decimal sellingPrice = 20m)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            Name = "Prod_" + Guid.NewGuid(),
            CostPrice = costPrice,
            SellingPrice = sellingPrice,
            MinimumStock = 0,
            CategoryId = categoryId
        });
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
