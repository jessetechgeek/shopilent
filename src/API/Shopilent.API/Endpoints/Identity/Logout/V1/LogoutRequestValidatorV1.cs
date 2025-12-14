using FastEndpoints;
using FluentValidation;

namespace Shopilent.API.Endpoints.Identity.Logout.V1;

public class LogoutRequestValidatorV1 : Validator<LogoutRequestV1>
{
    public LogoutRequestValidatorV1()
    {
        RuleFor(x => x.RefreshToken)
            .MaximumLength(255).WithMessage("Refresh token is too long.")
            .When(x => !string.IsNullOrEmpty(x.RefreshToken));

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason is too long.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
