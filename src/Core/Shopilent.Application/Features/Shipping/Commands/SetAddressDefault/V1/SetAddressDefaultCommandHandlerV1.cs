using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Errors;
using Shopilent.Domain.Shipping.Repositories.Read;
using Shopilent.Domain.Shipping.Repositories.Write;

namespace Shopilent.Application.Features.Shipping.Commands.SetAddressDefault.V1;

internal sealed class SetAddressDefaultCommandHandlerV1 : ICommandHandler<SetAddressDefaultCommandV1, AddressDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAddressWriteRepository _addressWriteRepository;
    private readonly IAddressReadRepository _addressReadRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<SetAddressDefaultCommandHandlerV1> _logger;

    public SetAddressDefaultCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IAddressWriteRepository addressWriteRepository,
        IAddressReadRepository addressReadRepository,
        ICurrentUserContext currentUserContext,
        ILogger<SetAddressDefaultCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _addressWriteRepository = addressWriteRepository;
        _addressReadRepository = addressReadRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<AddressDto>> Handle(SetAddressDefaultCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if user is authenticated
            if (!_currentUserContext.UserId.HasValue)
            {
                _logger.LogWarning("Unauthenticated user attempted to set address as default");
                return Result.Failure<AddressDto>(
                    Error.Unauthorized(
                        code: "Address.Unauthorized",
                        message: "User must be authenticated to set address as default"));
            }

            var userId = _currentUserContext.UserId.Value;

            // Get the address to set as default
            var address = await _addressWriteRepository.GetByIdAsync(request.AddressId, cancellationToken);
            if (address == null)
            {
                _logger.LogWarning("Address with ID {AddressId} not found", request.AddressId);
                return Result.Failure<AddressDto>(AddressErrors.NotFound(request.AddressId));
            }

            // Verify the address belongs to the current user
            if (address.UserId != userId)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to set address {AddressId} as default but it belongs to another user",
                    userId, request.AddressId);
                return Result.Failure<AddressDto>(
                    Error.Forbidden(
                        code: "Address.NotOwned",
                        message: "You can only set your own addresses as default"));
            }

            // If address is already default, return it as-is
            if (address.IsDefault)
            {
                _logger.LogInformation("Address {AddressId} is already set as default for user {UserId}",
                    request.AddressId, userId);

                var currentDefaultDto =
                    await _addressReadRepository.GetByIdAsync(request.AddressId, cancellationToken);
                return Result.Success(currentDefaultDto);
            }

            // Get all addresses of the same type for this user to unset any existing defaults
            var userAddresses = await _addressWriteRepository.GetByUserIdAsync(userId, cancellationToken);
            var addressesOfSameType = userAddresses
                .Where(a => a.AddressType == address.AddressType ||
                            address.AddressType == Domain.Shipping.Enums.AddressType.Both)
                .ToList();

            // Unset any existing default addresses of the same type
            foreach (var existingAddress in addressesOfSameType.Where(a => a.IsDefault && a.Id != address.Id))
            {
                var unsetResult = existingAddress.SetDefault(false);
                if (unsetResult.IsFailure)
                {
                    _logger.LogError("Failed to unset default for address {AddressId}: {Error}",
                        existingAddress.Id, unsetResult.Error.Message);
                    return Result.Failure<AddressDto>(unsetResult.Error);
                }

                await _addressWriteRepository.UpdateAsync(existingAddress, cancellationToken);
            }

            // Set the new address as default
            var setDefaultResult = address.SetDefault(true);
            if (setDefaultResult.IsFailure)
            {
                _logger.LogError("Failed to set address {AddressId} as default: {Error}",
                    request.AddressId, setDefaultResult.Error.Message);
                return Result.Failure<AddressDto>(setDefaultResult.Error);
            }

            await _addressWriteRepository.UpdateAsync(address, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Return the updated address DTO
            var updatedAddressDto = await _addressReadRepository.GetByIdAsync(request.AddressId, cancellationToken);

            _logger.LogInformation("Successfully set address {AddressId} as default for user {UserId}",
                request.AddressId, userId);

            return Result.Success(updatedAddressDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting address {AddressId} as default", request.AddressId);

            return Result.Failure<AddressDto>(
                Error.Failure(
                    code: "Address.SetDefaultFailed",
                    message: $"Failed to set address as default: {ex.Message}"));
        }
    }
}
