using FluentValidation;

namespace OpsPilot.Application.Sales;

public class SaveSalesOrderRequestValidator : AbstractValidator<SaveSalesOrderRequest>
{
    public SaveSalesOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one line.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).When(l => l.UnitPrice.HasValue);
        });
        RuleFor(x => x.Lines)
            .Must(lines => lines.Select(l => l.ProductId).Distinct().Count() == lines.Count)
            .When(x => x.Lines is { Count: > 0 })
            .WithMessage("Each product can appear only once; combine the quantities instead.");
    }
}
