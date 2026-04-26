using FluentValidation.TestHelper;

using InventoryControl.Validators.Api;

namespace InventoryControl.Tests.Unit.Validators;

public class ProductCreateDtoValidatorTests
{
    private readonly ProductCreateDtoValidator _sut = new();

    [Fact]
    public void Validate_ValidDto_PassesAllRules()
    {
        var dto = new ProductCreateDto
        {
            Name = "Widget A",
            CostPrice = 5.00m,
            SellingPrice = 10.00m,
            MinimumStock = 5,
            CategoryId = 1
        };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = "", CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameExceeds200Chars_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = new string('A', 201), CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NegativeCostPrice_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = "Widget", CostPrice = -1, CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.CostPrice);
    }

    [Fact]
    public void Validate_NegativeSellingPrice_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = "Widget", SellingPrice = -0.01m, CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.SellingPrice);
    }

    [Fact]
    public void Validate_NegativeMinimumStock_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = "Widget", MinimumStock = -1, CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.MinimumStock);
    }

    [Fact]
    public void Validate_CategoryIdZero_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = "Widget", CategoryId = 0 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }

    [Fact]
    public void Validate_SkuExceeds100Chars_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = "Widget", Sku = new string('X', 101), CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Fact]
    public void Validate_DescriptionExceeds500Chars_FailsWithError()
    {
        var dto = new ProductCreateDto { Name = "Widget", Description = new string('D', 501), CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }
}

public class ProductUpdateDtoValidatorTests
{
    private readonly ProductUpdateDtoValidator _sut = new();

    [Fact]
    public void Validate_ValidDto_PassesAllRules()
    {
        var dto = new ProductUpdateDto
        {
            Name = "Widget B",
            CostPrice = 5.00m,
            SellingPrice = 12.00m,
            MinimumStock = 3,
            CategoryId = 2
        };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_FailsWithError()
    {
        var dto = new ProductUpdateDto { Name = "", CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NegativeCostPrice_FailsWithError()
    {
        var dto = new ProductUpdateDto { Name = "Widget", CostPrice = -1, CategoryId = 1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.CostPrice);
    }

    [Fact]
    public void Validate_CategoryIdZero_FailsWithError()
    {
        var dto = new ProductUpdateDto { Name = "Widget", CategoryId = 0 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }
}

public class StockUpdateDtoValidatorTests
{
    private readonly StockUpdateDtoValidator _sut = new();

    [Fact]
    public void Validate_ZeroQuantity_Passes()
    {
        var dto = new StockUpdateDto { Quantity = 0 };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_PositiveQuantity_Passes()
    {
        var dto = new StockUpdateDto { Quantity = 100 };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NegativeQuantity_FailsWithError()
    {
        var dto = new StockUpdateDto { Quantity = -1 };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }
}
