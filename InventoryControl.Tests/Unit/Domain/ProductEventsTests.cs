namespace InventoryControl.Tests.Unit.Domain;

public class ProductEventsTests
{
    [Fact]
    public void NewProduct_HasNoDomainEvents()
    {
        var product = new Product();
        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    public void ApplyEntry_RaisesStockChangedEvent()
    {
        var product = new Product { Id = 1, CurrentStock = 5 };
        product.ApplyEntry(3);

        var evt = Assert.Single(product.DomainEvents);
        var stockChanged = Assert.IsType<StockChanged>(evt);
        Assert.Equal(1, stockChanged.ProductId);
        Assert.Equal(8, stockChanged.NewStock);
    }

    [Fact]
    public void ApplyExit_AboveMinimum_RaisesOnlyStockChanged()
    {
        var product = new Product { Id = 1, Name = "Widget", CurrentStock = 10, MinimumStock = 2 };
        product.ApplyExit(3);

        var evt = Assert.Single(product.DomainEvents);
        Assert.IsType<StockChanged>(evt);
    }

    [Fact]
    public void ApplyExit_CrossingBelowMinimum_RaisesBothEvents()
    {
        var product = new Product { Id = 1, Name = "Widget", CurrentStock = 5, MinimumStock = 3 };
        product.ApplyExit(3);

        Assert.Equal(2, product.DomainEvents.Count);
        Assert.Contains(product.DomainEvents, e => e is StockChanged);
        var below = Assert.Single(product.DomainEvents.OfType<ProductWentBelowMinimum>());
        Assert.Equal(2, below.CurrentStock);
        Assert.Equal(3, below.MinimumStock);
        Assert.Equal("Widget", below.ProductName);
    }

    [Fact]
    public void ApplyExit_AlreadyBelowMinimum_DoesNotRaiseProductWentBelowMinimumAgain()
    {
        // Already below minimum (1 < 5). Another exit keeps it below; should not re-raise.
        var product = new Product { Id = 1, Name = "Widget", CurrentStock = 1, MinimumStock = 5 };
        product.ApplyExit(1);

        Assert.DoesNotContain(product.DomainEvents, e => e is ProductWentBelowMinimum);
        Assert.Contains(product.DomainEvents, e => e is StockChanged);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllAccumulatedEvents()
    {
        var product = new Product { Id = 1, CurrentStock = 5 };
        product.ApplyEntry(2);
        product.ClearDomainEvents();

        Assert.Empty(product.DomainEvents);
    }
}
