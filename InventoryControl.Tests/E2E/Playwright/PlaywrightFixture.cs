using System.Net;
using InventoryControl.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Shared fixture that starts the application via TestServer + a Kestrel reverse proxy
/// on a real TCP port, and provides a Playwright browser instance for E2E tests.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    private PlaywrightWebAppFactory? _factory;
    private IHost? _proxyHost;
    private HttpClient? _innerClient;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = "";

    public const string AdminEmail = "admin@inventory.local";
    public const string AdminPassword = "Admin123!";

    public async Task InitializeAsync()
    {
        _factory = new PlaywrightWebAppFactory();

        // HttpClient that talks to the in-process TestServer (no TCP, instant)
        _innerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        // Start a lightweight Kestrel proxy on a real TCP port (port 0 = OS picks)
        var client = _innerClient;
        _proxyHost = new HostBuilder()
            .ConfigureWebHostDefaults(wb =>
            {
                wb.UseKestrel(opts => opts.Listen(IPAddress.Loopback, 0));
                wb.Configure(app =>
                {
                    app.Run(async ctx => await ForwardRequestAsync(ctx, client));
                });
            })
            .Build();
        await _proxyHost.StartAsync();

        // Read the actual bound address after Kestrel starts
        var server = _proxyHost.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        var addresses = server.Features
            .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
        BaseUrl = addresses!.Addresses.First();

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser != null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        if (_proxyHost != null) await _proxyHost.StopAsync();
        _innerClient?.Dispose();
        if (_factory != null) await _factory.DisposeAsync();
    }

    /// <summary>
    /// Creates an isolated browser context (like incognito) for each test class.
    /// </summary>
    public async Task<IBrowserContext> CreateContextAsync()
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "pt-BR",
            IgnoreHTTPSErrors = true
        });
    }

    /// <summary>
    /// Forwards an HTTP request from the Kestrel proxy to the in-process TestServer.
    /// </summary>
    private static async Task ForwardRequestAsync(HttpContext ctx, HttpClient innerClient)
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(ctx.Request.Method),
            RequestUri = new Uri(innerClient.BaseAddress!,
                ctx.Request.Path.Value + ctx.Request.QueryString.Value)
        };

        // Copy request headers (skip content headers — they go on Content)
        foreach (var (key, value) in ctx.Request.Headers)
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
            if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            requestMessage.Headers.TryAddWithoutValidation(key, value.ToArray());
        }

        // Copy request body for methods that carry a payload
        if (HttpMethods.IsPost(ctx.Request.Method)
            || HttpMethods.IsPut(ctx.Request.Method)
            || HttpMethods.IsPatch(ctx.Request.Method)
            || HttpMethods.IsDelete(ctx.Request.Method))
        {
            var body = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(body);
            body.Position = 0;
            requestMessage.Content = new StreamContent(body);
            if (ctx.Request.ContentType != null)
                requestMessage.Content.Headers.ContentType =
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ctx.Request.ContentType);
            if (ctx.Request.ContentLength.HasValue)
                requestMessage.Content.Headers.ContentLength = ctx.Request.ContentLength;
        }

        using var response = await innerClient.SendAsync(requestMessage,
            HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

        ctx.Response.StatusCode = (int)response.StatusCode;

        // Copy response headers
        var skipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "transfer-encoding" };
        foreach (var (key, values) in response.Headers)
        {
            if (!skipHeaders.Contains(key))
                ctx.Response.Headers[key] = values.ToArray();
        }
        foreach (var (key, values) in response.Content.Headers)
        {
            if (!skipHeaders.Contains(key))
                ctx.Response.Headers[key] = values.ToArray();
        }

        await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }
}

/// <summary>
/// WebApplicationFactory that replaces the database with a test SQLite instance
/// and removes background services. Uses the default TestServer (no Kestrel).
/// </summary>
internal class PlaywrightWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"e2e_test_{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove all EF Core / DbContext registrations
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                          || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in descriptors)
                services.Remove(d);

            // Use a file-based SQLite database for E2E tests
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_dbPath}");
                options.ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId
                        .PendingModelChangesWarning));
            });

            // Remove background services that interfere with tests
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                         && d.ImplementationType?.Name != "GenericWebHostService")
                .ToList();
            foreach (var d in hostedServices)
                services.Remove(d);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // SQLite file may still be locked; ignore cleanup failure
        }
    }
}
