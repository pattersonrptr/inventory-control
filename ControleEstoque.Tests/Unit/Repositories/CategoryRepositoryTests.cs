using ControleEstoque.Repositories;
using ControleEstoque.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Tests.Unit.Repositories;

public class CategoryRepositoryTests
{
    private readonly DatabaseFixture _fixture = new();

    [Fact]
    public async Task GetAllAsync_ReturnsCategoriesOrderedByName()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory(id: 1, name: "Zebra"));
        context.Categories.Add(TestDataBuilder.CreateCategory(id: 2, name: "Apple"));
        await context.SaveChangesAsync();

        var repo = new CategoryRepository(context);
        var categories = (await repo.GetAllAsync()).ToList();

        Assert.Equal(2, categories.Count);
        Assert.Equal("Apple", categories[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCategory_Returns()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        await context.SaveChangesAsync();

        var repo = new CategoryRepository(context);
        var category = await repo.GetByIdAsync(1);

        Assert.NotNull(category);
        Assert.Equal("Electronics", category.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExisting_ReturnsNull()
    {
        using var context = _fixture.CreateContext();
        var repo = new CategoryRepository(context);

        Assert.Null(await repo.GetByIdAsync(999));
    }

    [Fact]
    public async Task AddAsync_PersistsCategory()
    {
        using var context = _fixture.CreateContext();
        var repo = new CategoryRepository(context);
        var category = TestDataBuilder.CreateCategory(id: 0);
        await repo.AddAsync(category);

        Assert.Equal(1, await context.Categories.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesCategory()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        await context.SaveChangesAsync();

        var repo = new CategoryRepository(context);
        await repo.DeleteAsync(1);

        Assert.Equal(0, await context.Categories.CountAsync());
    }

    [Fact]
    public async Task ExistsAsync_ReturnsCorrectResult()
    {
        using var context = _fixture.CreateContext();
        context.Categories.Add(TestDataBuilder.CreateCategory());
        await context.SaveChangesAsync();

        var repo = new CategoryRepository(context);

        Assert.True(await repo.ExistsAsync(1));
        Assert.False(await repo.ExistsAsync(999));
    }
}
