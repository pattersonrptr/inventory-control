using InventoryControl.Models;

namespace InventoryControl.Tests.Fixtures;

public static class TestDataBuilder
{
    public static Category CreateCategory(int id = 1, string name = "Electronics")
        => new() { Id = id, Name = name, Description = $"{name} category" };

    public static Supplier CreateSupplier(int id = 1, string name = "Acme Corp")
        => new()
        {
            Id = id,
            Name = name,
            Cnpj = "12345678000100",
            Phone = "11999999999",
            Email = "contact@acme.com"
        };

    public static Product CreateProduct(
        int id = 1,
        string name = "Widget",
        int categoryId = 1,
        int currentStock = 50,
        int minimumStock = 10,
        string? sku = "WIDGET-001")
        => new()
        {
            Id = id,
            Name = name,
            Description = $"{name} description",
            CostPrice = 10.00m,
            SellingPrice = 25.00m,
            CurrentStock = currentStock,
            MinimumStock = minimumStock,
            CategoryId = categoryId,
            Sku = sku
        };

    public static StockMovement CreateEntry(
        int productId = 1,
        int quantity = 10,
        int? supplierId = 1,
        decimal? unitCost = 10.00m)
        => new()
        {
            ProductId = productId,
            Type = MovementType.Entry,
            Quantity = quantity,
            Date = DateTime.Today,
            SupplierId = supplierId,
            UnitCost = unitCost
        };

    public static StockMovement CreateExit(
        int productId = 1,
        int quantity = 5,
        ExitReason reason = ExitReason.Sale)
        => new()
        {
            ProductId = productId,
            Type = MovementType.Exit,
            Quantity = quantity,
            Date = DateTime.Today,
            ExitReason = reason
        };
}
