using ControleEstoque.Integrations;
using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Models;
using ControleEstoque.Repositories;
using ControleEstoque.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControleEstoque.Tests.E2E;

public class SyncWorkflowTests
{
    private readonly DatabaseFixture _fixture = new();

    [Fact]
    public async Task FullSyncWorkflow_SyncProducts_ProcessOrder_VerifyStockDeducted()
    {
        using var context = _fixture.CreateContext();

        // Seed data
        context.Categories.Add(TestDataBuilder.CreateCategory());
        context.Suppliers.Add(TestDataBuilder.CreateSupplier());
        var product = TestDataBuilder.CreateProduct(id: 0, currentStock: 100, sku: "SYNC-001");
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);
        var categoryRepo = new CategoryRepository(context);
        var processedOrderRepo = new ProcessedOrderRepository(context);
        var storeMock = new Mock<IStoreIntegration>();
        var config = new IntegrationConfig { Enabled = true, Platform = "test" };

        var syncService = new SyncService(
            storeMock.Object, productRepo, movementRepo, categoryRepo,
            processedOrderRepo, config, Mock.Of<ILogger<SyncService>>());

        // Step 1: Sync products — links external ID by SKU
        storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[] { new ExternalProduct { ExternalId = "ext-100", Sku = "SYNC-001" } });

        await syncService.SyncProductsFromStoreAsync();

        var synced = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal("ext-100", synced!.ExternalId);

        // Step 2: Process an order — deducts stock
        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-500",
            Status = "closed",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-100", Quantity = 15, UnitPrice = 25.00m }
            }
        };

        var processed = await syncService.ProcessOrderAsync(order);
        Assert.True(processed);

        var updated = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal(85, updated!.CurrentStock);

        // Step 3: Verify reprocessing the same order is skipped (deduplication)
        var reprocessed = await syncService.ProcessOrderAsync(order);
        Assert.False(reprocessed);
        var stillSame = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal(85, stillSame!.CurrentStock);

        // Step 4: Verify stock push would send the correct value
        storeMock.Setup(s => s.UpdateStockAsync("ext-100", 85)).Returns(Task.CompletedTask);
        await syncService.PushStockToStoreAsync(product.Id);
        storeMock.Verify(s => s.UpdateStockAsync("ext-100", 85), Times.Once);

        // Step 4: Verify movement was recorded
        var movements = (await movementRepo.GetByProductAsync(product.Id)).ToList();
        Assert.Single(movements);
        Assert.Equal(MovementType.Exit, movements[0].Type);
        Assert.Equal(15, movements[0].Quantity);
    }
}
