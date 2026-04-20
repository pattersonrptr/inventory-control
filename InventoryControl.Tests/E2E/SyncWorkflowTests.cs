using InventoryControl.Integrations;
using InventoryControl.Integrations.Abstractions;
using InventoryControl.Models;
using InventoryControl.Repositories;
using InventoryControl.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace InventoryControl.Tests.E2E;

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
        var config = new IntegrationConfig { Name = "test-store", Enabled = true, Platform = "test" };

        var syncService = new SyncService(
            storeMock.Object, productRepo, movementRepo, categoryRepo,
            processedOrderRepo, context, config, Mock.Of<ILogger<SyncService>>());

        // Step 1: Sync products — links external ID by SKU
        storeMock.Setup(s => s.GetProductsAsync())
            .ReturnsAsync(new[] { new ExternalProduct { ExternalId = "ext-100", Sku = "SYNC-001" } });

        await syncService.SyncProductsFromStoreAsync();

        var mapping = await context.ProductExternalMappings
            .FirstOrDefaultAsync(m => m.ProductId == product.Id && m.StoreName == "test-store");
        Assert.NotNull(mapping);
        Assert.Equal("ext-100", mapping.ExternalId);

        // Step 2: Process an order — deducts stock
        var order = new ExternalOrder
        {
            ExternalOrderId = "ORD-500",
            Status = "open",
            PaymentStatus = "paid",
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

        // Step 5: Verify movement was recorded
        var movements = (await movementRepo.GetByProductAsync(product.Id)).ToList();
        Assert.Single(movements);
        Assert.Equal(MovementType.Exit, movements[0].Type);
        Assert.Equal(15, movements[0].Quantity);

        // Step 6: Simulate refund — stock should be restored
        var refundedOrder = new ExternalOrder
        {
            ExternalOrderId = "ORD-500",
            Status = "open",
            PaymentStatus = "refunded",
            CreatedAt = DateTime.UtcNow,
            Items = new List<ExternalOrderItem>
            {
                new() { ExternalProductId = "ext-100", Quantity = 15, UnitPrice = 25.00m }
            }
        };

        var refundProcessed = await syncService.ProcessOrderAsync(refundedOrder);
        Assert.True(refundProcessed);

        var afterRefund = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal(100, afterRefund!.CurrentStock);

        // Step 7: Verify refund is not processed again
        var refundAgain = await syncService.ProcessOrderAsync(refundedOrder);
        Assert.False(refundAgain);
        var stillHundred = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal(100, stillHundred!.CurrentStock);

        // Step 8: Verify both movements exist (exit + entry)
        var allMovements = (await movementRepo.GetByProductAsync(product.Id)).ToList();
        Assert.Equal(2, allMovements.Count);
        Assert.Contains(allMovements, m => m.Type == MovementType.Exit && m.Quantity == 15);
        Assert.Contains(allMovements, m => m.Type == MovementType.Entry && m.Quantity == 15);
    }
}
