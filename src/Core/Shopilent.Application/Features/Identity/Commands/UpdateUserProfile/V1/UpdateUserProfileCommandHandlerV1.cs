using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Application.Features.Identity.Commands.UpdateUserProfile.V1;

internal sealed class
    UpdateUserProfileCommandHandlerV1 : ICommandHandler<UpdateUserProfileCommandV1, UpdateUserProfileResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateUserProfileCommandHandlerV1> _logger;

    public UpdateUserProfileCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateUserProfileCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateUserProfileResponseV1>> Handle(UpdateUserProfileCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get user by ID
            var user = await _userWriteRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return Result.Failure<UpdateUserProfileResponseV1>(UserErrors.NotFound(request.UserId));
            }

            // Create value objects
            var fullNameResult = FullName.Create(request.FirstName, request.LastName, request.MiddleName);
            if (fullNameResult.IsFailure)
            {
                return Result.Failure<UpdateUserProfileResponseV1>(fullNameResult.Error);
            }

            PhoneNumber phoneNumber = null;
            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                var phoneResult = PhoneNumber.Create(request.Phone);
                if (phoneResult.IsFailure)
                {
                    return Result.Failure<UpdateUserProfileResponseV1>(phoneResult.Error);
                }

                phoneNumber = phoneResult.Value;
            }

            // Update user personal info
            var updateResult = user.UpdatePersonalInfo(fullNameResult.Value, phoneNumber);
            if (updateResult.IsFailure)
            {
                return Result.Failure<UpdateUserProfileResponseV1>(updateResult.Error);
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                user.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("User profile updated successfully. UserId: {UserId}", user.Id);

            // Return response
            var response = new UpdateUserProfileResponseV1
            {
                Id = user.Id,
                FirstName = user.FullName.FirstName,
                LastName = user.FullName.LastName,
                MiddleName = user.FullName.MiddleName,
                Phone = user.Phone?.Value,
                UpdatedAt = DateTime.UtcNow
            };

            return Result.Success(response);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while updating user profile. UserId: {UserId}",
                request.UserId);

            return Result.Failure<UpdateUserProfileResponseV1>(ex.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile. UserId: {UserId}", request.UserId);

            return Result.Failure<UpdateUserProfileResponseV1>(
                Error.Failure(
                    code: "User.UpdateProfileFailed",
                    message: $"Failed to update user profile: {ex.Message}"));
        }
    }
}
