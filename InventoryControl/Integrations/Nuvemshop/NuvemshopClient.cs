using System.Net.Http.Headers;
using System.Text.Json;
using InventoryControl.Integrations.Abstractions;
using InventoryControl.Integrations.Nuvemshop.Models;

namespace InventoryControl.Integrations.Nuvemshop;

/// <summary>
/// Typed HttpClient wrapper for the Nuvemshop REST API.
/// Base URL: https://api.tiendanube.com/v1/{store_id}/
/// </summary>
public class NuvemshopClient
{
    private readonly HttpClient _http;
    private readonly IntegrationConfig _config;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NuvemshopClient(HttpClient http, IntegrationConfig config)
    {
        _http = http;
        _config = config;

        _http.BaseAddress = new Uri($"https://api.tiendanube.com/v1/{config.StoreId}/");
        _http.DefaultRequestHeaders.Add("Authentication", $"bearer {config.AccessToken}");
        _http.DefaultRequestHeaders.Add("User-Agent", "InventoryControl/1.0 (inventory-control; contact via GitHub)");
    }

    public async Task<List<NuvemshopProduct>> GetProductsAsync()
    {
        var response = await _http.GetAsync("products");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<NuvemshopProduct>>(json, JsonOptions) ?? new();
    }

    public async Task<NuvemshopOrder?> GetOrderAsync(long orderId)
    {
        var response = await _http.GetAsync($"orders/{orderId}");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NuvemshopOrder>(json, JsonOptions);
    }

    public async Task<List<NuvemshopOrder>> GetOrdersAsync(DateTime since)
    {
        var response = await _http.GetAsync($"orders?created_at_min={since:yyyy-MM-ddTHH:mm:ssZ}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new List<NuvemshopOrder>();
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<NuvemshopOrder>>(json, JsonOptions) ?? new();
    }

    public async Task<List<NuvemshopVariant>> GetVariantsAsync(long productId)
    {
        var response = await _http.GetAsync($"products/{productId}/variants");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<NuvemshopVariant>>(json, JsonOptions) ?? new();
    }

    public async Task<NuvemshopProduct?> CreateProductAsync(string name, string? description, decimal price, string? sku, int stock)
    {
        var body = new
        {
            name = new Dictionary<string, string> { ["pt"] = name },
            description = new Dictionary<string, string> { ["pt"] = description ?? string.Empty },
            variants = new[]
            {
                new
                {
                    price = price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    sku,
                    stock
                }
            }
        };
        var payload = JsonSerializer.Serialize(body);
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("products", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NuvemshopProduct>(json, JsonOptions);
    }

    public async Task<List<NuvemshopCategory>> GetCategoriesAsync()
    {
        var response = await _http.GetAsync("categories");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<NuvemshopCategory>>(json, JsonOptions) ?? new();
    }

    public async Task<NuvemshopCategory?> CreateCategoryAsync(string name)
    {
        var body = new { name = new Dictionary<string, string> { ["pt"] = name } };
        var payload = JsonSerializer.Serialize(body);
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("categories", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NuvemshopCategory>(json, JsonOptions);
    }

    public async Task UpdateVariantStockAsync(long productId, long variantId, int quantity)
    {
        var payload = JsonSerializer.Serialize(new { stock = quantity });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"products/{productId}/variants/{variantId}", content);
        response.EnsureSuccessStatusCode();
    }
}