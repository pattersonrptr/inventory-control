namespace ControleEstoque.Tests.Integration;

public class ReportsControllerTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;

    public ReportsControllerTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BelowMinimum_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/Reports/BelowMinimum");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Monthly_WithoutParams_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/Reports/Monthly");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Monthly_WithParams_ReturnsFilteredReport()
    {
        var response = await _client.GetAsync("/Reports/Monthly?month=3&year=2026");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("2026", content);
    }
}
