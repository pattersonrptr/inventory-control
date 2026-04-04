using System.Net;

namespace ControleEstoque.Tests.Integration;

public class HomeControllerTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;

    public HomeControllerTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ReturnsSuccessAndHtml()
    {
        var response = await _client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Inventory Control", content);
    }
}
