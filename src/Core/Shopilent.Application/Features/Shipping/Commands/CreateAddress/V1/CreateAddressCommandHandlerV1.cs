using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Repositories.Write;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Application.Features.Shipping.Commands.CreateAddress.V1;

internal sealed class CreateAddressCommandHandlerV1 : ICommandHandler<CreateAddressCommandV1, CreateAddressResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly IAddressWriteRepository _addressWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<CreateAddressCommandHandlerV1> _logger;

    public CreateAddressCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        IAddressWriteRepository addressWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<CreateAddressCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _addressWriteRepository = addressWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<CreateAddressResponseV1>> Handle(CreateAddressCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get current user ID
            if (!_currentUserContext.UserId.HasValue)
            {
                return Result.Failure<CreateAddressResponseV1>(
                    Error.Unauthorized("CreateAddress.Unauthorized", "User is not authenticated."));
            }

            var userId = _currentUserContext.UserId.Value;

            // Get user from repository
            var user = await _userWriteRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return Result.Failure<CreateAddressResponseV1>(
                    Error.NotFound("CreateAddress.UserNotFound", $"User with ID {userId} was not found."));
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
                return Result.Failure<CreateAddressResponseV1>(postalAddressResult.Error);
            }

            // Create phone number value object if provided
            PhoneNumber? phoneNumber = null;
            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                var phoneResult = PhoneNumber.Create(request.Phone);
                if (phoneResult.IsFailure)
                {
                    return Result.Failure<CreateAddressResponseV1>(phoneResult.Error);
                }

                phoneNumber = phoneResult.Value;
            }

            // If this is set as default, unset other default addresses of the same type
            if (request.IsDefault)
            {
                var existingAddresses = await _addressWriteRepository.GetByUserIdAsync(userId, cancellationToken);
                foreach (var existingAddress in existingAddresses.Where(a =>
                             a.AddressType == request.AddressType && a.IsDefault))
                {
                    var unsetResult = existingAddress.SetDefault(false);
                    if (unsetResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to unset default for address {AddressId}: {Error}",
                            existingAddress.Id, unsetResult.Error.Message);
                    }

                    await _addressWriteRepository.UpdateAsync(existingAddress, cancellationToken);
                }
            }

            // Create address using the appropriate factory method based on address type
            Result<Address> addressResult = request.AddressType switch
            {
                AddressType.Shipping => Address.CreateShipping(
                    user, postalAddressResult.Value, phoneNumber, request.IsDefault),
                AddressType.Billing => Address.CreateBilling(
                    user, postalAddressResult.Value, phoneNumber, request.IsDefault),
                AddressType.Both => Address.CreateBoth(
                    user, postalAddressResult.Value, phoneNumber, request.IsDefault),
                _ => Result.Failure<Address>(
                    Error.Validation("CreateAddress.InvalidAddressType", "Invalid address type specified."))
            };

            if (addressResult.IsFailure)
            {
                return Result.Failure<CreateAddressResponseV1>(addressResult.Error);
            }

            // Save the address
            var savedAddress = await _addressWriteRepository.AddAsync(addressResult.Value, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);


            _logger.LogInformation("Address created successfully with ID: {AddressId} for user: {UserId}",
                savedAddress.Id, userId);

            // Create response
            var response = new CreateAddressResponseV1
            {
                Id = savedAddress.Id,
                UserId = savedAddress.UserId,
                AddressLine1 = savedAddress.AddressLine1,
                AddressLine2 = savedAddress.AddressLine2,
                City = savedAddress.City,
                State = savedAddress.State,
                PostalCode = savedAddress.PostalCode,
                Country = savedAddress.Country,
                Phone = savedAddress.Phone?.Value,
                AddressType = savedAddress.AddressType,
                IsDefault = savedAddress.IsDefault,
                CreatedAt = savedAddress.CreatedAt
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating address for user: {UserId}", _currentUserContext.UserId);

            return Result.Failure<CreateAddressResponseV1>(
                Error.Failure("CreateAddress.Failed", $"An error occurred while creating the address: {ex.Message}"));
        }
    }
}
