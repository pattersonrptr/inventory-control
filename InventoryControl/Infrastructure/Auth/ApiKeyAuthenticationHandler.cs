using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InventoryControl.Infrastructure.Auth;

public class ApiKeyEntry
{
    public string Key { get; set; } = string.Empty;
    public string Role { get; set; } = "ReadOnly";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration) : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var providedKey = apiKeyHeader.ToString();

        var entries = _configuration.GetSection("Api:Keys").Get<List<ApiKeyEntry>>() ?? [];
        if (entries.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail("API key not configured on server."));

        var match = entries.FirstOrDefault(e =>
            !string.IsNullOrWhiteSpace(e.Key) &&
            string.Equals(e.Key, providedKey, StringComparison.Ordinal));

        if (match is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        return Task.FromResult(AuthenticateResult.Success(BuildTicket("ApiUser", match.Role)));
    }

    private AuthenticationTicket BuildTicket(string name, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.AuthenticationMethod, "ApiKey")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, Scheme.Name);
    }
}
