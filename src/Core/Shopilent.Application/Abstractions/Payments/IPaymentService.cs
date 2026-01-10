using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Application.Abstractions.Payments;

public interface IPaymentService
{
    Task<Result<PaymentResult>> ProcessPaymentAsync(
        Money amount,
        PaymentMethodType methodType,
        PaymentProvider provider,
        string paymentMethodToken,
        string customerId = null,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default);

    Task<Result<string>> RefundPaymentAsync(
        string transactionId,
        Money amount = null,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentStatus>> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    // Customer management methods
    Task<Result<string>> GetOrCreateCustomerAsync(
        PaymentProvider provider,
        string userId,
        string email,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default);

    Task<Result<string>> AttachPaymentMethodToCustomerAsync(
        PaymentProvider provider,
        string paymentMethodToken,
        string customerId,
        CancellationToken cancellationToken = default);

    // Webhook processing method
    Task<Result<WebhookResult>> ProcessWebhookAsync(
        PaymentProvider provider,
        string webhookPayload,
        string signature = null,
        Dictionary<string, string> headers = null,
        CancellationToken cancellationToken = default);

    // Setup intent methods
    Task<Result<SetupIntentResult>> CreateSetupIntentAsync(
        PaymentProvider provider,
        string customerId,
        string paymentMethodToken = null,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default);

    Task<Result<SetupIntentResult>> ConfirmSetupIntentAsync(
        PaymentProvider provider,
        string setupIntentId,
        string paymentMethodToken = null,
        CancellationToken cancellationToken = default);
}
