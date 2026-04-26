using System.Net;
using InventoryControl.Infrastructure.Persistence;
using InventoryControl.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

public class CategoriesControllerTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public CategoriesControllerTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ReturnsSuccessAndContainsCategoriesPage()
    {
        var response = await _client.GetAsync("/Categories");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Categor", content);
    }

    [Fact]
    public async Task Create_Get_ReturnsForm()
    {
        var response = await _client.GetAsync("/Categories/Create");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Details_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/Categories/Edit/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
