using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.ProcessOrderRefund.V1;

internal sealed class
    ProcessOrderRefundCommandHandlerV1 : ICommandHandler<ProcessOrderRefundCommandV1, ProcessOrderRefundResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ProcessOrderRefundCommandHandlerV1> _logger;

    public ProcessOrderRefundCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<ProcessOrderRefundCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<ProcessOrderRefundResponseV1>> Handle(ProcessOrderRefundCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get order by ID
            var order = await _orderWriteRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order with ID {OrderId} not found", request.OrderId);
                return Result.Failure<ProcessOrderRefundResponseV1>(OrderErrors.NotFound(request.OrderId));
            }

            // Process the refund
            var refundResult = order.ProcessRefund(request.Reason);
            if (refundResult.IsFailure)
            {
                _logger.LogWarning("Failed to process refund for order {OrderId}: {Error}",
                    request.OrderId, refundResult.Error.Message);
                return Result.Failure<ProcessOrderRefundResponseV1>(refundResult.Error);
            }

            // Update the order in the database
            await _orderWriteRepository.UpdateAsync(order, cancellationToken);

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully processed full refund for order {OrderId} by user {UserId}",
                request.OrderId, _currentUserContext.UserId);

            // Create response
            var response = new ProcessOrderRefundResponseV1
            {
                OrderId = order.Id,
                RefundAmount = order.RefundedAmount.Amount,
                Currency = order.RefundedAmount.Currency,
                Reason = order.RefundReason,
                RefundedAt = order.RefundedAt.Value,
                Status = "Refunded"
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for order {OrderId}", request.OrderId);

            return Result.Failure<ProcessOrderRefundResponseV1>(
                Error.Failure(
                    code: "OrderRefund.ProcessingFailed",
                    message: $"Failed to process refund: {ex.Message}"));
        }
    }
}
