using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Tests for admin-only features: user management and audit logs.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class AdminFeatureTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public AdminFeatureTests(PlaywrightFixture fixture) => _fixture = fixture;

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
    public async Task UserManagement_CreateEditDeleteUser()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fullName = $"Test User {suffix}";
        var email = $"testuser_{suffix}@test.com";
        var password = "TestPass123";
        var updatedName = $"Updated User {suffix}";

        // Step 1: Navigate to users list
        await _page.GotoAsync($"{_fixture.BaseUrl}/Account/Users");
        Assert.Contains("Usuários", await _page.TitleAsync());

        // Admin user should already exist
        var adminRow = _page.Locator("table tbody tr", new() { HasText = "admin@inventory.local" });
        await Assertions.Expect(adminRow).ToBeVisibleAsync();

        // Step 2: Create a new user
        await _page.ClickAsync("a:has-text('Novo Usuário')");
        await _page.WaitForURLAsync("**/Account/Create");
        await _page.FillAsync("#FullName", fullName);
        await _page.FillAsync("#Email", email);
        await _page.FillAsync("#Password", password);
        await _page.FillAsync("#ConfirmPassword", password);
        await _page.SelectOptionAsync("#Role", "Operator");
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Account/Users");

        // Verify the new user appears
        var userRow = _page.Locator("table tbody tr", new() { HasText = fullName });
        await Assertions.Expect(userRow).ToBeVisibleAsync();
        var userRowText = await userRow.TextContentAsync();
        Assert.Contains(email, userRowText);
        Assert.Contains("Operador", userRowText);

        // Step 3: Edit the user
        await userRow.Locator("a.btn-outline-primary").ClickAsync();
        await _page.WaitForURLAsync("**/Account/Edit/**");
        await _page.FillAsync("#FullName", updatedName);
        await _page.SelectOptionAsync("#Role", "Admin");
        await _page.ClickAsync("button.btn[type='submit']");
        await _page.WaitForURLAsync("**/Account/Users");

        var updatedRow = _page.Locator("table tbody tr", new() { HasText = updatedName });
        await Assertions.Expect(updatedRow).ToBeVisibleAsync();

        // Step 4: Delete the user (accept JS confirm dialog)
        _page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await updatedRow.Locator("form button.btn-outline-danger").ClickAsync();

        // Wait for redirection back to user list
        await _page.WaitForURLAsync("**/Account/Users", new() { Timeout = 10000 });

        // Verify the user is gone
        var deletedRow = _page.Locator("table tbody tr", new() { HasText = updatedName });
        await Assertions.Expect(deletedRow).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AuditLogs_PageLoadAndShowsEntries()
    {
        // First, create something to generate audit log entries
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await PageHelpers.CreateCategoryAsync(_page, _fixture.BaseUrl,
            $"AuditCat_{suffix}", "Audit test category");

        // Navigate to audit logs
        await _page.GotoAsync($"{_fixture.BaseUrl}/AuditLogs");

        // Page should load without errors
        var title = await _page.TitleAsync();
        Assert.Contains("Auditoria", title);

        // Should show the audit log table
        var table = _page.Locator("table");
        await Assertions.Expect(table).ToBeVisibleAsync();
    }

    [Fact]
    public async Task StoresPage_LoadsSuccessfully()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Stores");

        // Should load without errors
        var content = await _page.ContentAsync();
        Assert.Contains("Lojas", content);
    }
}
