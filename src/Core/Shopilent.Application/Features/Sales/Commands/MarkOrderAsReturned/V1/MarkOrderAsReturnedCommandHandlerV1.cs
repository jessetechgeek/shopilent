using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.MarkOrderAsReturned.V1;

internal sealed class MarkOrderAsReturnedCommandHandlerV1
    : ICommandHandler<MarkOrderAsReturnedCommandV1, MarkOrderAsReturnedResponseV1>
{
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkOrderAsReturnedCommandHandlerV1> _logger;

    public MarkOrderAsReturnedCommandHandlerV1(
        IOrderWriteRepository orderWriteRepository,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<MarkOrderAsReturnedCommandHandlerV1> logger)
    {
        _orderWriteRepository = orderWriteRepository;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<MarkOrderAsReturnedResponseV1>> Handle(
        MarkOrderAsReturnedCommandV1 request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Marking order {OrderId} as returned by user {UserId}. Reason: {Reason}",
            request.OrderId, _currentUserContext.UserId, request.ReturnReason);

        // Get the order
        var order = await _orderWriteRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", request.OrderId);
            return Result.Failure<MarkOrderAsReturnedResponseV1>(OrderErrors.NotFound(request.OrderId));
        }

        // Authorization: User must own the order OR be admin/manager
        var isAdmin = _currentUserContext.IsInRole("Admin");
        var isManager = _currentUserContext.IsInRole("Manager");

        if (order.UserId != _currentUserContext.UserId && !isAdmin && !isManager)
        {
            _logger.LogWarning(
                "User {UserId} attempted to mark order {OrderId} as returned without authorization",
                _currentUserContext.UserId, request.OrderId);
            return Result.Failure<MarkOrderAsReturnedResponseV1>(OrderErrors.AccessDenied);
        }

        // Mark the order as returned
        var markReturnedResult = order.MarkAsReturned(request.ReturnReason);
        if (markReturnedResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to mark order {OrderId} as returned: {Error}",
                request.OrderId, markReturnedResult.Error.Message);
            return Result.Failure<MarkOrderAsReturnedResponseV1>(markReturnedResult.Error);
        }

        // Update the order in repository
        await _orderWriteRepository.UpdateAsync(order, cancellationToken);

        // Commit the transaction
        await _unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully marked order {OrderId} as returned. Status: {Status}",
            order.Id, order.Status);

        // Extract returnedAt from metadata
        var returnedAt = order.Metadata.ContainsKey("returnedAt")
            ? (DateTime)order.Metadata["returnedAt"]
            : DateTime.UtcNow;

        return Result.Success(new MarkOrderAsReturnedResponseV1
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            ReturnReason = request.ReturnReason,
            ReturnedAt = returnedAt
        });
    }
}
