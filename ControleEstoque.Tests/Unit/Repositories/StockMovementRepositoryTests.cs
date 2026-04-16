using ControleEstoque.Models;
using ControleEstoque.Repositories;
using ControleEstoque.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Tests.Unit.Repositories;

public class StockMovementRepositoryTests
{
    private readonly DatabaseFixture _fixture = new();

    private async Task SeedBaseDataAsync(Data.AppDbContext context)
    {
        context.Categories.Add(TestDataBuilder.CreateCategory());
        context.Suppliers.Add(TestDataBuilder.CreateSupplier());
        context.Products.Add(TestDataBuilder.CreateProduct());
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_PersistsMovement()
    {
        using var context = _fixture.CreateContext();
        await SeedBaseDataAsync(context);

        var repo = new StockMovementRepository(context);
        await repo.AddAsync(TestDataBuilder.CreateEntry());

        Assert.Equal(1, await context.StockMovements.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMovementsOrderedByDateDesc()
    {
        using var context = _fixture.CreateContext();
        await SeedBaseDataAsync(context);
        context.StockMovements.Add(new StockMovement
        {
            ProductId = 1, Type = MovementType.Entry, Quantity = 5,
            Date = new DateTime(2026, 1, 1)
        });
        context.StockMovements.Add(new StockMovement
        {
            ProductId = 1, Type = MovementType.Exit, Quantity = 2,
            Date = new DateTime(2026, 3, 1), ExitReason = ExitReason.Sale
        });
        await context.SaveChangesAsync();

        var repo = new StockMovementRepository(context);
        var movements = (await repo.GetAllAsync()).ToList();

        Assert.Equal(2, movements.Count);
        Assert.True(movements[0].Date >= movements[1].Date);
    }

    [Fact]
    public async Task GetByProductAsync_FiltersCorrectly()
    {
        using var context = _fixture.CreateContext();
        await SeedBaseDataAsync(context);
        context.Categories.Add(TestDataBuilder.CreateCategory(id: 2, name: "Other"));
        context.Products.Add(TestDataBuilder.CreateProduct(id: 2, name: "Other Product", categoryId: 2, sku: "OTHER-001"));
        context.StockMovements.Add(TestDataBuilder.CreateEntry(productId: 1));
        context.StockMovements.Add(TestDataBuilder.CreateEntry(productId: 2));
        await context.SaveChangesAsync();

        var repo = new StockMovementRepository(context);
        var movements = (await repo.GetByProductAsync(1)).ToList();

        Assert.Single(movements);
        Assert.All(movements, m => Assert.Equal(1, m.ProductId));
    }

    [Fact]
    public async Task GetByMonthYearAsync_FiltersCorrectly()
    {
        using var context = _fixture.CreateContext();
        await SeedBaseDataAsync(context);
        context.StockMovements.Add(new StockMovement
        {
            ProductId = 1, Type = MovementType.Entry, Quantity = 10,
            Date = new DateTime(2026, 3, 15)
        });
        context.StockMovements.Add(new StockMovement
        {
            ProductId = 1, Type = MovementType.Entry, Quantity = 5,
            Date = new DateTime(2026, 4, 1)
        });
        await context.SaveChangesAsync();

        var repo = new StockMovementRepository(context);
        var movements = (await repo.GetByMonthYearAsync(3, 2026)).ToList();

        Assert.Single(movements);
        Assert.Equal(3, movements[0].Date.Month);
    }
}
