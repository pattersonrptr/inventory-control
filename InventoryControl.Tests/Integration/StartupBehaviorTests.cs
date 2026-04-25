using InventoryControl.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Verifies that normal app startup (no --migrate flag) does NOT apply database migrations.
/// Migrations must be triggered explicitly via: dotnet run -- migrate
/// </summary>
public class StartupBehaviorTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"startuptest_{Guid.NewGuid()}.db");

    [Fact]
    public async Task Startup_NormalMode_DoesNotApplyMigrations()
    {
        await using var factory = new NoMigrateWebAppFactory(_dbPath);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Trigger startup
        await client.GetAsync("/health");
        client.Dispose();

        // Release all pooled SQLite connections before inspecting the file
        SqliteConnection.ClearAllPools();

        // Verify that no migration history was written to the SQLite file
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        await conn.CloseAsync();

        Assert.Equal(0L, count);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best effort */ }
    }
}

/// <summary>
/// Factory that uses a real SQLite file to detect whether Program.cs calls Database.Migrate().
/// No test-override of the DB — uses whatever Program.cs wires up (SQLite path-based).
/// </summary>
public class NoMigrateWebAppFactory(string dbPath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbPath}");
        builder.UseEnvironment("Development");

        // Swap only authentication so the app can start without valid credentials
        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                          || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}")
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
                o.DefaultScheme = "Test";
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }
}
