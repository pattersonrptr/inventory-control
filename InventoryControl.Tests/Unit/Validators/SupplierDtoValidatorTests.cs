using FluentValidation.TestHelper;
using InventoryControl.Controllers.Api;
using InventoryControl.Validators.Api;

namespace InventoryControl.Tests.Unit.Validators;

public class SupplierDtoValidatorTests
{
    private readonly SupplierDtoValidator _sut = new();

    [Fact]
    public void Validate_ValidMinimalDto_Passes()
    {
        var dto = new SupplierDto { Name = "Acme Corp" };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidFullDto_Passes()
    {
        var dto = new SupplierDto
        {
            Name = "Acme Corp",
            Cnpj = "12345678000195",
            Phone = "+55 11 99999-9999",
            Email = "contact@acme.com"
        };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_FailsWithError()
    {
        var dto = new SupplierDto { Name = "" };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameExceeds200Chars_FailsWithError()
    {
        var dto = new SupplierDto { Name = new string('A', 201) };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_InvalidEmail_FailsWithError()
    {
        var dto = new SupplierDto { Name = "Acme", Email = "not-an-email" };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_NullEmail_Passes()
    {
        var dto = new SupplierDto { Name = "Acme", Email = null };
        var result = _sut.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_PhoneExceeds30Chars_FailsWithError()
    {
        var dto = new SupplierDto { Name = "Acme", Phone = new string('1', 31) };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Validate_CnpjExceeds18Chars_FailsWithError()
    {
        var dto = new SupplierDto { Name = "Acme", Cnpj = new string('1', 19) };
        var result = _sut.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Cnpj);
    }
}
