using System.Net;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Characterization tests for SyncController.
/// Documents current response behavior before Fase 1 adds explicit [Authorize].
/// </summary>
public class SyncControllerTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;

    public SyncControllerTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SyncProducts_WhenAuthenticated_WithNoStoreConfigured_Returns400()
    {
        var response = await _client.PostAsync("/api/sync/products", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessOrders_WhenAuthenticated_WithNoStoreConfigured_Returns400()
    {
        var response = await _client.PostAsync("/api/sync/orders", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListStores_WhenAuthenticated_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/sync/stores");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task PushStock_WhenAuthenticated_WithNoStoreConfigured_Returns400()
    {
        var response = await _client.PostAsync("/api/sync/push-stock/1", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PushProduct_WhenAuthenticated_WithNoStoreConfigured_Returns400()
    {
        var response = await _client.PostAsync("/api/sync/push-product/1", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
