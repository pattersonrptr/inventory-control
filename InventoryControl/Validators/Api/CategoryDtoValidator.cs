using FluentValidation;
using InventoryControl.Controllers.Api;

namespace InventoryControl.Validators.Api;

public class CategoryDtoValidator : AbstractValidator<CategoryDto>
{
    public CategoryDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
