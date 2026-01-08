using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Infrastructure.Payments.Abstractions;
using Shopilent.Infrastructure.Payments.Models;

namespace Shopilent.Infrastructure.Payments.Providers.Base;

public abstract class PaymentProviderBase : IPaymentProvider
{
    protected readonly ILogger Logger;

    protected PaymentProviderBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract PaymentProvider Provider { get; }

    public abstract Task<Result<PaymentResult>> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);

    public abstract Task<Result<string>> RefundPaymentAsync(
        string transactionId,
        Money amount = null,
        string reason = null,
        CancellationToken cancellationToken = default);

    public abstract Task<Result<PaymentStatus>> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    public virtual Task<Result<string>> GetOrCreateCustomerAsync(
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

    public virtual Task<Result<string>> AttachPaymentMethodToCustomerAsync(
        string paymentMethodToken,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<string>(
            Error.Failure(
                code: "CustomerManagement.NotSupported",
                message: $"Payment method attachment is not supported by {Provider} provider")));
    }

    public virtual Task<Result<WebhookResult>> ProcessWebhookAsync(
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

    public virtual Task<Result<SetupIntentResult>> CreateSetupIntentAsync(
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

    public virtual Task<Result<SetupIntentResult>> ConfirmSetupIntentAsync(
        string setupIntentId,
        string paymentMethodToken = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure<SetupIntentResult>(
            Error.Failure(
                code: "SetupIntent.NotSupported",
                message: $"Setup intent confirmation is not supported by {Provider} provider")));
    }

    protected virtual void LogPaymentOperation(string operation, string transactionId, PaymentStatus? status = null)
    {
        Logger.LogInformation("Payment operation {Operation} for transaction {TransactionId} with status {Status}",
            operation, transactionId, status);
    }

    protected virtual void LogPaymentError(string operation, string transactionId, Exception exception)
    {
        Logger.LogError(exception, "Payment operation {Operation} failed for transaction {TransactionId}",
            operation, transactionId);
    }
}
