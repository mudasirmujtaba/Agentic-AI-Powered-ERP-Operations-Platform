using FluentValidation;

namespace OpsPilot.Application.Products;

public class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderPoint).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SafetyStock)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.ReorderPoint)
            .WithMessage("Safety stock cannot exceed the reorder point.");
    }
}
