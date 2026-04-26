using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

public class CategoriesApiControllerTests : IClassFixture<ApiKeyWebAppFactory>
{
    private readonly ApiKeyWebAppFactory _factory;
    private readonly HttpClient _client;

    public CategoriesApiControllerTests(ApiKeyWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);
    }

    [Fact]
    public async Task GetAll_Returns200WithPagedResult()
    {
        var response = await _client.GetAsync("/api/v1/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Create_ValidCategory_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            Name = "Cat_" + Guid.NewGuid(),
            Description = "Test"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new { Name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingCategory_Returns200()
    {
        var created = await CreateCategoryAsync();
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.GetAsync($"/api/v1/categories/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GetById_NonExisting_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingCategory_Returns200WithUpdatedName()
    {
        var created = await CreateCategoryAsync();
        var id = created.GetProperty("id").GetInt32();
        var newName = "Updated_" + Guid.NewGuid();

        var response = await _client.PutAsJsonAsync($"/api/v1/categories/{id}", new { Name = newName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(newName, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_NonExisting_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/v1/categories/999999", new { Name = "X" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingCategory_Returns204()
    {
        var created = await CreateCategoryAsync();
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.DeleteAsync($"/api/v1/categories/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExisting_Returns404()
    {
        var response = await _client.DeleteAsync("/api/v1/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement> CreateCategoryAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            Name = "Cat_" + Guid.NewGuid(),
            Description = "test"
        });
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
