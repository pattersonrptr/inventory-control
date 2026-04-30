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
    private readonly Mock<IProductImageDownloader> _imageDownloaderMock = new();
    private readonly Mock<IProductImageUploader> _imageUploaderMock = new();
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
            Mock.Of<ILogger<SyncService>>(),
            _imageDownloaderMock.Object,
            _imageUploaderMock.Object);
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
    public async Task SyncProductsFromStoreAsync_NoSkuMatch_CreatesLocalProductAndMapping()
    {
        _productRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { TestDataBuilder.CreateProduct(sku: "SKU-A") });
        _productRepoMock.Setup(r => r.AddAsync(It.IsAny<Product>()))
            .Callback<Product>(p =>
            {
                _context.Products.Add(p);
                _context.SaveChanges();
            })
            .Returns(Task.CompletedTask);
        _categoryRepoMock.Setup(r => r.AddAsync(It.IsAny<Category>()))
            .Callback<Category>(c =>
            {
                _context.Categories.Add(c);
                _context.SaveChanges();
            })
            .Returns(Task.CompletedTask);

        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[]
            {
                new ExternalProduct
                {
                    ExternalId = "ext-1",
                    Sku = "SKU-B",
                    Name = "Pulled Product",
                    Price = 19.90m,
                    Stock = 5
                }
            });

        var summary = await _sut.SyncProductsFromStoreAsync();

        Assert.Equal(1, summary.Created);
        Assert.Equal(1, summary.NeedsCostReview);

        var created = await _context.Products.FirstOrDefaultAsync(p => p.Sku == "SKU-B");
        Assert.NotNull(created);
        Assert.Equal("Pulled Product", created.Name);
        Assert.Equal(0m, created.CostPrice);
        Assert.Equal(19.90m, created.SellingPrice);
        Assert.Equal(5, created.CurrentStock);

        var mapping = await _context.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ExternalId == "ext-1");
        Assert.NotNull(mapping);
        Assert.Equal(created.Id, mapping.ProductId);

        var fallback = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Sem categoria");
        Assert.NotNull(fallback);
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_ExistingMappingForSkulessExternal_LinksWithoutCreating()
    {
        // Reproduces the duplication bug: an external product without SKU was
        // created locally on a previous sync; on re-sync it must match by mapping
        // (ExternalId), not by SKU, otherwise a duplicate is created every run.
        var existing = TestDataBuilder.CreateProduct(sku: null);
        existing.Name = "Test Product";
        existing.SellingPrice = 9.99m;
        await SeedProductAsync(existing);

        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = existing.Id,
            StoreName = "test-store",
            ExternalId = "ext-no-sku",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();

        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { existing });
        _productRepoMock.Setup(r => r.AddAsync(It.IsAny<Product>()))
            .Callback<Product>(p => { _context.Products.Add(p); _context.SaveChanges(); })
            .Returns(Task.CompletedTask);

        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[]
            {
                new ExternalProduct
                {
                    ExternalId = "ext-no-sku",
                    Sku = "",
                    Name = "Test Product",
                    Price = 9.99m
                }
            });

        var summary = await _sut.SyncProductsFromStoreAsync();

        Assert.Equal(1, summary.Linked);
        Assert.Equal(0, summary.Created);
        _productRepoMock.Verify(r => r.AddAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_NewProductWithImages_TriggersImageDownload()
    {
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Product>());
        _productRepoMock.Setup(r => r.AddAsync(It.IsAny<Product>()))
            .Callback<Product>(p => { _context.Products.Add(p); _context.SaveChanges(); })
            .Returns(Task.CompletedTask);
        _categoryRepoMock.Setup(r => r.AddAsync(It.IsAny<Category>()))
            .Callback<Category>(c => { _context.Categories.Add(c); _context.SaveChanges(); })
            .Returns(Task.CompletedTask);

        var images = new List<ExternalImage>
        {
            new() { ExternalId = "img-1", Url = "https://store.example/a.jpg", Position = 1 },
            new() { ExternalId = "img-2", Url = "https://store.example/b.jpg", Position = 2 }
        };

        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[]
            {
                new ExternalProduct
                {
                    ExternalId = "ext-img",
                    Sku = "SKU-IMG",
                    Name = "With Images",
                    Price = 9.99m,
                    Stock = 1,
                    Images = images
                }
            });

        _imageDownloaderMock
            .Setup(d => d.DownloadAndSaveAsync(It.IsAny<int>(), It.IsAny<IEnumerable<ExternalImage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var summary = await _sut.SyncProductsFromStoreAsync();

        Assert.Equal(2, summary.ImagesDownloaded);
        _imageDownloaderMock.Verify(d => d.DownloadAndSaveAsync(
            It.IsAny<int>(),
            It.Is<IEnumerable<ExternalImage>>(imgs => imgs.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_LinkedExistingProduct_DoesNotImportImages()
    {
        var local = TestDataBuilder.CreateProduct(sku: "SKU-LINKED");
        await SeedProductAsync(local);
        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { local });

        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[]
            {
                new ExternalProduct
                {
                    ExternalId = "ext-linked",
                    Sku = "SKU-LINKED",
                    Name = local.Name,
                    Price = local.SellingPrice,
                    Images = new List<ExternalImage>
                    {
                        new() { ExternalId = "img-x", Url = "https://store.example/x.jpg" }
                    }
                }
            });

        await _sut.SyncProductsFromStoreAsync();

        _imageDownloaderMock.Verify(d => d.DownloadAndSaveAsync(
            It.IsAny<int>(),
            It.IsAny<IEnumerable<ExternalImage>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportImagesForLinkedProductAsync_NoMapping_ReturnsZero()
    {
        var product = TestDataBuilder.CreateProduct(sku: "SKU-NM");
        await SeedProductAsync(product);

        var saved = await _sut.ImportImagesForLinkedProductAsync(product.Id);

        Assert.Equal(0, saved);
        _imageDownloaderMock.Verify(d => d.DownloadAndSaveAsync(
            It.IsAny<int>(),
            It.IsAny<IEnumerable<ExternalImage>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportImagesForLinkedProductAsync_WithMapping_DownloadsImages()
    {
        var product = TestDataBuilder.CreateProduct(sku: "SKU-WM");
        await SeedProductAsync(product);
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-555",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();

        var images = new List<ExternalImage>
        {
            new() { ExternalId = "img-99", Url = "https://store.example/99.png", Position = 1 }
        };

        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[]
            {
                new ExternalProduct { ExternalId = "ext-555", Sku = "SKU-WM", Images = images }
            });

        _imageDownloaderMock
            .Setup(d => d.DownloadAndSaveAsync(product.Id, It.IsAny<IEnumerable<ExternalImage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var saved = await _sut.ImportImagesForLinkedProductAsync(product.Id);

        Assert.Equal(1, saved);
    }

    [Fact]
    public async Task SyncProductsFromStoreAsync_MatchingSkuWithDivergentName_FlagsConflict()
    {
        var local = TestDataBuilder.CreateProduct(sku: "SKU-X");
        local.Name = "Local Name";
        local.SellingPrice = 10m;
        await SeedProductAsync(local);

        _productRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { local });
        _storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[]
            {
                new ExternalProduct
                {
                    ExternalId = "ext-9",
                    Sku = "SKU-X",
                    Name = "External Name",
                    Price = 12m
                }
            });

        var summary = await _sut.SyncProductsFromStoreAsync();

        Assert.Equal(1, summary.Conflicts);

        var mapping = await _context.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == local.Id);
        Assert.NotNull(mapping);
        Assert.True(mapping.HasConflict);
        Assert.Contains("Name", mapping.ConflictDetails);
        Assert.Contains("Price", mapping.ConflictDetails);
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
    public async Task PushProductToStoreAsync_Success_AlsoUploadsImages()
    {
        var product = TestDataBuilder.CreateProduct();
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _storeMock.Setup(s => s.CreateProductAsync(
            product.Name, product.Description, product.SellingPrice, product.Sku, product.CurrentStock))
            .ReturnsAsync(new ExternalProduct { ExternalId = "ext-img" });
        _imageUploaderMock
            .Setup(u => u.UploadPendingAsync(product.Id, _storeMock.Object, "ext-img", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageUploadSummary(Uploaded: 2, SkippedFileMissing: 0, SkippedTooLarge: 0, Failed: 0));

        await _sut.PushProductToStoreAsync(product.Id);

        _imageUploaderMock.Verify(u => u.UploadPendingAsync(
            product.Id, _storeMock.Object, "ext-img", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushProductToStoreAsync_ImageUploadThrows_DoesNotPropagate()
    {
        var product = TestDataBuilder.CreateProduct();
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _storeMock.Setup(s => s.CreateProductAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(),
            It.IsAny<string?>(), It.IsAny<int>()))
            .ReturnsAsync(new ExternalProduct { ExternalId = "ext-img2" });
        _imageUploaderMock
            .Setup(u => u.UploadPendingAsync(It.IsAny<int>(), It.IsAny<IStoreIntegration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("upload failed"));

        await _sut.PushProductToStoreAsync(product.Id);

        var mapping = await _context.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == product.Id);
        Assert.NotNull(mapping);
        Assert.Equal("ext-img2", mapping.ExternalId);
    }

    [Fact]
    public async Task PushImagesToStoreAsync_NoMapping_ReturnsEmpty()
    {
        var product = TestDataBuilder.CreateProduct();
        await SeedProductAsync(product);

        var summary = await _sut.PushImagesToStoreAsync(product.Id);

        Assert.Equal(0, summary.Uploaded);
        Assert.Equal(0, summary.Total);
        _imageUploaderMock.Verify(u => u.UploadPendingAsync(
            It.IsAny<int>(), It.IsAny<IStoreIntegration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PushImagesToStoreAsync_WithMapping_DelegatesToUploader()
    {
        var product = TestDataBuilder.CreateProduct();
        await SeedProductAsync(product);
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-99",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();
        _imageUploaderMock
            .Setup(u => u.UploadPendingAsync(product.Id, _storeMock.Object, "ext-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageUploadSummary(Uploaded: 3, SkippedFileMissing: 0, SkippedTooLarge: 0, Failed: 0));

        var summary = await _sut.PushImagesToStoreAsync(product.Id);

        Assert.Equal(3, summary.Uploaded);
    }

    [Fact]
    public async Task PushToStoreAsync_NotMapped_CreatesProduct()
    {
        var product = TestDataBuilder.CreateProduct();
        await SeedProductAsync(product);
        _productRepoMock.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _storeMock.Setup(s => s.CreateProductAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(),
            It.IsAny<string?>(), It.IsAny<int>()))
            .ReturnsAsync(new ExternalProduct { ExternalId = "ext-unified" });
        _imageUploaderMock
            .Setup(u => u.UploadPendingAsync(It.IsAny<int>(), It.IsAny<IStoreIntegration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageUploadSummary.Empty);

        var result = await _sut.PushToStoreAsync(product.Id);

        Assert.True(result.WasNewlyCreated);
        _storeMock.Verify(s => s.CreateProductAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(),
            It.IsAny<string?>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task PushToStoreAsync_AlreadyMapped_OnlyUploadsImages()
    {
        var product = TestDataBuilder.CreateProduct();
        await SeedProductAsync(product);
        _context.ProductExternalMappings.Add(new ProductExternalMapping
        {
            ProductId = product.Id,
            StoreName = "test-store",
            ExternalId = "ext-77",
            Platform = "test-platform"
        });
        await _context.SaveChangesAsync();
        _imageUploaderMock
            .Setup(u => u.UploadPendingAsync(product.Id, _storeMock.Object, "ext-77", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageUploadSummary(Uploaded: 1, SkippedFileMissing: 0, SkippedTooLarge: 0, Failed: 0));

        var result = await _sut.PushToStoreAsync(product.Id);

        Assert.False(result.WasNewlyCreated);
        Assert.Equal(1, result.ImageSummary.Uploaded);
        _storeMock.Verify(s => s.CreateProductAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(),
            It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
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
