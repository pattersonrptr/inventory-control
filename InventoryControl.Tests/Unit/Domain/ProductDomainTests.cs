namespace InventoryControl.Tests.Unit.Domain;

public class ProductDomainTests
{
    // --- ApplyEntry ---

    [Fact]
    public void ApplyEntry_PositiveQuantity_IncreasesCurrentStock()
    {
        var product = new Product { CurrentStock = 10 };
        product.ApplyEntry(5);
        Assert.Equal(15, product.CurrentStock);
    }

    [Fact]
    public void ApplyEntry_ZeroQuantity_ThrowsArgumentException()
    {
        var product = new Product { CurrentStock = 10 };
        Assert.Throws<ArgumentException>(() => product.ApplyEntry(0));
    }

    [Fact]
    public void ApplyEntry_NegativeQuantity_ThrowsArgumentException()
    {
        var product = new Product { CurrentStock = 10 };
        Assert.Throws<ArgumentException>(() => product.ApplyEntry(-1));
    }

    // --- ApplyExit ---

    [Fact]
    public void ApplyExit_SufficientStock_DecreasesCurrentStock()
    {
        var product = new Product { Name = "Widget", CurrentStock = 10 };
        product.ApplyExit(3);
        Assert.Equal(7, product.CurrentStock);
    }

    [Fact]
    public void ApplyExit_ExactStock_ResetsToZero()
    {
        var product = new Product { Name = "Widget", CurrentStock = 5 };
        product.ApplyExit(5);
        Assert.Equal(0, product.CurrentStock);
    }

    [Fact]
    public void ApplyExit_InsufficientStock_ThrowsInsufficientStockException()
    {
        var product = new Product { Name = "Widget", CurrentStock = 5 };
        var ex = Assert.Throws<InsufficientStockException>(() => product.ApplyExit(10));
        Assert.Equal(5, ex.Available);
        Assert.Equal(10, ex.Requested);
        Assert.Equal("Widget", ex.ProductName);
    }

    [Fact]
    public void ApplyExit_ZeroQuantity_ThrowsArgumentException()
    {
        var product = new Product { Name = "Widget", CurrentStock = 5 };
        Assert.Throws<ArgumentException>(() => product.ApplyExit(0));
    }

    // --- Margin ---

    [Fact]
    public void Margin_TypicalPrices_ReturnsGrossMarginPercent()
    {
        var product = new Product { CostPrice = 60m, SellingPrice = 100m };
        Assert.Equal(40m, product.Margin);
    }

    [Fact]
    public void Margin_ZeroSellingPrice_ReturnsZero()
    {
        var product = new Product { CostPrice = 50m, SellingPrice = 0m };
        Assert.Equal(0m, product.Margin);
    }

    [Fact]
    public void Margin_CostEqualsSelling_ReturnsZero()
    {
        var product = new Product { CostPrice = 80m, SellingPrice = 80m };
        Assert.Equal(0m, product.Margin);
    }

    [Fact]
    public void Margin_ZeroCost_ReturnsOneHundredPercent()
    {
        var product = new Product { CostPrice = 0m, SellingPrice = 50m };
        Assert.Equal(100m, product.Margin);
    }
}
