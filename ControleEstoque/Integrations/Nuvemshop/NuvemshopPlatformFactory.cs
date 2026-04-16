using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Integrations.Nuvemshop;

namespace ControleEstoque.Integrations;

public class NuvemshopPlatformFactory : IPlatformFactory
{
    public string PlatformName => "nuvemshop";

    public IStoreIntegration CreateIntegration(IntegrationConfig config, HttpClient httpClient)
    {
        var client = new NuvemshopClient(httpClient, config);
        return new NuvemshopIntegration(client);
    }
}
