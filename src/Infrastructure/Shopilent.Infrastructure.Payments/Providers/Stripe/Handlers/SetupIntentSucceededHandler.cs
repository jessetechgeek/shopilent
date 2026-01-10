using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class SetupIntentSucceededHandler : IStripeWebhookHandler
{
    private readonly ILogger<SetupIntentSucceededHandler> _logger;

    public SetupIntentSucceededHandler(ILogger<SetupIntentSucceededHandler> logger)
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
        result.PaymentStatus = PaymentStatus.Succeeded;
        result.CustomerId = setupIntent.CustomerId;
        result.EventData.Add("payment_method_id", setupIntent.PaymentMethodId ?? string.Empty);
        result.EventData.Add("usage", setupIntent.Usage ?? string.Empty);

        if (setupIntent.Metadata != null)
        {
            result.EventData.Add("metadata", setupIntent.Metadata);
        }

        // For setup intents, we don't have an order ID, but we can store the customer ID
        result.OrderId = setupIntent.Metadata?.GetValueOrDefault("orderId");
        result.ProcessingMessage = "Setup intent succeeded - payment method set up for future use";
        result.IsProcessed = true;

        _logger.LogInformation("Setup intent succeeded: {SetupIntentId} for customer {CustomerId}, payment method: {PaymentMethodId}",
            setupIntent.Id, setupIntent.CustomerId, setupIntent.PaymentMethodId);

        return result;
    }
}