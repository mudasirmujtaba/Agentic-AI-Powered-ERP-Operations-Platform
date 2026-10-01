using FluentValidation;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Application.Inventory;

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    private static readonly InventoryTransactionType[] AllowedReasons =
        [InventoryTransactionType.Adjustment, InventoryTransactionType.Damaged, InventoryTransactionType.Return];

    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("Quantity cannot be zero.");
        RuleFor(x => x.Reason)
            .Must(r => AllowedReasons.Contains(r))
            .WithMessage("Reason must be Adjustment, Damaged or Return.");
        RuleFor(x => x.Quantity).LessThan(0).When(x => x.Reason == InventoryTransactionType.Damaged)
            .WithMessage("Damaged stock is a removal, so the quantity must be negative.");
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Reason == InventoryTransactionType.Return)
            .WithMessage("Returns add stock, so the quantity must be positive.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class TransferStockRequestValidator : AbstractValidator<TransferStockRequest>
{
    public TransferStockRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.FromWarehouseId).NotEmpty();
        RuleFor(x => x.ToWarehouseId).NotEmpty().NotEqual(x => x.FromWarehouseId)
            .WithMessage("Choose a different destination warehouse.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
