using FluentValidation;
using InventoryControl.Features.Products;
using InventoryControl.Features.Categories;
using InventoryControl.Features.Suppliers;

namespace InventoryControl.Validators.Api;

public class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Sku).MaximumLength(100).When(x => x.Sku is not null);
        RuleFor(x => x.Brand).MaximumLength(100).When(x => x.Brand is not null);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public class ProductUpdateDtoValidator : AbstractValidator<ProductUpdateDto>
{
    public ProductUpdateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Sku).MaximumLength(100).When(x => x.Sku is not null);
        RuleFor(x => x.Brand).MaximumLength(100).When(x => x.Brand is not null);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public class StockUpdateDtoValidator : AbstractValidator<StockUpdateDto>
{
    public StockUpdateDtoValidator()
    {
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
    }
}
