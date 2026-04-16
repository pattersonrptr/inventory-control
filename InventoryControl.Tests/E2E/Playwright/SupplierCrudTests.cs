using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Full CRUD lifecycle tests for Suppliers.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class SupplierCrudTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public SupplierCrudTests(PlaywrightFixture fixture) => _fixture = fixture;

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
    public async Task SupplierCrud_FullLifecycle()
    {
        var name = $"Supplier_{Guid.NewGuid().ToString("N")[..8]}";
        var updatedName = $"{name}_Updated";

        // Step 1: Create a new supplier
        await _page.GotoAsync($"{_fixture.BaseUrl}/Suppliers/Create");
        await _page.FillAsync("#Name", name);
        await _page.FillAsync("#Cnpj", "12.345.678/0001-90");
        await _page.FillAsync("#Phone", "(11) 99999-0000");
        await _page.FillAsync("#Email", "supplier@test.com");
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Suppliers");

        // Verify the supplier appears in the table
        var row = _page.Locator("table tbody tr", new() { HasText = name });
        await Assertions.Expect(row).ToBeVisibleAsync();

        // Step 2: Edit the supplier
        await row.Locator("a[title='Editar']").ClickAsync();
        await _page.WaitForURLAsync("**/Suppliers/Edit/**");
        await _page.FillAsync("#Name", updatedName);
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Suppliers");

        var updatedRow = _page.Locator("table tbody tr", new() { HasText = updatedName });
        await Assertions.Expect(updatedRow).ToBeVisibleAsync();

        // Step 3: Delete the supplier
        await updatedRow.Locator("a[title='Excluir']").ClickAsync();
        await _page.WaitForURLAsync("**/Suppliers/Delete/**");
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Suppliers");

        var deletedRow = _page.Locator("table tbody tr", new() { HasText = updatedName });
        await Assertions.Expect(deletedRow).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SupplierCreate_Validation_RequiresName()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Suppliers/Create");
        await _page.ClickAsync(".card-body button[type='submit']");

        var validationError = _page.Locator(".field-validation-error, .text-danger span");
        await Assertions.Expect(validationError.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SupplierCreate_AllFieldsPopulatedCorrectly()
    {
        var name = $"FullSupplier_{Guid.NewGuid().ToString("N")[..8]}";
        var cnpj = "98.765.432/0001-10";
        var phone = "(21) 88888-1234";
        var email = "full-supplier@test.com";

        await PageHelpers.CreateSupplierAsync(_page, _fixture.BaseUrl,
            name, cnpj, phone, email);

        // Verify all fields in the table row
        var row = _page.Locator("table tbody tr", new() { HasText = name });
        await Assertions.Expect(row).ToBeVisibleAsync();

        var rowText = await row.TextContentAsync();
        Assert.Contains(cnpj, rowText);
        Assert.Contains(phone, rowText);
        Assert.Contains(email, rowText);
    }
}
