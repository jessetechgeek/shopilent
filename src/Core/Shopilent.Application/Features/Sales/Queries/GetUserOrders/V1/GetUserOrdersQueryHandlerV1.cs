using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Sales.DTOs;

namespace Shopilent.Application.Features.Sales.Queries.GetUserOrders.V1;

internal sealed class GetUserOrdersQueryHandlerV1 : IQueryHandler<GetUserOrdersQueryV1, IReadOnlyList<OrderDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<GetUserOrdersQueryHandlerV1> _logger;

    public GetUserOrdersQueryHandlerV1(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        ILogger<GetUserOrdersQueryHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userReadRepository = userReadRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<OrderDto>>> Handle(
        GetUserOrdersQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate that the user exists
            var currentUser = await _userReadRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (currentUser == null)
            {
                _logger.LogWarning("User with ID {UserId} was not found", request.UserId);
                return Result.Failure<IReadOnlyList<OrderDto>>(UserErrors.NotFound(request.UserId));
            }

            var orders = await _unitOfWork.OrderReader.GetByUserIdAsync(request.UserId, cancellationToken);

            _logger.LogInformation("Retrieved {Count} orders for user {UserId}", orders.Count, request.UserId);
            return Result.Success(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for user {UserId}", request.UserId);

            return Result.Failure<IReadOnlyList<OrderDto>>(
                Error.Failure(
                    code: "Orders.GetUserOrdersFailed",
                    message: $"Failed to retrieve user orders: {ex.Message}"));
        }
    }
}
