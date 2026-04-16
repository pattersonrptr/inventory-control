using Microsoft.Playwright;

namespace InventoryControl.Tests.E2E.Playwright;

/// <summary>
/// Reusable page interaction helpers for common UI operations.
/// </summary>
public static class PageHelpers
{
    public static async Task LoginAsync(IPage page, string baseUrl,
        string email = PlaywrightFixture.AdminEmail,
        string password = PlaywrightFixture.AdminPassword)
    {
        await page.GotoAsync($"{baseUrl}/Account/Login");
        await page.FillAsync("#Email", email);
        await page.FillAsync("#Password", password);
        await page.ClickAsync("button[type='submit']");
        // Wait for redirect to dashboard
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new() { Timeout = 10000 });
    }

    public static async Task<int> CreateCategoryAsync(IPage page, string baseUrl,
        string name, string? description = null)
    {
        await page.GotoAsync($"{baseUrl}/Categories/Create");
        await page.FillAsync("#Name", name);
        if (description != null)
            await page.FillAsync("#Description", description);
        await page.ClickAsync("button.btn[type='submit']");
        await page.WaitForURLAsync($"**/Categories", new() { Timeout = 10000 });

        // Return the ID of the created category from the table
        var row = page.Locator("table tbody tr", new() { HasText = name }).First;
        var idText = await row.Locator("td").First.TextContentAsync();
        return int.Parse(idText!.Trim());
    }

    public static async Task<int> CreateSupplierAsync(IPage page, string baseUrl,
        string name, string? cnpj = null, string? phone = null, string? email = null)
    {
        await page.GotoAsync($"{baseUrl}/Suppliers/Create");
        await page.FillAsync("#Name", name);
        if (cnpj != null) await page.FillAsync("#Cnpj", cnpj);
        if (phone != null) await page.FillAsync("#Phone", phone);
        if (email != null) await page.FillAsync("#Email", email);
        await page.ClickAsync("button.btn[type='submit']");
        await page.WaitForURLAsync($"**/Suppliers", new() { Timeout = 10000 });

        var row = page.Locator("table tbody tr", new() { HasText = name }).First;
        var editHref = await row.Locator("a[title='Editar']").GetAttributeAsync("href");
        return int.Parse(editHref!.Split('/').Last());
    }

    public static async Task CreateProductAsync(IPage page, string baseUrl,
        string name, string categoryId, string supplierId,
        string costPrice = "10,00", string sellingPrice = "20,00",
        string minimumStock = "5", string? sku = null, string? description = null)
    {
        await page.GotoAsync($"{baseUrl}/Products/Create");
        await page.FillAsync("#Name", name);
        if (description != null) await page.FillAsync("#Description", description);

        // Use EvaluateAsync to set numeric values directly to avoid locale issues
        await page.EvaluateAsync($"document.querySelector('#CostPrice').value = '{costPrice.Replace(",", ".")}'");
        await page.EvaluateAsync($"document.querySelector('#SellingPrice').value = '{sellingPrice.Replace(",", ".")}'");
        await page.FillAsync("#MinimumStock", minimumStock);
        if (sku != null) await page.FillAsync("#Sku", sku);

        await page.SelectOptionAsync("#CategoryId", categoryId);
        await page.SelectOptionAsync("#SupplierId", supplierId);

        await page.ClickAsync("button.btn[type='submit']");
        await page.WaitForURLAsync($"**/Products", new() { Timeout = 10000 });
    }

    public static async Task RecordStockEntryAsync(IPage page, string baseUrl,
        string productId, int quantity, string? supplierId = null,
        string? unitCost = null, string? notes = null)
    {
        await page.GotoAsync($"{baseUrl}/StockMovements/Entry");
        await page.SelectOptionAsync("#ProductId", productId);
        await page.FillAsync("#Quantity", quantity.ToString());

        // Date defaults to today via the form, just verify it's set
        var dateValue = await page.InputValueAsync("#Date");
        if (string.IsNullOrEmpty(dateValue))
            await page.FillAsync("#Date", DateTime.Today.ToString("yyyy-MM-dd"));

        if (supplierId != null)
            await page.SelectOptionAsync("#SupplierId", supplierId);
        if (unitCost != null)
            await page.EvaluateAsync($"document.querySelector('#UnitCost').value = '{unitCost.Replace(",", ".")}'");
        if (notes != null)
            await page.FillAsync("#Notes", notes);

        await page.ClickAsync("button.btn[type='submit']");
        await page.WaitForURLAsync($"**/StockMovements", new() { Timeout = 10000 });
    }

    public static async Task RecordStockExitAsync(IPage page, string baseUrl,
        string productId, int quantity, string? exitReason = null, string? notes = null)
    {
        await page.GotoAsync($"{baseUrl}/StockMovements/Exit");
        await page.SelectOptionAsync("#ProductId", productId);
        await page.FillAsync("#Quantity", quantity.ToString());

        var dateValue = await page.InputValueAsync("#Date");
        if (string.IsNullOrEmpty(dateValue))
            await page.FillAsync("#Date", DateTime.Today.ToString("yyyy-MM-dd"));

        if (exitReason != null)
            await page.SelectOptionAsync("#ExitReason", exitReason);
        if (notes != null)
            await page.FillAsync("#Notes", notes);

        await page.ClickAsync("button.btn[type='submit']");
        await page.WaitForURLAsync($"**/StockMovements", new() { Timeout = 10000 });
    }
}
