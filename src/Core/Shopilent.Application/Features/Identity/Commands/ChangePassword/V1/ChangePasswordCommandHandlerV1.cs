using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Identity.Commands.ChangePassword.V1;

internal sealed class ChangePasswordCommandHandlerV1 : ICommandHandler<ChangePasswordCommandV1>
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<ChangePasswordCommandHandlerV1> _logger;

    public ChangePasswordCommandHandlerV1(
        IAuthenticationService authenticationService,
        ILogger<ChangePasswordCommandHandlerV1> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public async Task<Result> Handle(ChangePasswordCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate password match
            if (request.NewPassword != request.ConfirmPassword)
            {
                return Result.Failure(
                    Error.Validation(
                        code: "ChangePassword.PasswordMismatch",
                        message: "The new password and confirmation password do not match."));
            }

            if (request.NewPassword == request.CurrentPassword)
            {
                return Result.Failure(
                    Error.Validation(
                        code: "ChangePassword.SameAsCurrent",
                        message: "The new password must be different from the current password.")
                );
            }

            var result = await _authenticationService.ChangePasswordAsync(
                request.UserId,
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to change password for user {UserId}: {Error}", request.UserId, result.Error);
                return result;
            }

            _logger.LogInformation("Password changed successfully for user {UserId}", request.UserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when changing password for user {UserId}", request.UserId);
            
            return Result.Failure(
                Error.Failure(
                    code: "ChangePassword.Failed",
                    message: $"Failed to change password: {ex.Message}"));
        }
    }
}