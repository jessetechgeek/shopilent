using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Shipping.Errors;
using Shopilent.Domain.Shipping.Repositories.Write;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Application.Features.Shipping.Commands.UpdateAddress.V1;

internal sealed class UpdateAddressCommandHandlerV1 : ICommandHandler<UpdateAddressCommandV1, UpdateAddressResponseV1>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAddressWriteRepository _addressWriteRepository;
    private readonly ILogger<UpdateAddressCommandHandlerV1> _logger;

    public UpdateAddressCommandHandlerV1(
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        IAddressWriteRepository addressWriteRepository,
        ILogger<UpdateAddressCommandHandlerV1> logger)
    {
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _addressWriteRepository = addressWriteRepository;
        _logger = logger;
    }

    public async Task<Result<UpdateAddressResponseV1>> Handle(
        UpdateAddressCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating address {AddressId} for user {UserId}",
                request.Id, _currentUserContext.UserId);

            // Get the address
            var address = await _addressWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (address == null)
            {
                _logger.LogWarning("Address {AddressId} not found", request.Id);
                return Result.Failure<UpdateAddressResponseV1>(AddressErrors.NotFound(request.Id));
            }

            // Verify ownership
            if (address.UserId != _currentUserContext.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to update address {AddressId} owned by {OwnerId}",
                    _currentUserContext.UserId, request.Id, address.UserId);
                return Result.Failure<UpdateAddressResponseV1>(AddressErrors.NotFound(request.Id));
            }

            // Create postal address value object
            var postalAddressResult = PostalAddress.Create(
                request.AddressLine1,
                request.City,
                request.State,
                request.Country,
                request.PostalCode,
                request.AddressLine2);

            if (postalAddressResult.IsFailure)
            {
                _logger.LogWarning("Invalid postal address data for address {AddressId}: {Error}",
                    request.Id, postalAddressResult.Error.Message);
                return Result.Failure<UpdateAddressResponseV1>(postalAddressResult.Error);
            }

            // Create phone number value object if provided
            PhoneNumber phoneNumber = null;
            if (!string.IsNullOrEmpty(request.Phone))
            {
                var phoneResult = PhoneNumber.Create(request.Phone);
                if (phoneResult.IsFailure)
                {
                    _logger.LogWarning("Invalid phone number for address {AddressId}: {Error}",
                        request.Id, phoneResult.Error.Message);
                    return Result.Failure<UpdateAddressResponseV1>(phoneResult.Error);
                }

                phoneNumber = phoneResult.Value;
            }

            // Update the address
            var updateResult = address.Update(postalAddressResult.Value, phoneNumber);
            if (updateResult.IsFailure)
            {
                _logger.LogWarning("Failed to update address {AddressId}: {Error}",
                    request.Id, updateResult.Error.Message);
                return Result.Failure<UpdateAddressResponseV1>(updateResult.Error);
            }

            // Update address type if different
            if (address.AddressType != request.AddressType)
            {
                var addressTypeResult = address.SetAddressType(request.AddressType);
                if (addressTypeResult.IsFailure)
                {
                    _logger.LogWarning("Failed to update address type for {AddressId}: {Error}",
                        request.Id, addressTypeResult.Error.Message);
                    return Result.Failure<UpdateAddressResponseV1>(addressTypeResult.Error);
                }
            }

            // Save changes
            await _addressWriteRepository.UpdateAsync(address, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully updated address {AddressId} for user {UserId}",
                request.Id, _currentUserContext.UserId);

            // Return response
            var response = new UpdateAddressResponseV1
            {
                Id = address.Id,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                State = address.State,
                PostalCode = address.PostalCode,
                Country = address.Country,
                Phone = address.Phone?.Value,
                AddressType = address.AddressType,
                IsDefault = address.IsDefault,
                UpdatedAt = DateTime.UtcNow
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating address {AddressId} for user {UserId}",
                request.Id, _currentUserContext.UserId);

            return Result.Failure<UpdateAddressResponseV1>(
                Error.Failure(
                    code: "Address.UpdateFailed",
                    message: $"Failed to update address: {ex.Message}"));
        }
    }
}
