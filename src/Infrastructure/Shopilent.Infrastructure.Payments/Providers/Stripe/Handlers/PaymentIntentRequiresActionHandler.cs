using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class PaymentIntentRequiresActionHandler : IStripeWebhookHandler
{
    private readonly ILogger<PaymentIntentRequiresActionHandler> _logger;

    public PaymentIntentRequiresActionHandler(ILogger<PaymentIntentRequiresActionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<WebhookResult> HandleAsync(Event stripeEvent, WebhookResult result, CancellationToken cancellationToken)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null)
        {
            result.ProcessingMessage = "Invalid PaymentIntent data in webhook";
            return result;
        }

        result.TransactionId = paymentIntent.Id;
        result.PaymentStatus = PaymentStatus.RequiresAction;
        result.CustomerId = paymentIntent.CustomerId;
        result.EventData.Add("next_action", paymentIntent.NextAction?.Type ?? "unknown");

        if (paymentIntent.Metadata != null)
        {
            result.EventData.Add("metadata", paymentIntent.Metadata);
        }

        result.OrderId = paymentIntent.Metadata["orderId"];
        result.ProcessingMessage = "Payment requires additional action";
        result.IsProcessed = true;

        _logger.LogInformation("Payment requires action: {PaymentIntentId}", paymentIntent.Id);

        return result;
    }
}