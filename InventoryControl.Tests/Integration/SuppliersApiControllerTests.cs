using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InventoryControl.Tests.Integration;

public class SuppliersApiControllerTests : IClassFixture<ApiKeyWebAppFactory>
{
    private readonly ApiKeyWebAppFactory _factory;
    private readonly HttpClient _client;

    public SuppliersApiControllerTests(ApiKeyWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);
    }

    [Fact]
    public async Task GetAll_Returns200WithPagedResult()
    {
        var response = await _client.GetAsync("/api/v1/suppliers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Create_ValidSupplier_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/suppliers", new
        {
            Name = "Sup_" + Guid.NewGuid(), Cnpj = "00.000.000/0001-00", Email = "sup@test.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/suppliers", new { Name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingSupplier_Returns200()
    {
        var created = await CreateSupplierAsync();
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.GetAsync($"/api/v1/suppliers/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GetById_NonExisting_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/suppliers/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingSupplier_Returns200WithUpdatedName()
    {
        var created = await CreateSupplierAsync();
        var id = created.GetProperty("id").GetInt32();
        var newName = "Updated_" + Guid.NewGuid();

        var response = await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}", new { Name = newName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(newName, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_NonExisting_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/v1/suppliers/999999", new { Name = "X" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingSupplier_Returns204()
    {
        var created = await CreateSupplierAsync();
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.DeleteAsync($"/api/v1/suppliers/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExisting_Returns404()
    {
        var response = await _client.DeleteAsync("/api/v1/suppliers/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement> CreateSupplierAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/suppliers", new
        {
            Name = "Sup_" + Guid.NewGuid(), Cnpj = "00.000.000/0001-00"
        });
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
