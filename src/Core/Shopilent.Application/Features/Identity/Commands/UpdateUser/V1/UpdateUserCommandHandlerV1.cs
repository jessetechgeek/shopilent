using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Application.Features.Identity.Commands.UpdateUser.V1;

internal sealed class UpdateUserCommandHandlerV1 : ICommandHandler<UpdateUserCommandV1, UpdateUserResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateUserCommandHandlerV1> _logger;

    public UpdateUserCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateUserCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateUserResponseV1>> Handle(UpdateUserCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting to update user {UserId}", request.UserId);

            // Get the user to update
            var user = await _userWriteRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", request.UserId);
                return Result.Failure<UpdateUserResponseV1>(UserErrors.NotFound(request.UserId));
            }

            // Create value objects
            var fullNameResult = FullName.Create(request.FirstName, request.LastName, request.MiddleName);
            if (fullNameResult.IsFailure)
            {
                _logger.LogWarning("Invalid full name for user {UserId}: {Error}", request.UserId, fullNameResult.Error);
                return Result.Failure<UpdateUserResponseV1>(fullNameResult.Error);
            }

            PhoneNumber phoneNumber = null;
            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                var phoneResult = PhoneNumber.Create(request.Phone);
                if (phoneResult.IsFailure)
                {
                    _logger.LogWarning("Invalid phone number for user {UserId}: {Error}", request.UserId, phoneResult.Error);
                    return Result.Failure<UpdateUserResponseV1>(phoneResult.Error);
                }
                phoneNumber = phoneResult.Value;
            }

            // Update user personal information
            var updateResult = user.UpdatePersonalInfo(fullNameResult.Value, phoneNumber);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Failed to update user {UserId}: {Error}", request.UserId, updateResult.Error);
                return Result.Failure<UpdateUserResponseV1>(updateResult.Error);
            }

            // Save changes
            await _userWriteRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully updated user {UserId}", request.UserId);

            var response = new UpdateUserResponseV1 { User = user };
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", request.UserId);

            return Result.Failure<UpdateUserResponseV1>(
                Error.Failure(
                    code: "UpdateUser.Failed",
                    message: $"Failed to update user: {ex.Message}"));
        }
    }
}
