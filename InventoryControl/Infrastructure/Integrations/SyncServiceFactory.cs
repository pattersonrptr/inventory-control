using InventoryControl.Infrastructure.Persistence;
using InventoryControl.Infrastructure.Integrations.Abstractions;

using Microsoft.Extensions.Logging;

namespace InventoryControl.Infrastructure.Integrations;

public class SyncServiceFactory
{
    private readonly PlatformRegistry _registry;
    private readonly IProductRepository _productRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProcessedOrderRepository _processedOrderRepo;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SyncService> _logger;
    private readonly IProductImageDownloader _imageDownloader;

    public SyncServiceFactory(
        PlatformRegistry registry,
        IProductRepository productRepo,
        IStockMovementRepository movementRepo,
        ICategoryRepository categoryRepo,
        IProcessedOrderRepository processedOrderRepo,
        AppDbContext dbContext,
        ILogger<SyncService> logger,
        IProductImageDownloader imageDownloader)
    {
        _registry = registry;
        _productRepo = productRepo;
        _movementRepo = movementRepo;
        _categoryRepo = categoryRepo;
        _processedOrderRepo = processedOrderRepo;
        _dbContext = dbContext;
        _logger = logger;
        _imageDownloader = imageDownloader;
    }

    public SyncService Create(IntegrationConfig storeConfig)
    {
        var integration = _registry.CreateIntegration(storeConfig);
        return new SyncService(
            integration,
            _productRepo,
            _movementRepo,
            _categoryRepo,
            _processedOrderRepo,
            _dbContext,
            storeConfig,
            _logger,
            _imageDownloader);
    }
}
