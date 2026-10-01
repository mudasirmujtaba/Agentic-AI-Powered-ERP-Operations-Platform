using FluentValidation;

namespace OpsPilot.Application.Purchasing;

public class SavePurchaseOrderRequestValidator : AbstractValidator<SavePurchaseOrderRequest>
{
    public SavePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one line.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0).When(l => l.UnitCost.HasValue);
        });
        RuleFor(x => x.Lines)
            .Must(lines => lines.Select(l => l.ProductId).Distinct().Count() == lines.Count)
            .When(x => x.Lines is { Count: > 0 })
            .WithMessage("Each product can appear only once; combine the quantities instead.");
    }
}

public class RejectPurchaseOrderRequestValidator : AbstractValidator<RejectPurchaseOrderRequest>
{
    public RejectPurchaseOrderRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class ReceiveGoodsRequestValidator : AbstractValidator<ReceiveGoodsRequest>
{
    public ReceiveGoodsRequestValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines)
            .Must(lines => lines.Any(l => l.Quantity > 0))
            .When(x => x.Lines is { Count: > 0 })
            .WithMessage("Enter a received quantity on at least one line.");
        RuleForEach(x => x.Lines).ChildRules(line => line.RuleFor(l => l.Quantity).GreaterThanOrEqualTo(0));
    }
}
