using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.UpdateOrderStatus.V1;

internal sealed class
    UpdateOrderStatusCommandHandlerV1 : ICommandHandler<UpdateOrderStatusCommandV1, UpdateOrderStatusResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateOrderStatusCommandHandlerV1> _logger;

    public UpdateOrderStatusCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateOrderStatusCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateOrderStatusResponseV1>> Handle(UpdateOrderStatusCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get order by ID
            var order = await _orderWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (order == null)
            {
                return Result.Failure<UpdateOrderStatusResponseV1>(OrderErrors.NotFound(request.Id));
            }

            // Validate business rules based on status transition
            var validationResult = ValidateStatusTransition(order.Status, request.Status);
            if (validationResult.IsFailure)
            {
                return Result.Failure<UpdateOrderStatusResponseV1>(validationResult.Error);
            }

            // Update the order status
            var updateResult = order.UpdateOrderStatus(request.Status);
            if (updateResult.IsFailure)
            {
                return Result.Failure<UpdateOrderStatusResponseV1>(updateResult.Error);
            }

            // Add reason to metadata if provided
            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                var metadataKey = $"statusChange_{request.Status.ToString().ToLower()}_reason";
                var metadataResult = order.UpdateMetadata(metadataKey, request.Reason);
                if (metadataResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Failed to update order metadata with reason. Order ID: {OrderId}, Error: {Error}",
                        request.Id, metadataResult.Error.Message);
                }
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                order.SetAuditInfo(_currentUserContext.UserId);
            }

            await _orderWriteRepository.UpdateAsync(order, cancellationToken);

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Order status updated successfully. Order ID: {OrderId}, New Status: {Status}",
                order.Id, request.Status);

            // Create response
            var response = new UpdateOrderStatusResponseV1
            {
                Id = order.Id,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                UpdatedAt = DateTime.UtcNow
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status. Order ID: {OrderId}, Status: {Status}",
                request.Id, request.Status);

            return Result.Failure<UpdateOrderStatusResponseV1>(
                Error.Failure(
                    code: "Order.UpdateStatusFailed",
                    message: $"Failed to update order status: {ex.Message}"));
        }
    }

    private static Result ValidateStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        // Define valid status transitions
        var validTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = new[] { OrderStatus.Processing, OrderStatus.Cancelled },
            [OrderStatus.Processing] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
            [OrderStatus.Shipped] = new[] { OrderStatus.Delivered, OrderStatus.Cancelled },
            [OrderStatus.Delivered] = new OrderStatus[] { }, // Delivered is final state
            [OrderStatus.Cancelled] = new OrderStatus[] { } // Cancelled is final state
        };

        if (currentStatus == newStatus)
        {
            return Result.Failure(OrderErrors.InvalidOrderStatus($"Order is already {currentStatus}"));
        }

        if (!validTransitions.ContainsKey(currentStatus) ||
            !validTransitions[currentStatus].Contains(newStatus))
        {
            return Result.Failure(OrderErrors.InvalidOrderStatus(
                $"Cannot transition from {currentStatus} to {newStatus}"));
        }

        return Result.Success();
    }
}
