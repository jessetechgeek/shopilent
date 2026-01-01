using FluentValidation;

namespace Shopilent.Application.Features.Sales.Commands.MarkOrderAsReturned.V1;

public sealed class MarkOrderAsReturnedCommandValidatorV1 : AbstractValidator<MarkOrderAsReturnedCommandV1>
{
    public MarkOrderAsReturnedCommandValidatorV1()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required.");

        RuleFor(x => x.ReturnReason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.ReturnReason))
            .WithMessage("Return reason cannot exceed 500 characters.");
    }
}
