using FluentValidation.TestHelper;
using InventoryControl.Controllers.Api;
using InventoryControl.Validators.Api;

namespace InventoryControl.Tests.Unit.Validators;

public class CategoryDtoValidatorTests
{
    private readonly CategoryDtoValidator _sut = new();

    [Fact]
    public void Validate_ValidDto_PassesAllRules()
    {
        var dto = new CategoryDto { Name = "Electronics", Description = "All electronics" };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_FailsWithError()
    {
        var dto = new CategoryDto { Name = "" };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameExceeds100Chars_FailsWithError()
    {
        var dto = new CategoryDto { Name = new string('A', 101) };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_DescriptionExceeds500Chars_FailsWithError()
    {
        var dto = new CategoryDto { Name = "Valid", Description = new string('D', 501) };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_NullDescription_Passes()
    {
        var dto = new CategoryDto { Name = "Books", Description = null };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
