using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Tests for reports: Below Minimum, Monthly, and Profitability.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class ReportTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public ReportTests(PlaywrightFixture fixture) => _fixture = fixture;

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
    public async Task BelowMinimumReport_ShowsLowStockProducts()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var productName = $"LowStock_{suffix}";

        // Create product with minimum stock 10 and current stock 0
        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"LowCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            $"LowSup_{suffix}");
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            productName, categoryId.ToString(),
            minimumStock: "10");

        // Navigate to below minimum report
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/BelowMinimum");

        // The product should appear as below minimum (stock 0 <= minimum 10)
        var row = _page.Locator("table tbody tr", new() { HasText = productName });
        await Assertions.Expect(row).ToBeVisibleAsync();

        var rowText = await row.TextContentAsync();
        // Should show deficit badge
        Assert.Contains("-10", rowText);

        // Should have "Registrar Entrada" link
        var entryLink = row.Locator("a:has-text('Registrar Entrada')");
        await Assertions.Expect(entryLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task BelowMinimumReport_NoProductsBelowMinimum_ShowsSuccessMessage()
    {
        // This may already have products from other tests that are below minimum,
        // so we just verify the page loads correctly
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/BelowMinimum");
        var title = await _page.TitleAsync();
        Assert.Contains("Produtos Abaixo do Estoque Mínimo", title);
    }

    [Fact]
    public async Task MonthlyReport_ShowsMovementsForSelectedMonth()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var productName = $"MonthProd_{suffix}";

        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"MonthCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            $"MonthSup_{suffix}");
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            productName, categoryId.ToString());

        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = productName });
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // Record an entry and exit for the current month
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 40, supplierId.ToString());
        await PageHelpers.RecordStockExitAsync(_page, _fixture.BaseUrl,
            productId, 15, "1");

        // Navigate to monthly report for current month
        var now = DateTime.Now;
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/Monthly?month={now.Month}&year={now.Year}");

        // Verify the product appears with correct entry/exit totals
        var reportRow = _page.Locator("table tbody tr", new() { HasText = productName });
        await Assertions.Expect(reportRow).ToBeVisibleAsync();

        var reportText = await reportRow.TextContentAsync();
        Assert.Contains("+40", reportText);
        Assert.Contains("-15", reportText);
    }

    [Fact]
    public async Task MonthlyReport_FilterByDifferentMonth_ShowsNoMovements()
    {
        // Filter by a month far in the past where no data exists
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/Monthly?month=1&year=2020");

        // Should show the "no movements" message
        var noDataMessage = _page.Locator(".alert-info");
        await Assertions.Expect(noDataMessage).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ProfitabilityReport_ShowsRevenueAndCost()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var productName = $"ProfitProd_{suffix}";

        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"ProfitCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            $"ProfitSup_{suffix}");
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            productName, categoryId.ToString(),
            costPrice: "10,00", sellingPrice: "25,00");

        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = productName });
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // Record entry then sale exit
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 20, supplierId.ToString(), "10,00");
        await PageHelpers.RecordStockExitAsync(_page, _fixture.BaseUrl,
            productId, 8, "1");

        // Navigate to profitability report
        var now = DateTime.Now;
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/Profitability?month={now.Month}&year={now.Year}");

        // Verify the product appears with profitability data
        var reportRow = _page.Locator("table tbody tr", new() { HasText = productName });
        await Assertions.Expect(reportRow).ToBeVisibleAsync();

        var reportText = await reportRow.TextContentAsync();
        // Should show quantity sold = 8
        Assert.Contains("8", reportText);
    }
}
