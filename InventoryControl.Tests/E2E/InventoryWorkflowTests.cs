using InventoryControl.Infrastructure.Persistence;

using InventoryControl.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryControl.Tests.E2E;

public class InventoryWorkflowTests
{
    private readonly DatabaseFixture _fixture = new();

    [Fact]
    public async Task FullWorkflow_CreateProduct_AddEntry_RecordExit_VerifyStock()
    {
        using var context = _fixture.CreateContext();

        // Arrange: create supporting data
        var category = TestDataBuilder.CreateCategory();
        var supplier = TestDataBuilder.CreateSupplier();
        context.Categories.Add(category);
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();

        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        // Act 1: Create a product with 0 stock
        var product = TestDataBuilder.CreateProduct(id: 0, categoryId: category.Id, currentStock: 0, minimumStock: 5);
        await productRepo.AddAsync(product);
        var savedProduct = await productRepo.GetByIdAsync(product.Id);

        Assert.NotNull(savedProduct);
        Assert.Equal(0, savedProduct.CurrentStock);
        Assert.True(savedProduct.IsBelowMinimumStock);

        // Act 2: Record a stock entry of 20 units
        var entry = new StockMovement
        {
            ProductId = product.Id,
            Type = MovementType.Entry,
            Quantity = 20,
            Date = DateTime.Today,
            SupplierId = supplier.Id,
            UnitCost = 10.00m
        };
        await movementRepo.AddAsync(entry);
        await productRepo.UpdateStockAsync(product.Id, 20);

        savedProduct = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal(20, savedProduct!.CurrentStock);
        Assert.False(savedProduct.IsBelowMinimumStock);

        // Act 3: Record a stock exit of 8 units (sale)
        var exit = new StockMovement
        {
            ProductId = product.Id,
            Type = MovementType.Exit,
            Quantity = 8,
            Date = DateTime.Today,
            ExitReason = ExitReason.Sale
        };
        await movementRepo.AddAsync(exit);
        await productRepo.UpdateStockAsync(product.Id, 12);

        savedProduct = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal(12, savedProduct!.CurrentStock);
        Assert.False(savedProduct.IsBelowMinimumStock);

        // Act 4: Record another exit bringing stock below minimum
        var exit2 = new StockMovement
        {
            ProductId = product.Id,
            Type = MovementType.Exit,
            Quantity = 8,
            Date = DateTime.Today,
            ExitReason = ExitReason.Loss
        };
        await movementRepo.AddAsync(exit2);
        await productRepo.UpdateStockAsync(product.Id, 4);

        savedProduct = await productRepo.GetByIdAsync(product.Id);
        Assert.Equal(4, savedProduct!.CurrentStock);
        Assert.True(savedProduct.IsBelowMinimumStock);

        // Verify: product appears in below minimum list
        var belowMin = (await productRepo.GetBelowMinimumAsync()).ToList();
        Assert.Contains(belowMin, p => p.Id == product.Id);

        // Verify: all movements recorded
        var movements = (await movementRepo.GetByProductAsync(product.Id)).ToList();
        Assert.Equal(3, movements.Count);
    }
}
