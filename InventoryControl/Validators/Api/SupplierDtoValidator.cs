using FluentValidation;
using InventoryControl.Features.Products;
using InventoryControl.Features.Categories;
using InventoryControl.Features.Suppliers;

namespace InventoryControl.Validators.Api;

public class SupplierDtoValidator : AbstractValidator<SupplierDto>
{
    public SupplierDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cnpj).MaximumLength(18).When(x => x.Cnpj is not null);
        RuleFor(x => x.Phone).MaximumLength(30).When(x => x.Phone is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}
