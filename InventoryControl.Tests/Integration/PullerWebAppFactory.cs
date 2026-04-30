using InventoryControl.Infrastructure.Integrations;
using InventoryControl.Infrastructure.Integrations.Abstractions;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// WebApplicationFactory wiring a fake e-commerce platform (`fake-platform`) so the
/// SyncController can exercise the puller end-to-end without external HTTP calls.
/// Mutate <see cref="FakeStore"/> from tests to stage external products / categories.
/// </summary>
public class PullerWebAppFactory : WebApplicationFactory<Program>
{
    public const string StoreName = "fake-store";
    public const string Platform = "fake-platform";

    public FakeStoreIntegration FakeStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // The Stores list is read by Program.cs from configuration BEFORE
            // WebApplicationFactory can inject InMemoryCollection sources, so we
            // override the singleton registration directly.
            var storeListDescriptors = services
                .Where(d => d.ServiceType == typeof(List<IntegrationConfig>))
                .ToList();
            foreach (var descriptor in storeListDescriptors)
                services.Remove(descriptor);
            services.AddSingleton(new List<IntegrationConfig>
            {
                new()
                {
                    Name = StoreName,
                    Platform = Platform,
                    Enabled = true,
                    StoreId = "fake-1",
                    AccessToken = "fake-token",
                    OrderSyncIntervalMinutes = 60
                }
            });


            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                          || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var descriptor in dbDescriptors)
                services.Remove(descriptor);

            var dbName = "PullerTestDb_" + Guid.NewGuid();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Replace the real Nuvemshop platform factory with our fake one.
            var factoryDescriptors = services
                .Where(d => d.ServiceType == typeof(IPlatformFactory))
                .ToList();
            foreach (var descriptor in factoryDescriptors)
                services.Remove(descriptor);
            services.AddSingleton<IPlatformFactory>(new FakePlatformFactory(FakeStore));

            // Replace background hosted services so they don't fire during tests.
            var hostedDescriptors = services
                .Where(d => d.ImplementationType?.Namespace?.Contains("BackgroundJobs") == true
                         || d.ImplementationType?.Name == "OrderSyncBackgroundService")
                .ToList();
            foreach (var descriptor in hostedDescriptors)
                services.Remove(descriptor);

            // Auto-authenticate as Admin so [Authorize] passes.
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Development");
    }
}

public class FakeStoreIntegration : IStoreIntegration
{
    public List<ExternalProduct> Products { get; } = new();
    public List<ExternalCategory> Categories { get; } = new();
    public List<(string ExternalId, bool Published)> PublishedCalls { get; } = new();

    public Task<IEnumerable<ExternalProduct>> GetProductsAsync()
        => Task.FromResult<IEnumerable<ExternalProduct>>(Products);

    public Task<IEnumerable<ExternalCategory>> GetCategoriesAsync()
        => Task.FromResult<IEnumerable<ExternalCategory>>(Categories);

    public Task UpdateStockAsync(string externalProductId, int quantity)
        => Task.CompletedTask;

    public Task<IEnumerable<ExternalOrder>> GetOrdersAsync(DateTime since)
        => Task.FromResult<IEnumerable<ExternalOrder>>(Array.Empty<ExternalOrder>());

    public Task<ExternalOrder?> GetOrderAsync(string externalOrderId)
        => Task.FromResult<ExternalOrder?>(null);

    public Task<ExternalProduct?> CreateProductAsync(string name, string? description, decimal price, string? sku, int stock)
        => Task.FromResult<ExternalProduct?>(null);

    public Task<ExternalCategory?> CreateCategoryAsync(string name)
        => Task.FromResult<ExternalCategory?>(null);

    public Task SetProductPublishedAsync(string externalProductId, bool published)
    {
        PublishedCalls.Add((externalProductId, published));
        return Task.CompletedTask;
    }

    public Task<ExternalImage?> UploadProductImageAsync(
        string externalProductId,
        byte[] content,
        string fileName,
        int position,
        CancellationToken ct = default)
        => Task.FromResult<ExternalImage?>(null);
}

public class FakePlatformFactory : IPlatformFactory
{
    private readonly FakeStoreIntegration _integration;

    public FakePlatformFactory(FakeStoreIntegration integration) => _integration = integration;

    public string PlatformName => PullerWebAppFactory.Platform;

    public IStoreIntegration CreateIntegration(IntegrationConfig config, HttpClient httpClient) => _integration;
}
