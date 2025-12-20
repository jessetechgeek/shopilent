using FluentValidation;

namespace Shopilent.Application.Common.Validators;

public class PasswordValidator : AbstractValidator<string>
{
    public PasswordValidator()
    {
        RuleFor(password => password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long")
            .Matches(@"[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]")
            .WithMessage("Password must contain at least one digit")
            .Matches(@"[@$!%*?&#^()_\-+=\[\]{};:'"",.<>/?\\|`~]")
            .WithMessage("Password must contain at least one special character")
            .Must(HaveMinimumUniqueCharacters)
            .WithMessage("Password must contain at least 1 unique characters");
    }

    private static bool HaveMinimumUniqueCharacters(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        return password.Distinct().Count() >= 1;
    }
}
