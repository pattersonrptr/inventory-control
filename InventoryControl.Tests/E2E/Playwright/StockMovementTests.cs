using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Tests for stock entry, exit, stock level verification, and movement history.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class StockMovementTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public StockMovementTests(PlaywrightFixture fixture) => _fixture = fixture;

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
    public async Task StockEntry_IncreasesProductStock()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Create prerequisites
        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"EntryCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            $"EntrySup_{suffix}");
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            $"EntryProd_{suffix}", categoryId.ToString(),
            minimumStock: "5");

        // Get product ID
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = $"EntryProd_{suffix}" });
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // Record stock entry of 25 units
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 25, supplierId.ToString(), "10,00", "Test stock entry");

        // Verify the movement appears in history
        await _page.GotoAsync($"{_fixture.BaseUrl}/StockMovements");
        var movementRow = _page.Locator("table tbody tr", new() { HasText = $"EntryProd_{suffix}" });
        await Assertions.Expect(movementRow.First).ToBeVisibleAsync();
        var movementText = await movementRow.First.TextContentAsync();
        Assert.Contains("Entrada", movementText);
        Assert.Contains("25", movementText);

        // Verify product stock was updated to 25
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        row = _page.Locator("table tbody tr", new() { HasText = $"EntryProd_{suffix}" });
        var stockCell = row.Locator("td.text-center.fw-bold");
        var stockText = await stockCell.TextContentAsync();
        Assert.Contains("25", stockText!.Trim());
    }

    [Fact]
    public async Task StockExit_DecreasesProductStock()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"ExitCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            $"ExitSup_{suffix}");
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            $"ExitProd_{suffix}", categoryId.ToString(),
            minimumStock: "5");

        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = $"ExitProd_{suffix}" });
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // First add stock, then remove some
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 30, supplierId.ToString());

        // Record exit of 12 units (Sale reason)
        await PageHelpers.RecordStockExitAsync(_page, _fixture.BaseUrl,
            productId, 12, "1", "Test stock exit");

        // Verify stock is 30 - 12 = 18
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        row = _page.Locator("table tbody tr", new() { HasText = $"ExitProd_{suffix}" });
        var stockCell = row.Locator("td.text-center.fw-bold");
        var stockText = await stockCell.TextContentAsync();
        Assert.Contains("18", stockText!.Trim());
    }

    [Fact]
    public async Task StockEntry_And_Exit_BothAppearInHistory()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"HistCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            $"HistSup_{suffix}");
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            $"HistProd_{suffix}", categoryId.ToString());

        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = $"HistProd_{suffix}" });
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // Record entry then exit
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 50, supplierId.ToString());
        await PageHelpers.RecordStockExitAsync(_page, _fixture.BaseUrl,
            productId, 20, "1");

        // Verify both movements in history
        await _page.GotoAsync($"{_fixture.BaseUrl}/StockMovements");
        var movements = _page.Locator("table tbody tr", new() { HasText = $"HistProd_{suffix}" });
        var count = await movements.CountAsync();
        Assert.True(count >= 2, $"Expected at least 2 movements, found {count}");

        // Verify entry badge
        var entryBadge = _page.Locator("table tbody tr:has-text('HistProd_') .badge.bg-success");
        await Assertions.Expect(entryBadge.First).ToBeVisibleAsync();

        // Verify exit badge
        var exitBadge = _page.Locator("table tbody tr:has-text('HistProd_') .badge.bg-danger");
        await Assertions.Expect(exitBadge.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task StockExit_MoreThanAvailable_ShowsError()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"OverCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            $"OverSup_{suffix}");
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            $"OverProd_{suffix}", categoryId.ToString());

        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = $"OverProd_{suffix}" });
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // Add only 5 units
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 5);

        // Try to exit 10 units (more than available)
        await _page.GotoAsync($"{_fixture.BaseUrl}/StockMovements/Exit");
        await _page.SelectOptionAsync("#ProductId", productId);
        await _page.FillAsync("#Quantity", "10");
        var dateValue = await _page.InputValueAsync("#Date");
        if (string.IsNullOrEmpty(dateValue))
            await _page.FillAsync("#Date", DateTime.Today.ToString("yyyy-MM-dd"));
        await _page.ClickAsync("button.btn[type='submit']");

        // Should show an error about insufficient stock
        var content = await _page.ContentAsync();
        var hasError = content.Contains("insuficiente", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("estoque", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("alert-danger", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasError, "Expected an error message about insufficient stock");
    }

    [Fact]
    public async Task StockMovementHistory_ShowsAllFields()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var productName = $"FieldsProd_{suffix}";
        var supplierName = $"FieldsSup_{suffix}";
        var notes = $"NF-{suffix}";

        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"FieldsCat_{suffix}");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            supplierName);
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            productName, categoryId.ToString());

        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = productName });
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // Entry with all fields filled
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 15, supplierId.ToString(), "7,50", notes);

        await _page.GotoAsync($"{_fixture.BaseUrl}/StockMovements");
        var movementRow = _page.Locator("table tbody tr", new() { HasText = productName }).First;
        var rowText = await movementRow.TextContentAsync();

        // Verify all fields are shown
        Assert.Contains(productName, rowText);
        Assert.Contains("Entrada", rowText);
        Assert.Contains("15", rowText);
        Assert.Contains(supplierName, rowText);
        Assert.Contains(notes, rowText);
    }
}
