using System.Net;

namespace InventoryControl.Tests.Integration;

public class ProductsControllerTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;

    public ProductsControllerTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/Products");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_Get_ReturnsForm()
    {
        var response = await _client.GetAsync("/Products/Create");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Details_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/Products/Details/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Edit_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/Products/Edit/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
