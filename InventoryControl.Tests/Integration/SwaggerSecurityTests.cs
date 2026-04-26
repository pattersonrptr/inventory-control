using System.Net;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

public class SwaggerSecurityTests
{
    [Fact]
    public async Task Swagger_InDevelopment_IsAccessibleAnonymously()
    {
        using var factory = new WebAppFactory(); // Development env, auto-auth
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_InProduction_IsNotAccessible()
    {
        using var factory = new ProductionWebAppFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/swagger/index.html");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// Factory running in Production environment with no auto-authentication.
/// ConfigureAppConfiguration injects a dummy connection string so the startup
/// null-check in Program.cs doesn't throw before ConfigureServices can replace DbContext.
/// </summary>
public class ProductionWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // Must be set before the host builds — Program.cs throws if DefaultConnection is null.
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=:memory:");

        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                          || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in descriptors)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("ProdSwaggerTestDb_" + Guid.NewGuid())
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }
}
