using System.Net;

namespace InventoryControl.Tests.Integration;

public class HealthCheckTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessEndpoint_WithHealthyDb_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LivenessEndpoint_IsAnonymous()
    {
        // /health/live must be reachable without credentials (liveness probes don't authenticate)
        using var unauthFactory = new UnauthenticatedWebAppFactory();
        using var unauthClient = unauthFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await unauthClient.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
