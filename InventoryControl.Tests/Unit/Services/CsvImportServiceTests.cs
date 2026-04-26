using System.Text;


namespace InventoryControl.Tests.Unit.Services;

/// <summary>
/// Characterization tests for CsvImportService.
/// Documents current parsing behavior before Fase 2 validation hardening.
/// </summary>
public class CsvImportServiceTests
{
    private static Stream ToStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    // ── ParseProducts ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseProducts_ValidCsvSemicolonWithPtBrDecimals_ReturnsCorrectProducts()
    {
        // Brazilian standard: semicolon separator + comma decimal (avoids ambiguity).
        var csv = "Name;CostPrice;SellingPrice;MinimumStock;Sku\n" +
                  "Widget A;10,00;25,00;5;SKU-001\n" +
                  "Widget B;20,00;50,00;3;SKU-002";
        var categories = Enumerable.Empty<Category>();

        var result = CsvImportService.ParseProducts(ToStream(csv), categories);

        Assert.Equal(2, result.ValidItems.Count);
        Assert.Empty(result.Errors);

        var first = result.ValidItems[0];
        Assert.Equal("Widget A", first.Name);
        Assert.Equal(10.00m, first.CostPrice);
        Assert.Equal(25.00m, first.SellingPrice);
        Assert.Equal(5, first.MinimumStock);
        Assert.Equal("SKU-001", first.Sku);
    }

    [Fact]
    public void ParseProducts_SemicolonSeparatorWithPtBrDecimals_ParsesCorrectly()
    {
        var csv = "Name;CostPrice;SellingPrice\n" +
                  "Widget C;15,00;30,00";
        var categories = Enumerable.Empty<Category>();

        var result = CsvImportService.ParseProducts(ToStream(csv), categories);

        Assert.Single(result.ValidItems);
        Assert.Empty(result.Errors);
        Assert.Equal("Widget C", result.ValidItems[0].Name);
        Assert.Equal(15.00m, result.ValidItems[0].CostPrice);
    }

    [Fact]
    public void ParseProducts_WithCategory_MapsToCorrectCategoryId()
    {
        var csv = "Name,Category\nWidget D,Electronics";
        var categories = new[] { new Category { Id = 42, Name = "Electronics" } };

        var result = CsvImportService.ParseProducts(ToStream(csv), categories);

        Assert.Single(result.ValidItems);
        Assert.Equal(42, result.ValidItems[0].CategoryId);
    }

    [Fact]
    public void ParseProducts_UnknownCategory_AddsErrorAndSkipsRow()
    {
        var csv = "Name,Category\nWidget E,NonExistent";
        var categories = Enumerable.Empty<Category>();

        var result = CsvImportService.ParseProducts(ToStream(csv), categories);

        Assert.Empty(result.ValidItems);
        Assert.Single(result.Errors);
        Assert.Contains("NonExistent", result.Errors[0].Message);
    }

    [Fact]
    public void ParseProducts_EmptyStream_ReturnsHeaderError()
    {
        var result = CsvImportService.ParseProducts(ToStream(""), Enumerable.Empty<Category>());

        Assert.Single(result.Errors);
        Assert.Contains("empty", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseProducts_MissingNameColumn_ReturnsHeaderError()
    {
        var csv = "CostPrice,SellingPrice\n10,20";

        var result = CsvImportService.ParseProducts(ToStream(csv), Enumerable.Empty<Category>());

        Assert.Single(result.Errors);
        Assert.Contains("Name", result.Errors[0].Message);
    }

    [Fact]
    public void ParseProducts_RowWithEmptyName_AddsRowError()
    {
        var csv = "Name,CostPrice\n,10.00";

        var result = CsvImportService.ParseProducts(ToStream(csv), Enumerable.Empty<Category>());

        Assert.Empty(result.ValidItems);
        Assert.Single(result.Errors);
        Assert.Contains("required", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseProducts_InvalidCostPrice_AddsRowError()
    {
        var csv = "Name,CostPrice\nWidget F,not-a-number";

        var result = CsvImportService.ParseProducts(ToStream(csv), Enumerable.Empty<Category>());

        Assert.Empty(result.ValidItems);
        Assert.Single(result.Errors);
        Assert.Contains("cost price", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseProducts_BlankLines_AreSkipped()
    {
        var csv = "Name,CostPrice\nWidget G,5.00\n\nWidget H,8.00";

        var result = CsvImportService.ParseProducts(ToStream(csv), Enumerable.Empty<Category>());

        Assert.Equal(2, result.ValidItems.Count);
        Assert.Empty(result.Errors);
    }

    // ── ParseCategories ────────────────────────────────────────────────────────

    [Fact]
    public void ParseCategories_ValidCsv_ReturnsCorrectCategories()
    {
        var csv = "Name,Description\nElectronics,Electronic goods\nClothing,Apparel";

        var result = CsvImportService.ParseCategories(ToStream(csv));

        Assert.Equal(2, result.ValidItems.Count);
        Assert.Empty(result.Errors);
        Assert.Equal("Electronics", result.ValidItems[0].Name);
        Assert.Equal("Electronic goods", result.ValidItems[0].Description);
    }

    [Fact]
    public void ParseCategories_EmptyStream_ReturnsHeaderError()
    {
        var result = CsvImportService.ParseCategories(ToStream(""));

        Assert.Single(result.Errors);
        Assert.Contains("empty", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseCategories_MissingNameColumn_ReturnsHeaderError()
    {
        var result = CsvImportService.ParseCategories(ToStream("Description\nSomething"));

        Assert.Single(result.Errors);
        Assert.Contains("Name", result.Errors[0].Message);
    }
}
