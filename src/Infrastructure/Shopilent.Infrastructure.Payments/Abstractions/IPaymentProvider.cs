using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Infrastructure.Payments.Models;

namespace Shopilent.Infrastructure.Payments.Abstractions;

public interface IPaymentProvider
{
    PaymentProvider Provider { get; }

    Task<Result<PaymentResult>> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<string>> RefundPaymentAsync(
        string transactionId,
        Money amount = null,
        string reason = null,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentStatus>> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    // Customer management methods - optional for providers that support it
    Task<Result<string>> GetOrCreateCustomerAsync(
        string userId,
        string email,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<string>(
            Error.Failure(
                code: "CustomerManagement.NotSupported",
                message: $"Customer management is not supported by {Provider} provider")));
    }

    Task<Result<string>> AttachPaymentMethodToCustomerAsync(
        string paymentMethodToken,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<string>(
            Error.Failure(
                code: "CustomerManagement.NotSupported",
                message: $"Payment method attachment is not supported by {Provider} provider")));
    }

    // Webhook processing method - optional for providers that support it
    Task<Result<WebhookResult>> ProcessWebhookAsync(
        string webhookPayload,
        string signature = null,
        Dictionary<string, string> headers = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<WebhookResult>(
            Error.Failure(
                code: "Webhook.NotSupported",
                message: $"Webhook processing is not supported by {Provider} provider")));
    }

    // Setup intent methods - optional for providers that support it
    Task<Result<SetupIntentResult>> CreateSetupIntentAsync(
        string customerId,
        string paymentMethodToken = null,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<SetupIntentResult>(
            Error.Failure(
                code: "SetupIntent.NotSupported",
                message: $"Setup intent is not supported by {Provider} provider")));
    }

    Task<Result<SetupIntentResult>> ConfirmSetupIntentAsync(
        string setupIntentId,
        string paymentMethodToken = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<SetupIntentResult>(
            Error.Failure(
                code: "SetupIntent.NotSupported",
                message: $"Setup intent confirmation is not supported by {Provider} provider")));
    }
}
