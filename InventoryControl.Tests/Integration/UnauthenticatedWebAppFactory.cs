using InventoryControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Factory that uses InMemory DB but does NOT replace authentication.
/// Used to verify that endpoints return 401 for unauthenticated callers.
/// </summary>
public class UnauthenticatedWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                          || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            var dbName = "UnauthTestDb_" + Guid.NewGuid();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Development");
    }
}
