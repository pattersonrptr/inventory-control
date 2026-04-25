using System.Net;
using System.Text.RegularExpressions;
using InventoryControl.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.Integration;

/// <summary>
/// Tests that verify HTML forms can be submitted successfully (POST).
/// These catch database/model binding errors that GET-only tests miss.
/// </summary>
public class FormSubmissionTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public FormSubmissionTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── Categories ──────────────────────────────────────────────────────

    [Fact]
    public async Task Categories_Create_Post_ValidData_RedirectsToIndex()
    {
        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Categories/Create");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Test Category " + Guid.NewGuid(),
            ["Description"] = "Integration test category"
        };

        var response = await PostFormAsync(client, "/Categories/Create", formData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Categories", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Categories_Create_Post_EmptyName_ReturnsFormWithErrors()
    {
        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Categories/Create");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "",
            ["Description"] = ""
        };

        var response = await PostFormAsync(client, "/Categories/Create", formData);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("obrigat", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── Suppliers ────────────────────────────────────────────────────────

    [Fact]
    public async Task Suppliers_Create_Post_ValidData_RedirectsToIndex()
    {
        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Suppliers/Create");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Test Supplier " + Guid.NewGuid(),
            ["Cnpj"] = "",
            ["Phone"] = "",
            ["Email"] = ""
        };

        var response = await PostFormAsync(client, "/Suppliers/Create", formData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Suppliers", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Suppliers_Create_Post_EmptyName_ReturnsFormWithErrors()
    {
        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Suppliers/Create");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = ""
        };

        var response = await PostFormAsync(client, "/Suppliers/Create", formData);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("obrigat", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── Products ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Products_Create_Post_ValidData_RedirectsToIndex()
    {
        // Seed a category first
        var categoryId = await SeedCategoryAsync();

        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Products/Create");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Test Product " + Guid.NewGuid(),
            ["Description"] = "Integration test product",
            ["CostPrice"] = "10.00",
            ["SellingPrice"] = "20.00",
            ["MinimumStock"] = "5",
            ["CategoryId"] = categoryId.ToString()
        };

        var response = await PostFormAsync(client, "/Products/Create", formData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Products", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Products_Create_Post_MissingFields_ReturnsFormWithErrors()
    {
        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Products/Create");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = ""
        };

        var response = await PostFormAsync(client, "/Products/Create", formData);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("obrigat", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── Stock Movements ──────────────────────────────────────────────────

    [Fact]
    public async Task StockMovements_Entry_Post_ValidData_RedirectsToIndex()
    {
        var productId = await SeedProductAsync();

        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/StockMovements/Entry");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ProductId"] = productId.ToString(),
            ["Quantity"] = "10",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["Notes"] = "Integration test entry"
        };

        var response = await PostFormAsync(client, "/StockMovements/Entry", formData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/StockMovements", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StockMovements_Exit_Post_ValidData_RedirectsToIndex()
    {
        // Seed product and add stock first
        var productId = await SeedProductWithStockAsync(50);

        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/StockMovements/Exit");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ProductId"] = productId.ToString(),
            ["Quantity"] = "5",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["ExitReason"] = "Sale",
            ["Notes"] = "Integration test exit"
        };

        var response = await PostFormAsync(client, "/StockMovements/Exit", formData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/StockMovements", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StockMovements_Exit_Post_InsufficientStock_ReturnsFormWithError()
    {
        var productId = await SeedProductAsync(); // 0 stock

        using var client = CreateCookieClient();
        var token = await GetAntiForgeryTokenAsync(client, "/StockMovements/Exit");

        var formData = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ProductId"] = productId.ToString(),
            ["Quantity"] = "999",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["ExitReason"] = "Sale"
        };

        var response = await PostFormAsync(client, "/StockMovements/Exit", formData);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("insuficiente", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private HttpClient CreateCookieClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    private async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, @"name=""__RequestVerificationToken"".*?value=""([^""]+)""");

        Assert.True(match.Success, $"Could not find antiforgery token in {url}");
        return match.Groups[1].Value;
    }

    private async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> formData)
    {
        var content = new FormUrlEncodedContent(formData);
        return await client.PostAsync(url, content);
    }

    private async Task<(int categoryId, int supplierId)> SeedCategoryAndSupplierAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new Category { Name = "Test Cat " + Guid.NewGuid() };
        var supplier = new Supplier { Name = "Test Sup " + Guid.NewGuid() };

        db.Categories.Add(category);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return (category.Id, supplier.Id);
    }

    private async Task<int> SeedCategoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new Category { Name = "Test Cat " + Guid.NewGuid() };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        return category.Id;
    }

    private async Task<int> SeedProductAsync()
    {
        var categoryId = await SeedCategoryAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = new Product
        {
            Name = "Test Product " + Guid.NewGuid(),
            CostPrice = 10m,
            SellingPrice = 20m,
            CategoryId = categoryId,
            CurrentStock = 0
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }

    private async Task<int> SeedProductWithStockAsync(int stock)
    {
        var categoryId = await SeedCategoryAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = new Product
        {
            Name = "Test Product " + Guid.NewGuid(),
            CostPrice = 10m,
            SellingPrice = 20m,
            CategoryId = categoryId,
            CurrentStock = stock
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }
}
