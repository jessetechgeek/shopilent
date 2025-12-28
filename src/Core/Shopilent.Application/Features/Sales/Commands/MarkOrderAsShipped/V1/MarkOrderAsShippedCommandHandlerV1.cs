using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.MarkOrderAsShipped.V1;

internal sealed class MarkOrderAsShippedCommandHandlerV1 : ICommandHandler<MarkOrderAsShippedCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<MarkOrderAsShippedCommandHandlerV1> _logger;

    public MarkOrderAsShippedCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<MarkOrderAsShippedCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(MarkOrderAsShippedCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get order by ID
            var order = await _orderWriteRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order not found. OrderId: {OrderId}", request.OrderId);
                return Result.Failure(OrderErrors.NotFound(request.OrderId));
            }

            // Mark order as shipped
            var result = order.MarkAsShipped(request.TrackingNumber);
            if (result.IsFailure)
            {
                _logger.LogWarning("Failed to mark order as shipped. OrderId: {OrderId}, Error: {Error}",
                    request.OrderId, result.Error);
                return result;
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                order.SetAuditInfo(_currentUserContext.UserId);
            }

            await _orderWriteRepository.UpdateAsync(order, cancellationToken);
            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order marked as shipped successfully. OrderId: {OrderId}, TrackingNumber: {TrackingNumber}",
                request.OrderId, request.TrackingNumber);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking order as shipped. OrderId: {OrderId}, TrackingNumber: {TrackingNumber}",
                request.OrderId, request.TrackingNumber);

            return Result.Failure(
                Error.Failure(
                    code: "Order.MarkAsShippedFailed",
                    message: $"Failed to mark order as shipped: {ex.Message}"));
        }
    }
}
