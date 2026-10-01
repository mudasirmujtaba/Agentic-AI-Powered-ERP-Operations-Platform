using FluentValidation;

namespace OpsPilot.Application.Service;

public class SaveTicketRequestValidator : AbstractValidator<SaveTicketRequest>
{
    public SaveTicketRequestValidator()
    {
        RuleFor(r => r.Subject).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).NotEmpty().MaximumLength(4000);
        RuleFor(r => r.CustomerId).NotEmpty();
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.Priority).IsInEnum();
    }
}

public class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
        RuleFor(r => r.Resolution).MaximumLength(2000);
    }
}

public class AddTicketCommentRequestValidator : AbstractValidator<AddTicketCommentRequest>
{
    public AddTicketCommentRequestValidator()
    {
        RuleFor(r => r.Body).NotEmpty().MaximumLength(4000);
    }
}
