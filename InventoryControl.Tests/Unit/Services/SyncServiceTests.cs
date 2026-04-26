using InventoryControl.Infrastructure.Persistence;



using InventoryControl.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace InventoryControl.Tests.Unit.Services;

public class SyncServiceTests
{
    private readonly Mock<IStoreIntegration> _storeMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<IStockMovementRepository> _movementRepoMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepoMock = new();
    private readonly Mock<IProcessedOrderRepository> _processedOrderRepoMock = new();
    private readonly IntegrationConfig _config = new() { Name = "test-store", Enabled = true, Platform = "test-platform" };
    private readonly DatabaseFixture _fixture = new();
    private readonly AppDbContext _context;
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _context = _fixture.CreateContext();
        _sut = new SyncService(
            _storeMock.Object,
            _productRepoMock.Object,
            _movementRepoMock.Object,
            _categoryRepoMock.Object,
            _processedOrderRepoMock.Object,
            _context,
            _config,
            Mock.Of<ILogger<SyncService>>());
    }

    private async Task SeedProductAsync(Product product)
    {
        if (!await _context.Categories.AnyAsync(c => c.Id == product.CategoryId))
            _context.Categories.Add(TestDataBuilder.CreateCategory(id: product.CategoryId));
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    private async Task SeedCategoryAsync(Category category)
    {
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_MatchesBySku_CreatesMapping()
    {
        var localProduct = TestDataBuilder.CreateProduct(sku: "SKU-1");
        await SeedProductAsync(localProduct);
        _productRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { localProduct });
        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[] { new ExternalProduct { ExternalId = "ext-123", Sku = "SKU-1" } });

        await _sut.SyncProductsFromStoreAsync();

        var mapping = await _context.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == localProduct.Id && m.StoreName == "test-store");
        Assert.NotNull(mapping);
        Assert.Equal("ext-123", mapping.ExternalId);
        Assert.Equal("test-platform", mapping.Platform);
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_NoSkuMatch_DoesNotCreateMapping()
    {
        _productRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { TestDataBuilder.CreateProduct(sku: "SKU-A") });
        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[] { new ExternalProduct { ExternalId = "ext-1", Sku = "SKU-B" } });

        await _sut.SyncProductsFromStoreAsync();

        Assert.Empty(await _context.ProductExternalMappings.ToListAsync());
    }

    [Fact]
    public async Task PushProductToStoreAsync_ProductNotFound_DoesNothing()
    {
        _productRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Product?)null);

        await _sut.PushProductToStoreAsync(1);

        _storeMock.Verify(s => s.CreateProductAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(),
            It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PushProductToStoreAsync_Success_CreatesMapping()
    {
        var product = TestDataBuilder.CreateProduct();
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _storeMock.Setup(s => s.CreateProductAsync(
            product.Name, product.Description, product.SellingPrice, product.Sku, product.CurrentStock))
            .ReturnsAsync(new ExternalProduct { ExternalId = "ext-new" });

        await _sut.PushProductToStoreAsync(1);

        var mapping = await _context.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == product.Id && m.StoreName == "test-store");
        Assert.NotNull(mapping);
        Assert.Equal("ext-new", mapping.ExternalId);
    }

    [Fact]
    public async Task PushStockToStoreAsync_NoMapping_DoesNotPush()
    {
        var product = TestDataBuilder.CreateProduct();
        _productRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

        await _sut.PushStockToStoreAsync(1);

        _storeMock.Verify(s => s.UpdateStockAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PushStockToStoreAsync_WithMapping_PushesCurrentStock()
    {
        var product = TestDataBuilder.CreateProduct(currentStock: 42);
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-1",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();

        await _sut.PushStockToStoreAsync(1);

        _storeMock.Verify(s => s.UpdateStockAsync("ext-1", 42), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_MatchesByMapping_CreatesExitMovement()
    {
        var product = TestDataBuilder.CreateProduct(currentStock: 50);
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-1",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = "paid",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 3, UnitPrice = 25.00m }
            }
        };

        var result = await _sut.ProcessOrderAsync(order);

        Assert.True(result);
        _movementRepoMock.Verify(r => r.AddAsync(It.Is<StockMovement>(m =>
            m.ProductId == product.Id &&
            m.Type == MovementType.Exit &&
            m.Quantity == 3 &&
            m.ExitReason == ExitReason.Sale)), Times.Once);
        _productRepoMock.Verify(r => r.UpdateStockAsync(product.Id, 47), Times.Once);
        _processedOrderRepoMock.Verify(r => r.AddAsync(It.Is<ProcessedOrder>(po =>
            po.ExternalOrderId == "ORD-1" && po.Status == "open" && po.PaymentStatus == "paid")), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_InsufficientStock_SkipsItem()
    {
        var product = TestDataBuilder.CreateProduct(currentStock: 2);
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-1",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = "paid",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 10, UnitPrice = 25.00m }
            }
        };

        await _sut.ProcessOrderAsync(order);

        _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_NoMatchingProduct_SkipsItem()
    {
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Product>());
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = "paid",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "unknown", Quantity = 1, UnitPrice = 10.00m }
            }
        };

        await _sut.ProcessOrderAsync(order);

        _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task SyncCategoriesToStoreAsync_ExistingCategory_CreatesMapping()
    {
        var localCategory = TestDataBuilder.CreateCategory(name: "Tech");
        await SeedCategoryAsync(localCategory);
        _categoryRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { localCategory });
        _storeMock.Setup(s => s.GetCategoriesAsync())
            .ReturnsAsync(new[] { new ExternalCategory { ExternalId = "ext-cat-1", Name = "Tech" } });

        await _sut.SyncCategoriesToStoreAsync();

        var mapping = await _context.CategoryExternalMappings
            .FirstOrDefaultAsync(m => m.CategoryId == localCategory.Id && m.StoreName == "test-store");
        Assert.NotNull(mapping);
        Assert.Equal("ext-cat-1", mapping.ExternalId);
    }

    [Fact]
    public async Task SyncCategoriesToStoreAsync_NewCategory_CreatesOnStore()
    {
        var localCategory = TestDataBuilder.CreateCategory(name: "New");
        await SeedCategoryAsync(localCategory);
        _categoryRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { localCategory });
        _storeMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(Array.Empty<ExternalCategory>());
        _storeMock.Setup(s => s.CreateCategoryAsync("New"))
            .ReturnsAsync(new ExternalCategory { ExternalId = "ext-new", Name = "New" });

        await _sut.SyncCategoriesToStoreAsync();

        var mapping = await _context.CategoryExternalMappings
            .FirstOrDefaultAsync(m => m.CategoryId == localCategory.Id && m.StoreName == "test-store");
        Assert.NotNull(mapping);
        Assert.Equal("ext-new", mapping.ExternalId);
        _storeMock.Verify(s => s.CreateCategoryAsync("New"), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_AlreadyProcessed_SkipsAndReturnsFalse()
    {
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1"))
            .ReturnsAsync(new ProcessedOrder { ExternalOrderId = "ORD-1", Status = "open", PaymentStatus = "paid" });

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = "paid",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 5, UnitPrice = 10.00m }
            }
        };

        var result = await _sut.ProcessOrderAsync(order);

        Assert.False(result);
        _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<StockMovement>()), Times.Never);
        _processedOrderRepoMock.Verify(r => r.AddAsync(It.IsAny<ProcessedOrder>()), Times.Never);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("voided")]
    [InlineData("refunded")]
    [InlineData("abandoned")]
    public async Task ProcessOrderAsync_UnconfirmedPayment_SkipsAndReturnsFalse(string paymentStatus)
    {
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = paymentStatus,
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 5, UnitPrice = 10.00m }
            }
        };

        var result = await _sut.ProcessOrderAsync(order);

        Assert.False(result);
        _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_CancelledOrder_SkipsAndReturnsFalse()
    {
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "cancelled",
            PaymentStatus = "paid",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 5, UnitPrice = 10.00m }
            }
        };

        var result = await _sut.ProcessOrderAsync(order);

        Assert.False(result);
        _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("authorized")]
    public async Task ProcessOrderAsync_ConfirmedPayment_ProcessesOrder(string paymentStatus)
    {
        var product = TestDataBuilder.CreateProduct(currentStock: 50);
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-1",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = paymentStatus,
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 3, UnitPrice = 25.00m }
            }
        };

        var result = await _sut.ProcessOrderAsync(order);

        Assert.True(result);
        _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<StockMovement>()), Times.Once);
        _processedOrderRepoMock.Verify(r => r.AddAsync(It.Is<ProcessedOrder>(po =>
            po.ExternalOrderId == "ORD-1")), Times.Once);
    }

    [Theory]
    [InlineData("refunded")]
    [InlineData("voided")]
    public async Task ProcessOrderAsync_RefundedOrder_ReversesStockWithEntryMovement(string refundStatus)
    {
        var product = TestDataBuilder.CreateProduct(currentStock: 47);
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1"))
            .ReturnsAsync(new ProcessedOrder { ExternalOrderId = "ORD-1", Status = "open", PaymentStatus = "paid" });
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-1",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = refundStatus,
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 3, UnitPrice = 25.00m }
            }
        };

        var result = await _sut.ProcessOrderAsync(order);

        Assert.True(result);
        _movementRepoMock.Verify(r => r.AddAsync(It.Is<StockMovement>(m =>
            m.ProductId == product.Id &&
            m.Type == MovementType.Entry &&
            m.Quantity == 3 &&
            m.ExitReason == null)), Times.Once);
        _productRepoMock.Verify(r => r.UpdateStockAsync(product.Id, 50), Times.Once);
        _processedOrderRepoMock.Verify(r => r.UpdateAsync(It.Is<ProcessedOrder>(po =>
            po.ExternalOrderId == "ORD-1" && po.PaymentStatus == refundStatus)), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_AlreadyRefunded_SkipsAndReturnsFalse()
    {
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1"))
            .ReturnsAsync(new ProcessedOrder { ExternalOrderId = "ORD-1", Status = "open", PaymentStatus = "refunded" });

        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-1",
            Status = "open",
            PaymentStatus = "refunded",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-1", Quantity = 3, UnitPrice = 25.00m }
            }
        };

        var result = await _sut.ProcessOrderAsync(order);

        Assert.False(result);
        _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<StockMovement>()), Times.Never);
    }
}
