using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Repositories.Read;

namespace Shopilent.Application.Features.Shipping.Queries.GetUserAddresses.V1;

internal sealed class GetUserAddressesQueryHandlerV1 : IQueryHandler<GetUserAddressesQueryV1, IReadOnlyList<AddressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAddressReadRepository _addressReadRepository;
    private readonly ILogger<GetUserAddressesQueryHandlerV1> _logger;

    public GetUserAddressesQueryHandlerV1(
        IUnitOfWork unitOfWork,
        IAddressReadRepository addressReadRepository,
        ILogger<GetUserAddressesQueryHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _addressReadRepository = addressReadRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<AddressDto>>> Handle(
        GetUserAddressesQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if user exists
            var user = await _unitOfWork.UserReader.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} was not found", request.UserId);
                return Result.Failure<IReadOnlyList<AddressDto>>(
                    Error.NotFound(
                        code: "User.NotFound",
                        message: $"User with ID {request.UserId} was not found."));
            }

            var addresses = await _addressReadRepository.GetByUserIdAsync(request.UserId, cancellationToken);

            _logger.LogInformation("Retrieved {Count} addresses for user {UserId}", addresses.Count, request.UserId);
            return Result.Success(addresses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving addresses for user {UserId}", request.UserId);

            return Result.Failure<IReadOnlyList<AddressDto>>(
                Error.Failure(
                    code: "Addresses.GetUserAddressesFailed",
                    message: $"Failed to retrieve user addresses: {ex.Message}"));
        }
    }
}
