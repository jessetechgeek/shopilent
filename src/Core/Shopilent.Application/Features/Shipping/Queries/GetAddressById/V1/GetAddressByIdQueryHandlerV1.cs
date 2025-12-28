using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Errors;
using Shopilent.Domain.Shipping.Repositories.Read;

namespace Shopilent.Application.Features.Shipping.Queries.GetAddressById.V1;

internal sealed class GetAddressByIdQueryHandlerV1 : IQueryHandler<GetAddressByIdQueryV1, AddressDto>
{
    private readonly IAddressReadRepository _addressReadRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<GetAddressByIdQueryHandlerV1> _logger;

    public GetAddressByIdQueryHandlerV1(
        IAddressReadRepository addressReadRepository,
        ICurrentUserContext currentUserContext,
        ILogger<GetAddressByIdQueryHandlerV1> logger)
    {
        _addressReadRepository = addressReadRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<AddressDto>> Handle(GetAddressByIdQueryV1 request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting address with ID: {AddressId} for user: {UserId}",
                request.AddressId, _currentUserContext.UserId);

            var address = await _addressReadRepository.GetByIdAsync(request.AddressId, cancellationToken);

            if (address == null)
            {
                _logger.LogWarning("Address with ID: {AddressId} not found", request.AddressId);
                return Result.Failure<AddressDto>(AddressErrors.NotFound(request.AddressId));
            }

            // Ensure user can only access their own addresses
            if (address.UserId != _currentUserContext.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to access address {AddressId} belonging to user {OwnerId}",
                    _currentUserContext.UserId, request.AddressId, address.UserId);
                return Result.Failure<AddressDto>(AddressErrors.NotFound(request.AddressId));
            }

            _logger.LogInformation("Successfully retrieved address with ID: {AddressId}", request.AddressId);
            return Result.Success(address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting address with ID: {AddressId}", request.AddressId);
            return Result.Failure<AddressDto>(
                Error.Failure(
                    code: "Address.GetById.Failed",
                    message: $"Failed to retrieve address: {ex.Message}"));
        }
    }
}
