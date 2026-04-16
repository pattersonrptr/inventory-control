using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// End-to-end workflow test that simulates a complete user session:
/// login → create prerequisites → manage inventory → check reports → manage users.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class FullWorkflowTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public FullWorkflowTests(PlaywrightFixture fixture) => _fixture = fixture;

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
    public async Task CompleteUserSession_FromLoginToReports()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryName = $"Electronics_{suffix}";
        var supplierName = $"TechCorp_{suffix}";
        var product1Name = $"Laptop_{suffix}";
        var product2Name = $"Mouse_{suffix}";

        // === STEP 1: Login ===
        await PageHelpers.LoginAsync(_page, _fixture.BaseUrl);
        var dashboardTitle = await _page.TextContentAsync("h1");
        Assert.Contains("Painel", dashboardTitle);

        // === STEP 2: Create Category ===
        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            categoryName, "Electronic devices and accessories");

        // === STEP 3: Create Supplier ===
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            supplierName, "12.345.678/0001-90", "(11) 99999-0000", "tech@corp.com");

        // === STEP 4: Create Products ===
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            product1Name, categoryId.ToString(), supplierId.ToString(),
            costPrice: "3000,00", sellingPrice: "4500,00",
            minimumStock: "3", sku: $"LAP-{suffix}");

        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            product2Name, categoryId.ToString(), supplierId.ToString(),
            costPrice: "50,00", sellingPrice: "120,00",
            minimumStock: "10", sku: $"MOU-{suffix}");

        // Both products start at stock 0 = below minimum
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/BelowMinimum");
        var belowMinRow1 = _page.Locator("table tbody tr", new() { HasText = product1Name });
        await Assertions.Expect(belowMinRow1).ToBeVisibleAsync();
        var belowMinRow2 = _page.Locator("table tbody tr", new() { HasText = product2Name });
        await Assertions.Expect(belowMinRow2).ToBeVisibleAsync();

        // === STEP 5: Get product IDs ===
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row1 = _page.Locator("table tbody tr", new() { HasText = product1Name });
        var editLink1 = await row1.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId1 = editLink1!.Split('/').Last();

        var row2 = _page.Locator("table tbody tr", new() { HasText = product2Name });
        var editLink2 = await row2.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId2 = editLink2!.Split('/').Last();

        // === STEP 6: Record Stock Entries ===
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId1, 10, supplierId.ToString(), "3000,00", "Purchase order #001");
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId2, 50, supplierId.ToString(), "50,00", "Purchase order #002");

        // === STEP 7: Verify products are no longer below minimum ===
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        row1 = _page.Locator("table tbody tr", new() { HasText = product1Name });
        // Stock 10, minimum 3 — should NOT have warning class
        var row1Class = await row1.GetAttributeAsync("class") ?? "";
        Assert.DoesNotContain("table-warning", row1Class);

        row2 = _page.Locator("table tbody tr", new() { HasText = product2Name });
        // Stock 50, minimum 10 — should NOT have warning class
        var row2Class = await row2.GetAttributeAsync("class") ?? "";
        Assert.DoesNotContain("table-warning", row2Class);

        // === STEP 8: Record Stock Exits (Sales) ===
        await PageHelpers.RecordStockExitAsync(_page, _fixture.BaseUrl,
            productId1, 3, "1", "Order #A100");
        await PageHelpers.RecordStockExitAsync(_page, _fixture.BaseUrl,
            productId2, 45, "1", "Order #A101");

        // === STEP 9: Check stock levels after exits ===
        // Product 1: 10 - 3 = 7 (above minimum 3)
        // Product 2: 50 - 45 = 5 (below minimum 10!)
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        row2 = _page.Locator("table tbody tr", new() { HasText = product2Name });
        row2Class = await row2.GetAttributeAsync("class") ?? "";
        Assert.Contains("table-warning", row2Class);

        // === STEP 10: Verify Below Minimum report ===
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/BelowMinimum");
        var lowStockRow = _page.Locator("table tbody tr", new() { HasText = product2Name });
        await Assertions.Expect(lowStockRow).ToBeVisibleAsync();
        // Product 1 should NOT be in the report anymore
        var product1InReport = _page.Locator("table tbody tr", new() { HasText = product1Name });
        await Assertions.Expect(product1InReport).ToHaveCountAsync(0);

        // === STEP 11: Verify Monthly Report ===
        var now = DateTime.Now;
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/Monthly?month={now.Month}&year={now.Year}");

        // Both products should appear with their entry/exit data
        var monthlyRow1 = _page.Locator("table tbody tr", new() { HasText = product1Name });
        await Assertions.Expect(monthlyRow1).ToBeVisibleAsync();
        var monthlyRow1Text = await monthlyRow1.TextContentAsync();
        Assert.Contains("+10", monthlyRow1Text); // entries
        Assert.Contains("-3", monthlyRow1Text);  // exits

        var monthlyRow2 = _page.Locator("table tbody tr", new() { HasText = product2Name });
        await Assertions.Expect(monthlyRow2).ToBeVisibleAsync();
        var monthlyRow2Text = await monthlyRow2.TextContentAsync();
        Assert.Contains("+50", monthlyRow2Text); // entries
        Assert.Contains("-45", monthlyRow2Text); // exits

        // Verify totals in the footer
        var footer = _page.Locator("tfoot");
        await Assertions.Expect(footer).ToBeVisibleAsync();

        // === STEP 12: Verify Profitability Report ===
        await _page.GotoAsync($"{_fixture.BaseUrl}/Reports/Profitability?month={now.Month}&year={now.Year}");

        var profitRow1 = _page.Locator("table tbody tr", new() { HasText = product1Name });
        await Assertions.Expect(profitRow1).ToBeVisibleAsync();
        var profitRow1Text = await profitRow1.TextContentAsync();
        Assert.Contains("3", profitRow1Text); // quantity sold

        // === STEP 13: Verify Movement History ===
        await _page.GotoAsync($"{_fixture.BaseUrl}/StockMovements");
        var allMovements = _page.Locator("table tbody tr");
        var movementCount = await allMovements.CountAsync();
        Assert.True(movementCount >= 4, $"Expected at least 4 movements, found {movementCount}");

        // === STEP 14: Verify Dashboard shows updated stats ===
        await _page.GotoAsync(_fixture.BaseUrl);
        var totalProductsCard = _page.Locator(".card.bg-primary .display-6");
        var totalText = await totalProductsCard.TextContentAsync();
        var totalProducts = int.Parse(totalText!.Trim());
        Assert.True(totalProducts >= 2, $"Expected at least 2 products, found {totalProducts}");

        // === STEP 15: Check audit logs page loads ===
        await _page.GotoAsync($"{_fixture.BaseUrl}/AuditLogs");
        var auditTable = _page.Locator("table");
        await Assertions.Expect(auditTable).ToBeVisibleAsync();
    }
}
