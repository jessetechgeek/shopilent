using FluentValidation;
using Shopilent.Application.Common.Validators;

namespace Shopilent.Application.Features.Identity.Commands.ChangePassword.V1;

internal sealed class ChangePasswordCommandValidatorV1 : AbstractValidator<ChangePasswordCommandV1>
{
    public ChangePasswordCommandValidatorV1()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(v => v.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(v => v.NewPassword)
            .SetValidator(new PasswordValidator());

        RuleFor(v => v.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(v => v.NewPassword).WithMessage("Passwords do not match.");
    }
}
