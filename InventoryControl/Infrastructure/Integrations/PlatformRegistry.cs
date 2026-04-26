using InventoryControl.Infrastructure.Integrations.Abstractions;

namespace InventoryControl.Infrastructure.Integrations;

public class PlatformRegistry
{
    private readonly Dictionary<string, IPlatformFactory> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IntegrationConfig> _stores;
    private readonly IHttpClientFactory _httpClientFactory;

    public PlatformRegistry(
        IEnumerable<IPlatformFactory> factories,
        List<IntegrationConfig> stores,
        IHttpClientFactory httpClientFactory)
    {
        foreach (var factory in factories)
            _factories[factory.PlatformName] = factory;

        _stores = stores;
        _httpClientFactory = httpClientFactory;
    }

    public IReadOnlyList<IntegrationConfig> GetAllStores() => _stores.AsReadOnly();

    public IReadOnlyList<IntegrationConfig> GetEnabledStores() =>
        _stores.Where(s => s.Enabled).ToList().AsReadOnly();

    public IntegrationConfig? GetStoreByName(string name) =>
        _stores.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public IntegrationConfig? GetStoreByPlatformStoreId(string storeId) =>
        _stores.FirstOrDefault(s => s.StoreId == storeId && s.Enabled);

    public IStoreIntegration CreateIntegration(IntegrationConfig config)
    {
        if (!_factories.TryGetValue(config.Platform, out var factory))
            throw new InvalidOperationException(
                $"No platform factory registered for '{config.Platform}'. " +
                $"Registered platforms: {string.Join(", ", _factories.Keys)}");

        var httpClient = _httpClientFactory.CreateClient($"Platform_{config.Platform}");
        return factory.CreateIntegration(config, httpClient);
    }

    public IEnumerable<string> GetRegisteredPlatforms() => _factories.Keys;
}
