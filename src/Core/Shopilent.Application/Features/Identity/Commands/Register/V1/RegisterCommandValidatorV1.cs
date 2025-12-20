using FluentValidation;

namespace Shopilent.Application.Features.Identity.Commands.Register.V1;

internal sealed class RegisterCommandValidatorV1 : AbstractValidator<RegisterCommandV1>
{
    public RegisterCommandValidatorV1()
    {
        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.");

        RuleFor(v => v.Password)
            .SetValidator(new PasswordValidator());

        RuleFor(v => v.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(v => v.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(v => v.Phone)
            .Matches(@"^\+?[0-9\s\-\(\)]{7,20}$").WithMessage("Phone number is not valid.")
            .When(v => !string.IsNullOrEmpty(v.Phone));
    }
}