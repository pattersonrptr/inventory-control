using ControleEstoque.Models;

namespace ControleEstoque.Tests.Unit.Models;

public class ProductTests
{
    [Theory]
    [InlineData(10, 10, true)]
    [InlineData(5, 10, true)]
    [InlineData(0, 10, true)]
    [InlineData(11, 10, false)]
    [InlineData(100, 10, false)]
    public void IsBelowMinimumStock_ReturnsExpected(int currentStock, int minimumStock, bool expected)
    {
        var product = new Product
        {
            CurrentStock = currentStock,
            MinimumStock = minimumStock
        };

        Assert.Equal(expected, product.IsBelowMinimumStock);
    }

    [Fact]
    public void IsBelowMinimumStock_ZeroMinimum_ZeroStock_ReturnsTrue()
    {
        var product = new Product { CurrentStock = 0, MinimumStock = 0 };

        Assert.True(product.IsBelowMinimumStock);
    }
}
