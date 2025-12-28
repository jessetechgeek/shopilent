using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;

namespace Shopilent.Application.Features.Payments.Commands.DeletePaymentMethod.V1;

internal sealed class DeletePaymentMethodCommandHandlerV1 : ICommandHandler<DeletePaymentMethodCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentReadRepository _paymentReadRepository;
    private readonly IPaymentMethodWriteRepository _paymentMethodWriteRepository;
    private readonly IPaymentMethodReadRepository _paymentMethodReadRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<DeletePaymentMethodCommandHandlerV1> _logger;

    public DeletePaymentMethodCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IPaymentReadRepository paymentReadRepository,
        IPaymentMethodWriteRepository paymentMethodWriteRepository,
        IPaymentMethodReadRepository paymentMethodReadRepository,
        ICurrentUserContext currentUserContext,
        ILogger<DeletePaymentMethodCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentReadRepository = paymentReadRepository;
        _paymentMethodWriteRepository = paymentMethodWriteRepository;
        _paymentMethodReadRepository = paymentMethodReadRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeletePaymentMethodCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get current user
            var currentUserId = _currentUserContext.UserId;
            if (currentUserId == Guid.Empty)
            {
                return Result.Failure(Error.Unauthorized(
                    code: "PaymentMethod.Unauthorized",
                    message: "User must be authenticated to delete payment methods."));
            }

            // Get payment method by ID
            var paymentMethod = await _paymentMethodWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (paymentMethod == null)
            {
                return Result.Failure(PaymentMethodErrors.NotFound(request.Id));
            }

            // Check if the payment method belongs to the current user
            if (paymentMethod.UserId != currentUserId)
            {
                return Result.Failure(Error.Forbidden(
                    code: "PaymentMethod.NotOwned",
                    message: "You can only delete your own payment methods."));
            }

            // Check if payment method is being used in any pending/processing payments
            var activePayments =
                await _paymentReadRepository.GetByPaymentMethodIdAsync(request.Id, cancellationToken);
            if (activePayments != null && activePayments.Any(p =>
                    p.Status == PaymentStatus.Pending ||
                    p.Status == PaymentStatus.Processing))
            {
                return Result.Failure(Error.Conflict(
                    code: "PaymentMethod.InUse",
                    message: "Cannot delete payment method that has pending or processing payments."));
            }

            // If this is the default payment method, check if user has other payment methods
            if (paymentMethod.IsDefault)
            {
                var userPaymentMethods =
                    await _paymentMethodReadRepository.GetByUserIdAsync(currentUserId.Value, cancellationToken);
                if (userPaymentMethods.Count > 1)
                {
                    // User has other payment methods, we should handle setting a new default
                    // For now, we'll prevent deletion of default payment method if there are others
                    return Result.Failure(Error.Validation(
                        code: "PaymentMethod.IsDefault",
                        message:
                        "Cannot delete default payment method. Please set another payment method as default first."));
                }
            }

            // Call domain method to delete payment method
            var deleteResult = paymentMethod.Delete();
            if (deleteResult.IsFailure)
            {
                return deleteResult;
            }

            // Delete from repository
            await _paymentMethodWriteRepository.DeleteAsync(paymentMethod, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payment method deleted successfully. ID: {PaymentMethodId}, User: {UserId}",
                paymentMethod.Id, currentUserId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting payment method with ID {PaymentMethodId}: {ErrorMessage}",
                request.Id, ex.Message);

            return Result.Failure(
                Error.Failure(
                    "PaymentMethod.DeleteFailed",
                    "Failed to delete payment method"));
        }
    }
}
