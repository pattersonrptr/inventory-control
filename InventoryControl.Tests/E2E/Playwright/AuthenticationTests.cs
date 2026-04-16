using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Tests for authentication: login, logout, access control.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class AuthenticationTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public AuthenticationTests(PlaywrightFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = await _fixture.CreateContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task UnauthenticatedUser_RedirectsToLogin()
    {
        await _page.GotoAsync(_fixture.BaseUrl);
        await _page.WaitForURLAsync("**/Account/Login**");

        var title = await _page.TitleAsync();
        Assert.Contains("Entrar", title);
    }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToDashboard()
    {
        await PageHelpers.LoginAsync(_page, _fixture.BaseUrl);

        var heading = await _page.TextContentAsync("h1");
        Assert.Contains("Painel", heading);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await _page.FillAsync("#Email", "wrong@email.com");
        await _page.FillAsync("#Password", "wrongpassword");
        await _page.ClickAsync("button[type='submit']");

        // Should stay on the login page with an error
        await _page.WaitForSelectorAsync(".text-danger");
        var url = _page.Url;
        Assert.Contains("/Account/Login", url);
    }

    [Fact]
    public async Task Logout_RedirectsToLogin()
    {
        await PageHelpers.LoginAsync(_page, _fixture.BaseUrl);

        // Click the user dropdown and then logout
        await _page.ClickAsync(".navbar a.dropdown-toggle:has(.bi-person-circle)");
        await _page.ClickAsync("form[action='/Account/Logout'] button");

        await _page.WaitForURLAsync("**/Account/Login**");
        var title = await _page.TitleAsync();
        Assert.Contains("Entrar", title);
    }
}
