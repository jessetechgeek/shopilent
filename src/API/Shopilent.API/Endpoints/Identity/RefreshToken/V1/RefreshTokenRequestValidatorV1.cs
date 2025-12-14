using FastEndpoints;
using FluentValidation;

namespace Shopilent.API.Endpoints.Identity.RefreshToken.V1;

public class RefreshTokenRequestValidatorV1 : Validator<RefreshTokenRequestV1>
{
    public RefreshTokenRequestValidatorV1()
    {
        RuleFor(x => x.RefreshToken)
            .MaximumLength(255).WithMessage("Refresh token is too long.")
            .When(x => !string.IsNullOrEmpty(x.RefreshToken));
    }
}
