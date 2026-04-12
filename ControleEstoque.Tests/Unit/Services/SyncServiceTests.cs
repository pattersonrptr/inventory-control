using ControleEstoque.Integrations;
using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using ControleEstoque.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControleEstoque.Tests.Unit.Services;

public class SyncServiceTests
{
    private readonly Mock<IStoreIntegration> _storeMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<IStockMovementRepository> _movementRepoMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepoMock = new();
    private readonly Mock<IProcessedOrderRepository> _processedOrderRepoMock = new();
    private readonly IntegrationConfig _config = new() { Enabled = true, Platform = "test-platform" };
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _sut = new SyncService(
            _storeMock.Object,
            _productRepoMock.Object,
            _movementRepoMock.Object,
            _categoryRepoMock.Object,
            _processedOrderRepoMock.Object,
            _config,
            Mock.Of<ILogger<SyncService>>());
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_MatchesBySku_UpdatesExternalId()
    {
        var localProduct = TestDataBuilder.CreateProduct(sku: "SKU-1");
        _productRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { localProduct });
        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[] { new ExternalProduct { ExternalId = "ext-123", Sku = "SKU-1" } });

        await _sut.SyncProductsFromStoreAsync();

        Assert.Equal("ext-123", localProduct.ExternalId);
        Assert.Equal("test-platform", localProduct.ExternalIdSource);
        _productRepoMock.Verify(r => r.UpdateAsync(localProduct), Times.Once);
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_NoSkuMatch_DoesNotUpdate()
    {
        _productRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { TestDataBuilder.CreateProduct(sku: "SKU-A") });
        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[] { new ExternalProduct { ExternalId = "ext-1", Sku = "SKU-B" } });

        await _sut.SyncProductsFromStoreAsync();

        _productRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
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
    public async Task PushProductToStoreAsync_Success_SavesExternalId()
    {
        var product = TestDataBuilder.CreateProduct();
        _productRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _storeMock.Setup(s => s.CreateProductAsync(
            product.Name, product.Description, product.SellingPrice, product.Sku, product.CurrentStock))
            .ReturnsAsync(new ExternalProduct { ExternalId = "ext-new" });

        await _sut.PushProductToStoreAsync(1);

        Assert.Equal("ext-new", product.ExternalId);
        _productRepoMock.Verify(r => r.UpdateAsync(product), Times.Once);
    }

    [Fact]
    public async Task PushStockToStoreAsync_NoExternalId_DoesNotPush()
    {
        var product = TestDataBuilder.CreateProduct(externalId: null);
        _productRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

        await _sut.PushStockToStoreAsync(1);

        _storeMock.Verify(s => s.UpdateStockAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PushStockToStoreAsync_WithExternalId_PushesCurrentStock()
    {
        var product = TestDataBuilder.CreateProduct(currentStock: 42, externalId: "ext-1");
        _productRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

        await _sut.PushStockToStoreAsync(1);

        _storeMock.Verify(s => s.UpdateStockAsync("ext-1", 42), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_MatchesByExternalId_CreatesExitMovement()
    {
        var product = TestDataBuilder.CreateProduct(currentStock: 50, externalId: "ext-1");
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);

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
        var product = TestDataBuilder.CreateProduct(currentStock: 2, externalId: "ext-1");
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);

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
    public async Task SyncCategoriesToStoreAsync_ExistingCategory_LinksExternalId()
    {
        var localCategory = TestDataBuilder.CreateCategory(name: "Tech");
        _categoryRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { localCategory });
        _storeMock.Setup(s => s.GetCategoriesAsync())
            .ReturnsAsync(new[] { new ExternalCategory { ExternalId = "ext-cat-1", Name = "Tech" } });

        await _sut.SyncCategoriesToStoreAsync();

        Assert.Equal("ext-cat-1", localCategory.ExternalId);
        _categoryRepoMock.Verify(r => r.UpdateAsync(localCategory), Times.Once);
    }

    [Fact]
    public async Task SyncCategoriesToStoreAsync_NewCategory_CreatesOnStore()
    {
        var localCategory = TestDataBuilder.CreateCategory(name: "New");
        _categoryRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { localCategory });
        _storeMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(Array.Empty<ExternalCategory>());
        _storeMock.Setup(s => s.CreateCategoryAsync("New"))
            .ReturnsAsync(new ExternalCategory { ExternalId = "ext-new", Name = "New" });

        await _sut.SyncCategoriesToStoreAsync();

        Assert.Equal("ext-new", localCategory.ExternalId);
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
        var product = TestDataBuilder.CreateProduct(currentStock: 50, externalId: "ext-1");
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1")).ReturnsAsync((ProcessedOrder?)null);

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
        var product = TestDataBuilder.CreateProduct(currentStock: 47, externalId: "ext-1");
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { product });
        _processedOrderRepoMock.Setup(r => r.GetByExternalOrderIdAsync("ORD-1"))
            .ReturnsAsync(new ProcessedOrder { ExternalOrderId = "ORD-1", Status = "open", PaymentStatus = "paid" });

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
