using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class SetupIntentCanceledHandler : IStripeWebhookHandler
{
    private readonly ILogger<SetupIntentCanceledHandler> _logger;

    public SetupIntentCanceledHandler(ILogger<SetupIntentCanceledHandler> logger)
    {
        _logger = logger;
    }

    public async Task<WebhookResult> HandleAsync(Event stripeEvent, WebhookResult result, CancellationToken cancellationToken)
    {
        var setupIntent = stripeEvent.Data.Object as SetupIntent;
        if (setupIntent == null)
        {
            result.ProcessingMessage = "Invalid SetupIntent data in webhook";
            return result;
        }

        result.TransactionId = setupIntent.Id;
        result.PaymentStatus = PaymentStatus.Canceled;
        result.CustomerId = setupIntent.CustomerId;
        result.EventData.Add("payment_method_id", setupIntent.PaymentMethodId ?? string.Empty);
        result.EventData.Add("usage", setupIntent.Usage ?? string.Empty);

        // Add cancellation reason if available
        if (setupIntent.CancellationReason != null)
        {
            result.EventData.Add("cancellation_reason", setupIntent.CancellationReason);
        }

        if (setupIntent.Metadata != null)
        {
            result.EventData.Add("metadata", setupIntent.Metadata);
        }

        result.OrderId = setupIntent.Metadata?.GetValueOrDefault("orderId");
        result.ProcessingMessage = $"Setup intent canceled: {setupIntent.CancellationReason ?? "Unknown reason"}";
        result.IsProcessed = true;

        _logger.LogWarning("Setup intent canceled: {SetupIntentId} for customer {CustomerId}, reason: {CancellationReason}",
            setupIntent.Id, setupIntent.CustomerId, setupIntent.CancellationReason);

        return result;
    }
}