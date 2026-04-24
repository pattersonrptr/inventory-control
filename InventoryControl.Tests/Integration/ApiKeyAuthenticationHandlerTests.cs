using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Characterization tests for ApiKeyAuthenticationHandler.
/// Documents current behavior before Fase 1 refactors per-key role support.
/// </summary>
public class ApiKeyAuthenticationHandlerTests : IClassFixture<ApiKeyWebAppFactory>
{
    private readonly HttpClient _client;

    public ApiKeyAuthenticationHandlerTests(ApiKeyWebAppFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ApiEndpoint_WithValidApiKey_Returns200()
    {
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);

        var response = await _client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_WithInvalidApiKey_Returns401()
    {
        _client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await _client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_WithMissingApiKey_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_ValidApiKey_ResponseIsJson()
    {
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);

        var response = await _client.GetAsync("/api/v1/products");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
