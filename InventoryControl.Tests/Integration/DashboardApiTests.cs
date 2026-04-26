using System.Net;
using System.Text.Json;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Characterization tests for HomeController dashboard API endpoints.
/// Documents expected response shape for authenticated requests.
/// NOTE: Fase 1 will add tests verifying these endpoints return 401 when unauthenticated.
/// </summary>
public class DashboardApiTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;

    public DashboardApiTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MovementsByMonth_WhenAuthenticated_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/dashboard/movements-by-month");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task TopSellers_WhenAuthenticated_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/dashboard/top-sellers");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task StockByCategory_WhenAuthenticated_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/dashboard/stock-by-category");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task RecentMovements_WhenAuthenticated_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/movements/recent");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Theory]
    [InlineData("/api/dashboard/movements-by-month")]
    [InlineData("/api/dashboard/top-sellers")]
    [InlineData("/api/dashboard/stock-by-category")]
    [InlineData("/api/movements/recent")]
    public async Task DashboardEndpoint_WhenAuthenticated_ReturnsApplicationJson(string path)
    {
        var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
