using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.MarkOrderAsDelivered.V1;

internal sealed class MarkOrderAsDeliveredCommandHandlerV1 : ICommandHandler<MarkOrderAsDeliveredCommandV1,
    MarkOrderAsDeliveredResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<MarkOrderAsDeliveredCommandHandlerV1> _logger;

    public MarkOrderAsDeliveredCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<MarkOrderAsDeliveredCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<MarkOrderAsDeliveredResponseV1>> Handle(MarkOrderAsDeliveredCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get order by ID
            var order = await _orderWriteRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                return Result.Failure<MarkOrderAsDeliveredResponseV1>(OrderErrors.NotFound(request.OrderId));
            }

            // Mark order as delivered
            var deliveredResult = order.MarkAsDelivered();
            if (deliveredResult.IsFailure)
            {
                return Result.Failure<MarkOrderAsDeliveredResponseV1>(deliveredResult.Error);
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                order.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} marked as delivered successfully by user {UserId}",
                request.OrderId, _currentUserContext.UserId);

            // Return success response
            return Result.Success(new MarkOrderAsDeliveredResponseV1
            {
                Id = order.Id,
                Status = order.Status,
                UpdatedAt = DateTime.UtcNow,
                Message = "Order marked as delivered successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while marking order {OrderId} as delivered", request.OrderId);
            return Result.Failure<MarkOrderAsDeliveredResponseV1>(
                Error.Failure(
                    code: "MarkOrderAsDelivered.Failed",
                    message: $"Order delivery marking failed: {ex.Message}"));
        }
    }
}
