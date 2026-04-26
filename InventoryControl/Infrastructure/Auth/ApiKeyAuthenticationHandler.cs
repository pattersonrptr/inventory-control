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

        // New format: Api:Keys array with per-key roles.
        var entries = _configuration.GetSection("Api:Keys").Get<List<ApiKeyEntry>>() ?? [];
        if (entries.Count > 0)
        {
            var match = entries.FirstOrDefault(e =>
                !string.IsNullOrWhiteSpace(e.Key) &&
                string.Equals(e.Key, providedKey, StringComparison.Ordinal));

            if (match is not null)
                return Task.FromResult(AuthenticateResult.Success(BuildTicket("ApiUser", match.Role)));

            // Legacy key is ignored when the new format is present.
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        // Legacy format: single Api:Key — always grants Admin. Deprecated since v6.1.2.
        var legacyKey = _configuration["Api:Key"];
        if (string.IsNullOrWhiteSpace(legacyKey))
            return Task.FromResult(AuthenticateResult.Fail("API key not configured on server."));

        Logger.LogWarning(
            "'Api:Key' is deprecated. Migrate to the 'Api:Keys' array format (see appsettings.example.json).");

        if (!string.Equals(providedKey, legacyKey, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        return Task.FromResult(AuthenticateResult.Success(BuildTicket("ApiUser", "Admin")));
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
