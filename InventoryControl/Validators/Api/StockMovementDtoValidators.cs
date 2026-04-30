using FluentValidation;
using InventoryControl.Features.Stock;

namespace InventoryControl.Validators.Api;

public class StockEntryDtoValidator : AbstractValidator<StockEntryDto>
{
    public StockEntryDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0).When(x => x.SupplierId is not null);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
    }
}

public class StockExitDtoValidator : AbstractValidator<StockExitDto>
{
    public StockExitDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
    }
}
