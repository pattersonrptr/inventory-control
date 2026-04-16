using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Full CRUD lifecycle tests for Categories.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class CategoryCrudTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public CategoryCrudTests(PlaywrightFixture fixture) => _fixture = fixture;

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
    public async Task CategoryCrud_FullLifecycle()
    {
        var name = $"Cat_Test_{Guid.NewGuid().ToString("N")[..8]}";
        var description = "Test category description";
        var updatedName = $"{name}_Updated";

        // Step 1: Navigate to categories index — should show empty or existing list
        await _page.GotoAsync($"{_fixture.BaseUrl}/Categories");
        Assert.Contains("Categorias", await _page.TitleAsync());

        // Step 2: Create a new category
        await _page.ClickAsync("a:has-text('Nova Categoria')");
        await _page.WaitForURLAsync("**/Categories/Create");
        await _page.FillAsync("#Name", name);
        await _page.FillAsync("#Description", description);
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Categories");

        // Verify the new category appears in the table
        var row = _page.Locator("table tbody tr", new() { HasText = name });
        await Assertions.Expect(row).ToBeVisibleAsync();
        var descriptionCell = row.Locator("td").Nth(2);
        Assert.Contains(description, await descriptionCell.TextContentAsync());

        // Step 3: Edit the category
        await row.Locator("a[title='Editar']").ClickAsync();
        await _page.WaitForURLAsync("**/Categories/Edit/**");
        await _page.FillAsync("#Name", updatedName);
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Categories");

        // Verify the updated name
        var updatedRow = _page.Locator("table tbody tr", new() { HasText = updatedName });
        await Assertions.Expect(updatedRow).ToBeVisibleAsync();

        // Step 4: Delete the category
        await updatedRow.Locator("a[title='Excluir']").ClickAsync();
        await _page.WaitForURLAsync("**/Categories/Delete/**");

        // Confirm deletion
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Categories");

        // Verify the category is gone
        var deletedRow = _page.Locator("table tbody tr", new() { HasText = updatedName });
        await Assertions.Expect(deletedRow).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task CategoryCreate_Validation_RequiresName()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Categories/Create");

        // Submit with empty name — use the form's submit button specifically
        await _page.ClickAsync(".card-body button[type='submit']");

        // Should stay on the create page with a validation error
        var validationError = _page.Locator(".field-validation-error, .text-danger span");
        await Assertions.Expect(validationError.First).ToBeVisibleAsync();
    }
}
