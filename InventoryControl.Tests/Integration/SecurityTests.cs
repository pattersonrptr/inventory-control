using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Security tests verifying that protected endpoints reject unauthenticated callers.
/// These were RED before Fase 1 fixes (endpoints returned 302 redirect instead of 401).
/// </summary>
public class SecurityTests : IClassFixture<UnauthenticatedWebAppFactory>
{
    private readonly HttpClient _client;

    public SecurityTests(UnauthenticatedWebAppFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── Dashboard API endpoints ────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/dashboard/movements-by-month")]
    [InlineData("/api/dashboard/top-sellers")]
    [InlineData("/api/dashboard/stock-by-category")]
    [InlineData("/api/movements/recent")]
    public async Task DashboardEndpoint_WhenUnauthenticated_Returns401(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Sync API endpoints ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/sync/products")]
    [InlineData("/api/sync/orders")]
    [InlineData("/api/sync/categories")]
    public async Task SyncPostEndpoint_WhenUnauthenticated_Returns401(string path)
    {
        var response = await _client.PostAsync(path, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncListStores_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/sync/stores");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
