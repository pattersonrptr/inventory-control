using ControleEstoque.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    private readonly List<SqliteConnection> _connections = new();

    public AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose()
    {
        foreach (var connection in _connections)
            connection.Dispose();
    }
}
