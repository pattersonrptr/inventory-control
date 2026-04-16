using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Tests the dashboard page: cards, navigation links, page loads correctly.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class DashboardTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public DashboardTests(PlaywrightFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = await _fixture.CreateContextAsync();
        _page = await _context.NewPageAsync();
        await PageHelpers.LoginAsync(_page, _fixture.BaseUrl);
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsStatCards()
    {
        await _page.GotoAsync(_fixture.BaseUrl);

        // Should display the three stat cards
        var totalProducts = _page.Locator(".card.bg-primary .display-6");
        await Assertions.Expect(totalProducts).ToBeVisibleAsync();

        var belowMinimum = _page.Locator(".card:has(.bi-exclamation-triangle) .display-6");
        await Assertions.Expect(belowMinimum).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dashboard_NavigationLinksWork()
    {
        await _page.GotoAsync(_fixture.BaseUrl);

        // Click "Ver todos" link on Total Products card
        await _page.ClickAsync("a[href='/Products']");
        await _page.WaitForURLAsync("**/Products");
        Assert.Contains("Produtos", await _page.TitleAsync());

        // Go back to dashboard
        await _page.GotoAsync(_fixture.BaseUrl);

        // Click "Ver lista" link on Below Minimum card
        await _page.ClickAsync(".card-footer a[href='/Reports/BelowMinimum']");
        await _page.WaitForURLAsync("**/Reports/BelowMinimum");
    }

    [Fact]
    public async Task Navbar_AllMenuLinksAccessible()
    {
        // Products
        await _page.ClickAsync("a.nav-link[href='/Products']");
        await _page.WaitForURLAsync("**/Products");
        var title = await _page.TitleAsync();
        Assert.Contains("Produtos", title);

        // Categories
        await _page.ClickAsync("a.nav-link[href='/Categories']");
        await _page.WaitForURLAsync("**/Categories");

        // Suppliers
        await _page.ClickAsync("a.nav-link[href='/Suppliers']");
        await _page.WaitForURLAsync("**/Suppliers");

        // Movements > History (dropdown — click the toggle first)
        await _page.ClickAsync(".nav-link.dropdown-toggle:has-text('Movimentações')");
        await _page.ClickAsync("a.dropdown-item[href='/StockMovements']");
        await _page.WaitForURLAsync("**/StockMovements");

        // Reports > Below Minimum (dropdown)
        await _page.ClickAsync(".nav-link.dropdown-toggle:has-text('Relatórios')");
        await _page.ClickAsync("a.dropdown-item[href='/Reports/BelowMinimum']");
        await _page.WaitForURLAsync("**/Reports/BelowMinimum");

        // Admin > Users (dropdown)
        await _page.ClickAsync(".nav-link.dropdown-toggle:has-text('Admin')");
        await _page.ClickAsync("a.dropdown-item[href='/Account/Users']");
        await _page.WaitForURLAsync("**/Account/Users");
    }
}
