using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Repositories.Read;

namespace Shopilent.Application.Features.Payments.Queries.GetUserPaymentMethods.V1;

internal sealed class
    GetUserPaymentMethodsQueryHandlerV1 : IQueryHandler<GetUserPaymentMethodsQueryV1, IReadOnlyList<PaymentMethodDto>>
{
    private readonly IPaymentMethodReadRepository _paymentMethodReadRepository;
    private readonly ILogger<GetUserPaymentMethodsQueryHandlerV1> _logger;

    public GetUserPaymentMethodsQueryHandlerV1(
        IPaymentMethodReadRepository paymentMethodReadRepository,
        ILogger<GetUserPaymentMethodsQueryHandlerV1> logger)
    {
        _paymentMethodReadRepository = paymentMethodReadRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<PaymentMethodDto>>> Handle(
        GetUserPaymentMethodsQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var paymentMethods = await _paymentMethodReadRepository
                .GetByUserIdAsync(request.UserId, cancellationToken);

            _logger.LogInformation("Retrieved {Count} payment methods for user {UserId}",
                paymentMethods.Count, request.UserId);

            return Result.Success(paymentMethods);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment methods for user {UserId}", request.UserId);

            return Result.Failure<IReadOnlyList<PaymentMethodDto>>(
                Error.Failure(
                    code: "PaymentMethods.GetUserPaymentMethodsFailed",
                    message: $"Failed to retrieve payment methods: {ex.Message}"));
        }
    }
}
