using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Write;

namespace Shopilent.Application.Features.Identity.Commands.ChangeUserRole.V1;

internal sealed class ChangeUserRoleCommandHandlerV1 : ICommandHandler<ChangeUserRoleCommandV1, string>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly ILogger<ChangeUserRoleCommandHandlerV1> _logger;

    public ChangeUserRoleCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        ILogger<ChangeUserRoleCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(ChangeUserRoleCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get the user
            var user = await _userWriteRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", request.UserId);
                return Result.Failure<string>(UserErrors.NotFound(request.UserId));
            }

            // Check if role is already the same
            if (user.Role == request.NewRole)
            {
                _logger.LogInformation("User {UserId} already has role {Role}", request.UserId, request.NewRole);
                return Result.Success($"User already has role {request.NewRole}");
            }

            // Update the user role
            var result = user.SetRole(request.NewRole);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to set role for user {UserId}: {Error}", request.UserId, result.Error);
                return Result.Failure<string>(result.Error);
            }

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully changed role for user {UserId} to {NewRole}",
                request.UserId, request.NewRole);

            return Result.Success($"User role successfully changed to {request.NewRole}");
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning("Concurrency conflict while changing role for user {UserId}: {Error}",
                request.UserId, ex.Error);
            return Result.Failure<string>(ex.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing role for user {UserId}", request.UserId);
            return Result.Failure<string>(
                Error.Failure(
                    code: "ChangeUserRole.Failed",
                    message: $"Failed to change user role: {ex.Message}"));
        }
    }
}
