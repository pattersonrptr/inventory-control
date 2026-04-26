namespace InventoryControl.Infrastructure.Integrations.Abstractions;

public interface IPlatformFactory
{
    string PlatformName { get; }
    IStoreIntegration CreateIntegration(IntegrationConfig config, HttpClient httpClient);
}
