using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Full CRUD lifecycle tests for Products.
/// Products depend on categories and suppliers, so we create prerequisites first.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class ProductCrudTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public ProductCrudTests(PlaywrightFixture fixture) => _fixture = fixture;

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
    public async Task ProductCrud_FullLifecycle()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryName = $"ProdCat_{suffix}";
        var supplierName = $"ProdSup_{suffix}";
        var productName = $"Product_{suffix}";
        var updatedProductName = $"{productName}_Updated";

        // Step 1: Create prerequisite category and supplier
        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            categoryName, "Category for product test");
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            supplierName);

        // Step 2: Create a product
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl,
            productName,
            categoryId.ToString(),
            costPrice: "15,50",
            sellingPrice: "29,90",
            minimumStock: "10",
            sku: $"SKU-{suffix}",
            description: "Test product description");

        // Verify the product appears in the table
        var row = _page.Locator("table tbody tr", new() { HasText = productName });
        await Assertions.Expect(row).ToBeVisibleAsync();

        var rowText = await row.TextContentAsync();
        Assert.Contains(categoryName, rowText);

        // Stock is 0, minimum is 10 — should be flagged as below minimum (warning row)
        await Assertions.Expect(row).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("table-warning"));

        // Step 3: View product details
        await row.Locator("a[title='Detalhes']").ClickAsync();
        await _page.WaitForURLAsync("**/Products/Details/**");
        var detailsContent = await _page.ContentAsync();
        Assert.Contains(productName, detailsContent);
        Assert.Contains($"SKU-{suffix}", detailsContent);

        // Step 4: Edit the product
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        row = _page.Locator("table tbody tr", new() { HasText = productName });
        await row.Locator("a[title='Editar']").ClickAsync();
        await _page.WaitForURLAsync("**/Products/Edit/**");
        await _page.FillAsync("#Name", updatedProductName);
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Products");

        var updatedRow = _page.Locator("table tbody tr", new() { HasText = updatedProductName });
        await Assertions.Expect(updatedRow).ToBeVisibleAsync();

        // Step 5: Delete the product
        await updatedRow.Locator("a[title='Excluir']").ClickAsync();
        await _page.WaitForURLAsync("**/Products/Delete/**");
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Products");

        var deletedRow = _page.Locator("table tbody tr", new() { HasText = updatedProductName });
        await Assertions.Expect(deletedRow).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ProductCreate_Validation_RequiresFields()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products/Create");
        // Submit without filling anything
        await _page.ClickAsync("button.btn[type='submit']");

        // Should show validation errors
        var validationErrors = _page.Locator(".text-danger, .field-validation-error");
        var count = await validationErrors.CountAsync();
        Assert.True(count > 0, "Expected validation errors for required fields");
    }

    [Fact]
    public async Task ProductDelete_FailsWhenStockMovementsExist()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryName = $"DelCat_{suffix}";
        var supplierName = $"DelSup_{suffix}";
        var productName = $"DelProd_{suffix}";

        // Create category, supplier, product
        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl, categoryName);
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl, supplierName);
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl, productName,
            categoryId.ToString());

        // Get product ID from the table
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var row = _page.Locator("table tbody tr", new() { HasText = productName });
        // The product ID is embedded in the action URLs
        var editLink = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        var productId = editLink!.Split('/').Last();

        // Record a stock entry so the product has movements
        await PageHelpers.RecordStockEntryAsync(_page, _fixture.BaseUrl,
            productId, 10, supplierId.ToString(), "5,00", "Test entry");

        // Now try to delete the product — should fail
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products/Delete/{productId}");
        await _page.ClickAsync("button.btn[type='submit']");

        // Should show an error (TempData message or stay on page)
        var content = await _page.ContentAsync();
        // The product should still exist — deletion fails because of movements
        await _page.GotoAsync($"{_fixture.BaseUrl}/Products");
        var stillExists = _page.Locator("table tbody tr", new() { HasText = productName });
        await Assertions.Expect(stillExists).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CategoryDelete_FailsWhenProductsExist()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryName = $"CatDel_{suffix}";
        var supplierName = $"SupDel_{suffix}";
        var productName = $"ProdDel_{suffix}";

        // Create category and supplier, then a product linked to the category
        var categoryId = await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl, categoryName);
        var supplierId = await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl, supplierName);
        await PageHelpers.CreateProductAsync(_page, _fixture.BaseUrl, productName,
            categoryId.ToString());

        // Try to delete the category — should fail because products are linked
        await _page.GotoAsync($"{_fixture.BaseUrl}/Categories/Delete/{categoryId}");
        await _page.ClickAsync("button.btn[type='submit']");

        // Category should still exist
        await _page.GotoAsync($"{_fixture.BaseUrl}/Categories");
        var stillExists = _page.Locator("table tbody tr", new() { HasText = categoryName });
        await Assertions.Expect(stillExists).ToBeVisibleAsync();
    }
}
