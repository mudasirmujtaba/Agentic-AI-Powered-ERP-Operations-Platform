using FluentValidation;

namespace OpsPilot.Application.Customers;

public class SaveCustomerRequestValidator : AbstractValidator<SaveCustomerRequest>
{
    public SaveCustomerRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactName).MaximumLength(150);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Status).IsInEnum();

        RuleFor(x => x.Addresses).NotNull();
        RuleForEach(x => x.Addresses).SetValidator(new CustomerAddressDtoValidator());
        RuleFor(x => x.Addresses)
            .Must(addresses => addresses.Where(a => a.IsDefault).GroupBy(a => a.Type).All(g => g.Count() == 1))
            .When(x => x.Addresses is not null)
            .WithMessage("Only one address per type can be the default.");
    }
}

public class CustomerAddressDtoValidator : AbstractValidator<CustomerAddressDto>
{
    public CustomerAddressDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Line2).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
    }
}
