using ControleEstoque.Data;
using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ControleEstoque.Integrations;

public class SyncServiceFactory
{
    private readonly PlatformRegistry _registry;
    private readonly IProductRepository _productRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProcessedOrderRepository _processedOrderRepo;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SyncService> _logger;

    public SyncServiceFactory(
        PlatformRegistry registry,
        IProductRepository productRepo,
        IStockMovementRepository movementRepo,
        ICategoryRepository categoryRepo,
        IProcessedOrderRepository processedOrderRepo,
        AppDbContext dbContext,
        ILogger<SyncService> logger)
    {
        _registry = registry;
        _productRepo = productRepo;
        _movementRepo = movementRepo;
        _categoryRepo = categoryRepo;
        _processedOrderRepo = processedOrderRepo;
        _dbContext = dbContext;
        _logger = logger;
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
            _logger);
    }
}
