using InventoryControl.ViewModels;

namespace InventoryControl.Tests.Unit.ViewModels;

public class MonthlyReportViewModelTests
{
    [Fact]
    public void MonthName_ReturnsFormattedMonthYear()
    {
        var vm = new MonthlyReportViewModel { Month = 3, Year = 2026 };

        Assert.Contains("2026", vm.MonthName);
    }

    [Fact]
    public void TotalEntries_SumsAllItems()
    {
        var vm = new MonthlyReportViewModel
        {
            Month = 1,
            Year = 2026,
            Items = new[]
            {
                new MonthlyReportItem { ProductName = "A", TotalEntries = 10, TotalExits = 3 },
                new MonthlyReportItem { ProductName = "B", TotalEntries = 20, TotalExits = 5 }
            }
        };

        Assert.Equal(30, vm.TotalEntries);
    }

    [Fact]
    public void TotalExits_SumsAllItems()
    {
        var vm = new MonthlyReportViewModel
        {
            Month = 1,
            Year = 2026,
            Items = new[]
            {
                new MonthlyReportItem { ProductName = "A", TotalEntries = 10, TotalExits = 3 },
                new MonthlyReportItem { ProductName = "B", TotalEntries = 20, TotalExits = 5 }
            }
        };

        Assert.Equal(8, vm.TotalExits);
    }

    [Fact]
    public void Balance_ReturnsEntriesMinusExits()
    {
        var item = new MonthlyReportItem
        {
            ProductName = "Widget",
            TotalEntries = 100,
            TotalExits = 40
        };

        Assert.Equal(60, item.Balance);
    }

    [Fact]
    public void EmptyItems_ReturnZeroTotals()
    {
        var vm = new MonthlyReportViewModel { Month = 1, Year = 2026 };

        Assert.Equal(0, vm.TotalEntries);
        Assert.Equal(0, vm.TotalExits);
    }
}
