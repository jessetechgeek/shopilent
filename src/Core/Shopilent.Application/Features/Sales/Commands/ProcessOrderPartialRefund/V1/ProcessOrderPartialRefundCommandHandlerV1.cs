using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.Features.Sales.Commands.ProcessOrderPartialRefund.V1;

internal sealed class ProcessOrderPartialRefundCommandHandlerV1
    : ICommandHandler<ProcessOrderPartialRefundCommandV1, ProcessOrderPartialRefundResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly ILogger<ProcessOrderPartialRefundCommandHandlerV1> _logger;

    public ProcessOrderPartialRefundCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        ILogger<ProcessOrderPartialRefundCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _logger = logger;
    }

    public async Task<Result<ProcessOrderPartialRefundResponseV1>> Handle(
        ProcessOrderPartialRefundCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the order from the repository
            var order = await _orderWriteRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order with ID {OrderId} was not found", request.OrderId);
                return Result.Failure<ProcessOrderPartialRefundResponseV1>(
                    OrderErrors.NotFound(request.OrderId));
            }

            // Create Money value object for the refund amount
            var refundAmount = Money.Create(request.Amount, request.Currency);
            if (refundAmount.IsFailure)
            {
                _logger.LogWarning("Invalid refund amount: {Amount} {Currency}", request.Amount, request.Currency);
                return Result.Failure<ProcessOrderPartialRefundResponseV1>(refundAmount.Error);
            }

            // Process the partial refund
            var result = order.ProcessPartialRefund(refundAmount.Value, request.Reason);
            if (result.IsFailure)
            {
                _logger.LogWarning("Failed to process partial refund for order {OrderId}: {Error}",
                    request.OrderId, result.Error.Message);
                return Result.Failure<ProcessOrderPartialRefundResponseV1>(result.Error);
            }

            // Save the updated order
            await _orderWriteRepository.UpdateAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed partial refund of {Amount} {Currency} for order {OrderId}",
                request.Amount, request.Currency, request.OrderId);

            // Create the response
            var response = new ProcessOrderPartialRefundResponseV1
            {
                OrderId = order.Id,
                RefundAmount = refundAmount.Value.Amount,
                Currency = refundAmount.Value.Currency,
                TotalRefundedAmount = order.RefundedAmount?.Amount ?? 0,
                RemainingAmount = order.Total.Amount - (order.RefundedAmount?.Amount ?? 0),
                Reason = request.Reason,
                RefundedAt = order.RefundedAt ?? DateTime.UtcNow,
                IsFullyRefunded = (order.RefundedAmount?.Amount ?? 0) >= order.Total.Amount
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing partial refund for order {OrderId}", request.OrderId);

            return Result.Failure<ProcessOrderPartialRefundResponseV1>(
                Error.Failure(
                    code: "Order.PartialRefundFailed",
                    message: $"Failed to process partial refund: {ex.Message}"));
        }
    }
}
