using InventoryControl.Integrations.Abstractions;
using InventoryControl.Integrations.Nuvemshop;

namespace InventoryControl.Integrations;

public class NuvemshopPlatformFactory : IPlatformFactory
{
    public string PlatformName => "nuvemshop";

    public IStoreIntegration CreateIntegration(IntegrationConfig config, HttpClient httpClient)
    {
        var client = new NuvemshopClient(httpClient, config);
        return new NuvemshopIntegration(client);
    }
}
