using FluentValidation;

namespace OpsPilot.Application.Warehouses;

public class SaveWarehouseRequestValidator : AbstractValidator<SaveWarehouseRequest>
{
    public SaveWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Location).MaximumLength(250);
    }
}
