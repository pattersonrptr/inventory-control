
using InventoryControl.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Tests.Unit.Repositories;

public class ProductRepositoryTests
{
    private readonly DatabaseFixture _fixture = new();

    [Fact]
    public async Task GetAllAsync_ReturnsProductsOrderedByName()
    {
        using var context = _fixture.CreateContext();
        var category = TestDataBuilder.CreateCategory();
        context.Categories.Add(category);
        context.Products.Add(TestDataBuilder.CreateProduct(id: 1, name: "Zebra", categoryId: 1, sku: "ZEBRA-001"));
        context.Products.Add(TestDataBuilder.CreateProduct(id: 2, name: "Apple", categoryId: 1, sku: "APPLE-001"));
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var products = (await repo.GetAllAsync()).ToList();

        Assert.Equal(2, products.Count);
        Assert.Equal("Apple", products[0].Name);
        Assert.Equal("Zebra", products[1].Name);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingProduct_ReturnsWithIncludes()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        context.Products.Add(TestDataBuilder.CreateProduct());
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var product = await repo.GetByIdAsync(1);

        Assert.NotNull(product);
        Assert.NotNull(product.Category);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingProduct_ReturnsNull()
    {
        using var context = _fixture.CreateContext();
        var repo = new ProductRepository(context);

        var product = await repo.GetByIdAsync(999);

        Assert.Null(product);
    }

    [Fact]
    public async Task GetBelowMinimumAsync_ReturnsOnlyBelowMinimum()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        context.Products.Add(TestDataBuilder.CreateProduct(id: 1, name: "Low", currentStock: 5, minimumStock: 10, sku: "LOW-001"));
        context.Products.Add(TestDataBuilder.CreateProduct(id: 2, name: "OK", currentStock: 50, minimumStock: 10, sku: "OK-001"));
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var products = (await repo.GetBelowMinimumAsync()).ToList();

        Assert.Single(products);
        Assert.Equal("Low", products[0].Name);
    }

    [Fact]
    public async Task AddAsync_PersistsProduct()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var product = TestDataBuilder.CreateProduct(id: 0);
        await repo.AddAsync(product);

        Assert.Equal(1, await context.Products.CountAsync());
    }

    [Fact]
    public async Task UpdateStockAsync_UpdatesQuantity()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        context.Products.Add(TestDataBuilder.CreateProduct(currentStock: 50));
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        await repo.UpdateStockAsync(1, 30);

        var product = await context.Products.FindAsync(1);
        Assert.Equal(30, product!.CurrentStock);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProduct()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        context.Products.Add(TestDataBuilder.CreateProduct());
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        await repo.DeleteAsync(1);

        Assert.Equal(0, await context.Products.CountAsync());
    }

    [Fact]
    public async Task ExistsAsync_ExistingProduct_ReturnsTrue()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        context.Products.Add(TestDataBuilder.CreateProduct());
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);

        Assert.True(await repo.ExistsAsync(1));
        Assert.False(await repo.ExistsAsync(999));
    }
}
