using System.Net;
using System.Text.Json;
using InventoryControl.Infrastructure.Integrations.Abstractions;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Smoke tests covering the bidirectional product sync (puller).
/// Each test gets a fresh factory so the FakeStoreIntegration state doesn't leak.
/// </summary>
public class PullerSyncIntegrationTests
{
    [Fact]
    public async Task SyncProducts_NoSkuMatch_CreatesLocalProductWithCostPriceZero()
    {
        await using var factory = new PullerWebAppFactory();
        var client = factory.CreateClient();
        factory.FakeStore.Products.Add(new ExternalProduct
        {
            ExternalId = "ext-100",
            Sku = "SKU-NEW",
            Name = "Pulled From Store",
            Price = 49.90m,
            Stock = 3
        });

        var response = await client.PostAsync("/api/sync/products", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("created").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("needsCostReview").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await db.Products.AsNoTracking()
            .Include(p => p.ExternalMappings)
            .FirstAsync(p => p.Sku == "SKU-NEW");
        Assert.Equal("Pulled From Store", created.Name);
        Assert.Equal(0m, created.CostPrice);
        Assert.Equal(49.90m, created.SellingPrice);
        Assert.Single(created.ExternalMappings);
        Assert.Equal("ext-100", created.ExternalMappings.First().ExternalId);

        var fallback = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Sem categoria");
        Assert.NotNull(fallback);
    }

    [Fact]
    public async Task SyncProducts_MatchingSku_LinksWithoutOverwritingLocal()
    {
        await using var factory = new PullerWebAppFactory();
        var client = factory.CreateClient();

        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = new Category { Name = "Cat_local" };
            db.Categories.Add(category);
            db.Products.Add(new Product
            {
                Name = "Local Untouched",
                CostPrice = 5m,
                SellingPrice = 10m,
                Sku = "SKU-LINK",
                CategoryId = category.Id
            });
            await db.SaveChangesAsync();
            productId = (await db.Products.FirstAsync(p => p.Sku == "SKU-LINK")).Id;
        }

        factory.FakeStore.Products.Add(new ExternalProduct
        {
            ExternalId = "ext-link",
            Sku = "SKU-LINK",
            Name = "Local Untouched",
            Price = 10m,
            Stock = 0
        });

        var response = await client.PostAsync("/api/sync/products", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("linked").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("conflicts").GetInt32());

        using var verify = factory.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db2.Products.AsNoTracking()
            .Include(p => p.ExternalMappings)
            .FirstAsync(p => p.Id == productId);
        Assert.Equal("Local Untouched", product.Name);
        Assert.Equal(5m, product.CostPrice);
        Assert.Single(product.ExternalMappings);
        Assert.False(product.ExternalMappings.First().HasConflict);
    }

    [Fact]
    public async Task SyncProducts_DivergentNameOrPrice_FlagsConflictMapping()
    {
        await using var factory = new PullerWebAppFactory();
        var client = factory.CreateClient();

        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = new Category { Name = "Cat_conflict" };
            db.Categories.Add(category);
            db.Products.Add(new Product
            {
                Name = "Local Name",
                CostPrice = 5m,
                SellingPrice = 10m,
                Sku = "SKU-DIV",
                CategoryId = category.Id
            });
            await db.SaveChangesAsync();
            productId = (await db.Products.FirstAsync(p => p.Sku == "SKU-DIV")).Id;
        }

        factory.FakeStore.Products.Add(new ExternalProduct
        {
            ExternalId = "ext-div",
            Sku = "SKU-DIV",
            Name = "External Name",
            Price = 15m,
            Stock = 0
        });

        var response = await client.PostAsync("/api/sync/products", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("conflicts").GetInt32());

        using var verify = factory.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var mapping = await db2.ProductExternalMappings.AsNoTracking()
            .FirstAsync(m => m.ProductId == productId);
        Assert.True(mapping.HasConflict);
        Assert.NotNull(mapping.ConflictDetails);
        Assert.Contains("Name", mapping.ConflictDetails);
        Assert.Contains("Price", mapping.ConflictDetails);
    }

    [Fact]
    public async Task SyncProducts_ResponseSummary_IncludesAllCounters()
    {
        await using var factory = new PullerWebAppFactory();
        var client = factory.CreateClient();
        factory.FakeStore.Products.Add(new ExternalProduct
        {
            ExternalId = "ext-99",
            Sku = "SKU-FRESH",
            Name = "Fresh",
            Price = 1m,
            Stock = 0
        });

        var response = await client.PostAsync("/api/sync/products", null);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("linked", out _));
        Assert.True(root.TryGetProperty("created", out _));
        Assert.True(root.TryGetProperty("conflicts", out _));
        Assert.True(root.TryGetProperty("needsCostReview", out _));
        Assert.True(root.TryGetProperty("total", out _));
        Assert.Equal(1, root.GetProperty("total").GetInt32());
    }
}
