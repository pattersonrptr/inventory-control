
using InventoryControl.Tests.Fixtures;

namespace InventoryControl.Tests.Unit.Repositories;

/// <summary>
/// TDD tests for CategoryRepository.GetAllForListAsync.
/// Old GetAllAsync includes Products (collection) — expensive.
/// GetAllForListAsync must NOT load products.
/// </summary>
public class CategoryRepositoryListTests
{
    private readonly DatabaseFixture _fixture = new();

    [Fact]
    public async Task GetAllForListAsync_ReturnsCorrectPage()
    {
        using var ctx = _fixture.CreateContext();
        for (var i = 1; i <= 10; i++)
            ctx.Categories.Add(new Category { Id = i, Name = $"Cat {i:D2}" });
        await ctx.SaveChangesAsync();
        var repo = new CategoryRepository(ctx);

        var result = await repo.GetAllForListAsync(page: 1, pageSize: 4);

        Assert.Equal(4, result.Items.Count);
        Assert.Equal(10, result.TotalCount);
    }

    [Fact]
    public async Task GetAllForListAsync_DoesNotLoadProducts()
    {
        using var ctx = _fixture.CreateContext();
        ctx.Categories.Add(new Category { Id = 1, Name = "Electronics" });
        ctx.Categories.Add(new Category { Id = 2, Name = "Books" });
        await ctx.SaveChangesAsync();
        var repo = new CategoryRepository(ctx);

        var result = await repo.GetAllForListAsync(1, 10);

        Assert.All(result.Items, c => Assert.Empty(c.Products));
    }

    [Fact]
    public async Task GetAllForListAsync_SecondPageReturnsDistinctItems()
    {
        using var ctx = _fixture.CreateContext();
        for (var i = 1; i <= 6; i++)
            ctx.Categories.Add(new Category { Id = i, Name = $"Cat {i:D2}" });
        await ctx.SaveChangesAsync();
        var repo = new CategoryRepository(ctx);

        var p1 = await repo.GetAllForListAsync(1, 3);
        var p2 = await repo.GetAllForListAsync(2, 3);

        Assert.Empty(p1.Items.Select(c => c.Id).Intersect(p2.Items.Select(c => c.Id)));
    }
}
