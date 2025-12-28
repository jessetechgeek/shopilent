using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Payments.Repositories.Read;

namespace Shopilent.Application.Features.Payments.Queries.GetPaymentMethod.V1;

internal sealed class GetPaymentMethodQueryHandlerV1 : IQueryHandler<GetPaymentMethodQueryV1, PaymentMethodDto>
{
    private readonly IPaymentMethodReadRepository _paymentMethodReadRepository;
    private readonly ILogger<GetPaymentMethodQueryHandlerV1> _logger;

    public GetPaymentMethodQueryHandlerV1(
        IPaymentMethodReadRepository paymentMethodReadRepository,
        ILogger<GetPaymentMethodQueryHandlerV1> logger)
    {
        _paymentMethodReadRepository = paymentMethodReadRepository;
        _logger = logger;
    }

    public async Task<Result<PaymentMethodDto>> Handle(GetPaymentMethodQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var paymentMethod = await _paymentMethodReadRepository.GetByIdAsync(request.Id, cancellationToken);

            if (paymentMethod == null)
            {
                _logger.LogWarning("Payment method with ID {PaymentMethodId} was not found", request.Id);
                return Result.Failure<PaymentMethodDto>(PaymentMethodErrors.NotFound(request.Id));
            }

            // Check if the payment method belongs to the requesting user
            if (paymentMethod.UserId != request.UserId)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to access payment method {PaymentMethodId} belonging to user {OwnerId}",
                    request.UserId, request.Id, paymentMethod.UserId);
                return Result.Failure<PaymentMethodDto>(
                    Error.Forbidden(
                        code: "PaymentMethod.AccessDenied",
                        message: "You do not have permission to access this payment method"));
            }

            _logger.LogInformation("Retrieved payment method with ID {PaymentMethodId} for user {UserId}", request.Id,
                request.UserId);
            return Result.Success(paymentMethod);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment method with ID {PaymentMethodId} for user {UserId}",
                request.Id, request.UserId);

            return Result.Failure<PaymentMethodDto>(
                Error.Failure(
                    code: "PaymentMethod.GetFailed",
                    message: $"Failed to retrieve payment method: {ex.Message}"));
        }
    }
}
