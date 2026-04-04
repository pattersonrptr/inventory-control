using ControleEstoque.Data;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() { }
}
