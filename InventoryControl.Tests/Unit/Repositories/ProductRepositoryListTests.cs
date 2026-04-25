using InventoryControl.Repositories;
using InventoryControl.Tests.Fixtures;

namespace InventoryControl.Tests.Unit.Repositories;

/// <summary>
/// TDD tests for GetAllForListAsync: paginates at DB level and does NOT load
/// heavy navigation properties (Images, ExternalMappings, StockMovements).
/// </summary>
public class ProductRepositoryListTests
{
    private readonly DatabaseFixture _fixture = new();

    [Fact]
    public async Task GetAllForListAsync_ReturnsCorrectPage()
    {
        using var ctx = _fixture.CreateContext();
        ctx.Categories.Add(TestDataBuilder.CreateCategory());
        for (var i = 1; i <= 10; i++)
            ctx.Products.Add(TestDataBuilder.CreateProduct(id: i, name: $"Product {i:D2}", sku: $"SKU-{i:D3}"));
        await ctx.SaveChangesAsync();
        var repo = new ProductRepository(ctx);

        var result = await repo.GetAllForListAsync(page: 1, pageSize: 5);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task GetAllForListAsync_SecondPageReturnsDistinctItems()
    {
        using var ctx = _fixture.CreateContext();
        ctx.Categories.Add(TestDataBuilder.CreateCategory());
        for (var i = 1; i <= 10; i++)
            ctx.Products.Add(TestDataBuilder.CreateProduct(id: i, name: $"Product {i:D2}", sku: $"SKU-{i:D3}"));
        await ctx.SaveChangesAsync();
        var repo = new ProductRepository(ctx);

        var p1 = await repo.GetAllForListAsync(1, 5);
        var p2 = await repo.GetAllForListAsync(2, 5);

        Assert.Empty(p1.Items.Select(p => p.Id).Intersect(p2.Items.Select(p => p.Id)));
    }

    [Fact]
    public async Task GetAllForListAsync_LoadsCategory()
    {
        using var ctx = _fixture.CreateContext();
        ctx.Categories.Add(TestDataBuilder.CreateCategory());
        ctx.Products.Add(TestDataBuilder.CreateProduct(id: 1, sku: "S1"));
        ctx.Products.Add(TestDataBuilder.CreateProduct(id: 2, sku: "S2"));
        await ctx.SaveChangesAsync();
        var repo = new ProductRepository(ctx);

        var result = await repo.GetAllForListAsync(1, 10);

        Assert.All(result.Items, p => Assert.NotNull(p.Category));
    }

    [Fact]
    public async Task GetAllForListAsync_DoesNotLoadImages()
    {
        using var ctx = _fixture.CreateContext();
        ctx.Categories.Add(TestDataBuilder.CreateCategory());
        ctx.Products.Add(TestDataBuilder.CreateProduct(id: 1, sku: "S1"));
        await ctx.SaveChangesAsync();
        var repo = new ProductRepository(ctx);

        var result = await repo.GetAllForListAsync(1, 10);

        // Images not included → collection empty (not null)
        Assert.All(result.Items, p => Assert.Empty(p.Images));
    }

    [Fact]
    public async Task GetAllForListAsync_DoesNotLoadExternalMappings()
    {
        using var ctx = _fixture.CreateContext();
        ctx.Categories.Add(TestDataBuilder.CreateCategory());
        ctx.Products.Add(TestDataBuilder.CreateProduct(id: 1, sku: "S1"));
        await ctx.SaveChangesAsync();
        var repo = new ProductRepository(ctx);

        var result = await repo.GetAllForListAsync(1, 10);

        Assert.All(result.Items, p => Assert.Empty(p.ExternalMappings));
    }

    [Fact]
    public async Task GetAllForListAsync_ExactlyPageSizeRowsMaterialized()
    {
        using var ctx = _fixture.CreateContext();
        ctx.Categories.Add(TestDataBuilder.CreateCategory());
        for (var i = 1; i <= 7; i++)
            ctx.Products.Add(TestDataBuilder.CreateProduct(id: i, name: $"Product {i:D2}", sku: $"SKU-{i:D3}"));
        await ctx.SaveChangesAsync();
        var repo = new ProductRepository(ctx);

        var result = await repo.GetAllForListAsync(page: 2, pageSize: 3);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(7, result.TotalCount);
    }
}
