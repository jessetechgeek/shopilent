using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Payments.Repositories.Write;

namespace Shopilent.Application.Features.Payments.Commands.SetDefaultPaymentMethod.V1;

internal sealed class SetDefaultPaymentMethodCommandHandlerV1 :
    ICommandHandler<SetDefaultPaymentMethodCommandV1, SetDefaultPaymentMethodResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentMethodWriteRepository _paymentMethodWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<SetDefaultPaymentMethodCommandHandlerV1> _logger;

    public SetDefaultPaymentMethodCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IPaymentMethodWriteRepository paymentMethodWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<SetDefaultPaymentMethodCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentMethodWriteRepository = paymentMethodWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<SetDefaultPaymentMethodResponseV1>> Handle(
        SetDefaultPaymentMethodCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the payment method
            var paymentMethod =
                await _paymentMethodWriteRepository.GetByIdAsync(request.PaymentMethodId, cancellationToken);
            if (paymentMethod == null)
            {
                _logger.LogWarning("Payment method not found. ID: {PaymentMethodId}", request.PaymentMethodId);
                return Result.Failure<SetDefaultPaymentMethodResponseV1>(
                    PaymentMethodErrors.NotFound(request.PaymentMethodId));
            }

            // Verify ownership
            if (paymentMethod.UserId != request.UserId)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to set payment method {PaymentMethodId} as default, but it belongs to user {OwnerId}",
                    request.UserId, request.PaymentMethodId, paymentMethod.UserId);
                return Result.Failure<SetDefaultPaymentMethodResponseV1>(
                    PaymentMethodErrors.NotFound(request.PaymentMethodId));
            }

            // Check if payment method is active
            if (!paymentMethod.IsActive)
            {
                _logger.LogWarning("Attempted to set inactive payment method as default. ID: {PaymentMethodId}",
                    request.PaymentMethodId);
                return Result.Failure<SetDefaultPaymentMethodResponseV1>(
                    PaymentMethodErrors.InactivePaymentMethod);
            }

            // If already default, return success
            if (paymentMethod.IsDefault)
            {
                _logger.LogInformation("Payment method {PaymentMethodId} is already set as default for user {UserId}",
                    request.PaymentMethodId, request.UserId);

                return Result.Success(new SetDefaultPaymentMethodResponseV1
                {
                    PaymentMethodId = paymentMethod.Id,
                    IsDefault = true,
                    DisplayName = paymentMethod.DisplayName,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Get all user's payment methods to unset current default
            var userPaymentMethods =
                await _paymentMethodWriteRepository.GetByUserIdAsync(request.UserId, cancellationToken);

            // Unset current default payment method
            var currentDefault = userPaymentMethods.FirstOrDefault(pm => pm.IsDefault);
            if (currentDefault != null)
            {
                var unsetResult = currentDefault.SetDefault(false);
                if (unsetResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to unset current default payment method. ID: {PaymentMethodId}, Error: {Error}",
                        currentDefault.Id, unsetResult.Error.Message);
                    return Result.Failure<SetDefaultPaymentMethodResponseV1>(unsetResult.Error);
                }

                await _paymentMethodWriteRepository.UpdateAsync(currentDefault, cancellationToken);
            }

            // Set the new default
            var setDefaultResult = paymentMethod.SetDefault(true);
            if (setDefaultResult.IsFailure)
            {
                _logger.LogError("Failed to set payment method as default. ID: {PaymentMethodId}, Error: {Error}",
                    request.PaymentMethodId, setDefaultResult.Error.Message);
                return Result.Failure<SetDefaultPaymentMethodResponseV1>(setDefaultResult.Error);
            }

            // Update the payment method
            await _paymentMethodWriteRepository.UpdateAsync(paymentMethod, cancellationToken);

            // Save changes using Unit of Work
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payment method {PaymentMethodId} set as default for user {UserId}",
                request.PaymentMethodId, request.UserId);

            return Result.Success(new SetDefaultPaymentMethodResponseV1
            {
                PaymentMethodId = paymentMethod.Id,
                IsDefault = true,
                DisplayName = paymentMethod.DisplayName,
                UpdatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error setting payment method as default. PaymentMethodId: {PaymentMethodId}, UserId: {UserId}",
                request.PaymentMethodId, request.UserId);

            return Result.Failure<SetDefaultPaymentMethodResponseV1>(
                Error.Failure(
                    code: "PaymentMethod.SetDefaultFailed",
                    message: $"Failed to set payment method as default: {ex.Message}"));
        }
    }
}
