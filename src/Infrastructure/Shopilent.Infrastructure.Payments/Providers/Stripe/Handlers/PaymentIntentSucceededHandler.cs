using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class PaymentIntentSucceededHandler : IStripeWebhookHandler
{
    private readonly ILogger<PaymentIntentSucceededHandler> _logger;

    public PaymentIntentSucceededHandler(ILogger<PaymentIntentSucceededHandler> logger)
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
        result.PaymentStatus = PaymentStatus.Succeeded;
        result.CustomerId = paymentIntent.CustomerId;
        result.EventData.Add("amount", paymentIntent.Amount);
        result.EventData.Add("currency", paymentIntent.Currency);
        result.EventData.Add("payment_method", paymentIntent.PaymentMethodId);

        if (paymentIntent.Metadata != null)
        {
            result.EventData.Add("metadata", paymentIntent.Metadata);
        }

        result.OrderId = paymentIntent.Metadata["orderId"];
        result.ProcessingMessage = "Payment succeeded";
        result.IsProcessed = true;

        _logger.LogInformation("Payment succeeded: {PaymentIntentId}", paymentIntent.Id);

        return result;
    }
}