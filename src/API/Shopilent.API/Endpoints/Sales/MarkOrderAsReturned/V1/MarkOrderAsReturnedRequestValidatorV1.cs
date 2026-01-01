using FastEndpoints;
using FluentValidation;

namespace Shopilent.API.Endpoints.Sales.MarkOrderAsReturned.V1;

public class MarkOrderAsReturnedRequestValidatorV1 : Validator<MarkOrderAsReturnedRequestV1>
{
    public MarkOrderAsReturnedRequestValidatorV1()
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
