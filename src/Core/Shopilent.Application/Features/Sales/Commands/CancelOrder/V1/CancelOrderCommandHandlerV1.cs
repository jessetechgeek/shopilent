using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.CancelOrder.V1;

internal sealed class CancelOrderCommandHandlerV1 : ICommandHandler<CancelOrderCommandV1, CancelOrderResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly ILogger<CancelOrderCommandHandlerV1> _logger;

    public CancelOrderCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        ILogger<CancelOrderCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _logger = logger;
    }

    public async Task<Result<CancelOrderResponseV1>> Handle(CancelOrderCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Attempting to cancel order {OrderId} by user {UserId}, IsAdmin: {IsAdmin}, IsManager: {IsManager}",
                request.OrderId, request.CurrentUserId, request.IsAdmin, request.IsManager);

            // Get the order
            var order = await _orderWriteRepository.GetByIdAsync(request.OrderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order with ID {OrderId} was not found", request.OrderId);
                return Result.Failure<CancelOrderResponseV1>(OrderErrors.NotFound(request.OrderId));
            }

            // Authorization check: Customers can only cancel their own orders, Admins/Managers can cancel any order
            if (!IsAuthorizedToCancelOrder(order, request.CurrentUserId, request.IsAdmin, request.IsManager))
            {
                _logger.LogWarning("User {UserId} attempted to cancel order {OrderId} belonging to user {OrderUserId}",
                    request.CurrentUserId, request.OrderId, order.UserId);

                return Result.Failure<CancelOrderResponseV1>(
                    Error.Forbidden("Order.CancelDenied", "You are not authorized to cancel this order"));
            }

            // Attempt to cancel the order using domain logic with role-based permissions
            var isAdminOrManager = request.IsAdmin || request.IsManager;
            var cancelResult = order.Cancel(request.Reason, isAdminOrManager);

            if (cancelResult.IsFailure)
            {
                _logger.LogWarning("Failed to cancel order {OrderId}: {ErrorMessage}",
                    request.OrderId, cancelResult.Error.Message);
                return Result.Failure<CancelOrderResponseV1>(cancelResult.Error);
            }

            // Update the order in the repository
            await _orderWriteRepository.UpdateAsync(order, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully cancelled order {OrderId}", request.OrderId);

            // Create response
            var response = new CancelOrderResponseV1
            {
                OrderId = order.Id, Status = order.Status, Reason = request.Reason, CancelledAt = DateTime.UtcNow
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}: {ErrorMessage}", request.OrderId, ex.Message);

            return Result.Failure<CancelOrderResponseV1>(
                Error.Failure(
                    code: "Order.CancelFailed",
                    message: $"Failed to cancel order: {ex.Message}"));
        }
    }

    private static bool IsAuthorizedToCancelOrder(Order order, Guid? currentUserId, bool isAdmin,
        bool isManager)
    {
        // If no user context, deny access
        if (!currentUserId.HasValue)
            return false;

        // Admins and Managers can cancel any order
        if (isAdmin || isManager)
            return true;

        // Regular users (customers) can only cancel their own orders
        return order.UserId == currentUserId;
    }
}
