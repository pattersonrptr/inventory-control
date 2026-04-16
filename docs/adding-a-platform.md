# How to Add a New E-Commerce Platform

This guide explains how to integrate a new e-commerce platform (e.g., Shopify, WooCommerce, Mercado Livre) into Inventory Control.

The system uses a **platform registry pattern**: each platform provides a factory that creates `IStoreIntegration` instances. The application supports N stores simultaneously, each with its own configuration.

---

## Architecture Overview

```
PlatformRegistry
  ├── NuvemshopPlatformFactory  → creates NuvemshopIntegration
  ├── ShopifyPlatformFactory    → creates ShopifyIntegration   (your new platform)
  └── ...
```

Key interfaces:

| Interface | Purpose |
|---|---|
| `IStoreIntegration` | The contract every platform must implement (products, orders, stock, categories) |
| `IPlatformFactory` | Creates `IStoreIntegration` instances for a given platform |
| `PlatformRegistry` | Discovers factories, resolves stores by name, creates integrations |

---

## Step-by-Step

### 1. Create the Platform Folder

```
ControleEstoque/Integrations/YourPlatform/
├── YourPlatformClient.cs           # HTTP client wrapper
├── YourPlatformIntegration.cs      # IStoreIntegration adapter
├── YourPlatformPlatformFactory.cs  # IPlatformFactory implementation
└── Models/
    ├── YourPlatformProduct.cs      # Platform-specific API models
    ├── YourPlatformOrder.cs
    └── ...
```

### 2. Implement `IStoreIntegration`

Create `YourPlatformIntegration.cs` implementing all methods:

```csharp
using ControleEstoque.Integrations.Abstractions;

namespace ControleEstoque.Integrations.YourPlatform;

public class YourPlatformIntegration : IStoreIntegration
{
    private readonly YourPlatformClient _client;

    public YourPlatformIntegration(YourPlatformClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ExternalProduct>> GetProductsAsync()
    {
        // Call your platform's API and map to ExternalProduct
        var products = await _client.GetProductsAsync();
        return products.Select(p => new ExternalProduct
        {
            ExternalId = p.Id.ToString(),
            Name = p.Title,
            Sku = p.Sku,
            Price = p.Price,
            Stock = p.InventoryQuantity,
            // ...
        });
    }

    public async Task UpdateStockAsync(string externalProductId, int quantity)
    {
        // Push stock update to the platform
    }

    public async Task<IEnumerable<ExternalOrder>> GetOrdersAsync(DateTime since)
    {
        // Fetch orders since the given date and map to ExternalOrder
    }

    public async Task<ExternalOrder?> GetOrderAsync(string externalOrderId)
    {
        // Fetch a single order by ID
    }

    public async Task<ExternalProduct?> CreateProductAsync(
        string name, string? description, decimal price, string? sku, int stock)
    {
        // Create a product on the platform
    }

    public async Task<IEnumerable<ExternalCategory>> GetCategoriesAsync()
    {
        // Fetch categories from the platform
    }

    public async Task<ExternalCategory?> CreateCategoryAsync(string name)
    {
        // Create a category on the platform
    }
}
```

### 3. Create the HTTP Client

```csharp
using ControleEstoque.Integrations.Abstractions;

namespace ControleEstoque.Integrations.YourPlatform;

public class YourPlatformClient
{
    private readonly HttpClient _http;

    public YourPlatformClient(HttpClient http, IntegrationConfig config)
    {
        _http = http;
        // Configure base URL and authentication headers from config
        _http.BaseAddress = new Uri($"https://api.yourplatform.com/v1/");
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.AccessToken}");
    }

    // Add methods to call the platform's REST API...
}
```

### 4. Implement `IPlatformFactory`

```csharp
using ControleEstoque.Integrations.Abstractions;

namespace ControleEstoque.Integrations.YourPlatform;

public class YourPlatformPlatformFactory : IPlatformFactory
{
    public string PlatformName => "yourplatform";  // must match config "Platform" value

    public IStoreIntegration CreateIntegration(IntegrationConfig config, HttpClient httpClient)
    {
        var client = new YourPlatformClient(httpClient, config);
        return new YourPlatformIntegration(client);
    }
}
```

### 5. Register in `Program.cs`

Add two lines to `Program.cs`:

```csharp
// Register the platform factory
builder.Services.AddSingleton<IPlatformFactory, YourPlatformPlatformFactory>();

// Register a named HttpClient with resilience handlers
builder.Services.AddHttpClient("Platform_yourplatform")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        // ... configure as needed
    });
```

The `PlatformRegistry` automatically discovers all registered `IPlatformFactory` implementations via dependency injection.

### 6. Add Configuration

Add a new entry to the `Stores` array in `appsettings.json`:

```json
{
  "Stores": [
    {
      "Name": "My YourPlatform Store",
      "Enabled": true,
      "Platform": "yourplatform",
      "StoreId": "<YOUR_STORE_ID>",
      "AccessToken": "<YOUR_ACCESS_TOKEN>",
      "StoreUrl": "https://mystore.yourplatform.com",
      "OrderSyncIntervalMinutes": 15
    }
  ]
}
```

The `Platform` value must exactly match the `PlatformName` property of your factory (case-insensitive).

### 7. (Optional) Add a Webhook Controller

If the platform supports webhooks for real-time order notifications:

```csharp
[AllowAnonymous]
[ApiController]
[Route("api/webhooks/yourplatform")]
public class YourPlatformWebhookController : ControllerBase
{
    private readonly PlatformRegistry _registry;
    private readonly SyncServiceFactory _syncFactory;

    // Inject PlatformRegistry and SyncServiceFactory
    // Use _registry.GetStoreByPlatformStoreId() to match the incoming webhook
    // Use _syncFactory.Create(storeConfig) to get a SyncService for that store
}
```

---

## Testing

1. Add unit tests for your integration adapter in `ControleEstoque.Tests/Unit/Services/`
2. Test the mapping from platform-specific models to `External*` abstractions
3. Verify the factory creates a working integration with a mock HttpClient

## Checklist

- [ ] `IStoreIntegration` fully implemented
- [ ] `IPlatformFactory` registered in `Program.cs`
- [ ] Named `HttpClient` with resilience registered in `Program.cs`
- [ ] Platform-specific models in `Models/` subfolder
- [ ] Configuration documented in `appsettings.example.json`
- [ ] Webhook controller (if applicable)
- [ ] Unit tests for the adapter
- [ ] `CHANGELOG.md` updated
- [ ] `README.md` updated
