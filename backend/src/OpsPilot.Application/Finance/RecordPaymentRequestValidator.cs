using FluentValidation;

namespace OpsPilot.Application.Finance;

public class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.PaidAtUtc).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .When(x => x.PaidAtUtc.HasValue)
            .WithMessage("Payment date cannot be in the future.");
    }
}
