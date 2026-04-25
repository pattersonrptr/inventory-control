using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Characterization and behavior tests for ApiKeyAuthenticationHandler.
/// Covers both legacy (Api:Key) and new (Api:Keys array) config formats.
/// </summary>
public class ApiKeyAuthenticationHandlerTests : IClassFixture<ApiKeyWebAppFactory>
{
    private readonly ApiKeyWebAppFactory _factory;

    public ApiKeyAuthenticationHandlerTests(ApiKeyWebAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    // ── Legacy Api:Key format ──────────────────────────────────────────────────

    [Fact]
    public async Task LegacyApiKey_ValidKey_Returns200()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyApiKey_InvalidKey_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyApiKey_MissingKey_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyApiKey_ValidKey_ResponseIsJson()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyWebAppFactory.ValidApiKey);

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    // ── New Api:Keys array format ──────────────────────────────────────────────

    [Fact]
    public async Task NewApiKeysFormat_ValidKey_Returns200()
    {
        const string newKey = "new-format-key-abc";
        using var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:Keys:0:Key"] = newKey,
                    ["Api:Keys:0:Role"] = "Admin"
                })))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        client.DefaultRequestHeaders.Add("X-Api-Key", newKey);

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NewApiKeysFormat_ValidKeyWithCustomRole_Returns200()
    {
        const string readOnlyKey = "readonly-key-xyz";
        using var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:Keys:0:Key"] = readOnlyKey,
                    ["Api:Keys:0:Role"] = "ReadOnly"
                })))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        client.DefaultRequestHeaders.Add("X-Api-Key", readOnlyKey);

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NewApiKeysFormat_KeyNotInList_Returns401()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:Keys:0:Key"] = "correct-key",
                    ["Api:Keys:0:Role"] = "Admin",
                    ["Api:Key"] = ""   // ensure legacy key is empty
                })))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
