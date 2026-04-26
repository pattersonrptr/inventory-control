using InventoryControl.Infrastructure.Integrations.Abstractions;
using InventoryControl.Infrastructure.Integrations.Nuvemshop;

namespace InventoryControl.Infrastructure.Integrations;

public class NuvemshopPlatformFactory : IPlatformFactory
{
    public string PlatformName => "nuvemshop";

    public IStoreIntegration CreateIntegration(IntegrationConfig config, HttpClient httpClient)
    {
        var client = new NuvemshopClient(httpClient, config);
        return new NuvemshopIntegration(client);
    }
}
