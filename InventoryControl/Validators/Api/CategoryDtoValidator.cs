using FluentValidation;
using InventoryControl.Features.Products;
using InventoryControl.Features.Categories;
using InventoryControl.Features.Suppliers;

namespace InventoryControl.Validators.Api;

public class CategoryDtoValidator : AbstractValidator<CategoryDto>
{
    public CategoryDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.ParentId).GreaterThan(0).When(x => x.ParentId is not null);
    }
}
