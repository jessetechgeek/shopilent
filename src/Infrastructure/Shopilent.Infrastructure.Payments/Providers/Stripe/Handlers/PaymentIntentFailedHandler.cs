using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class PaymentIntentFailedHandler : IStripeWebhookHandler
{
    private readonly ILogger<PaymentIntentFailedHandler> _logger;

    public PaymentIntentFailedHandler(ILogger<PaymentIntentFailedHandler> logger)
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
        result.PaymentStatus = PaymentStatus.Failed;
        result.CustomerId = paymentIntent.CustomerId;
        result.EventData.Add("last_payment_error", paymentIntent.LastPaymentError?.Message ?? "Unknown error");

        if (paymentIntent.Metadata != null)
        {
            result.EventData.Add("metadata", paymentIntent.Metadata);
        }

        result.OrderId = paymentIntent.Metadata["orderId"];
        result.ProcessingMessage = $"Payment failed: {paymentIntent.LastPaymentError?.Message ?? "Unknown error"}";
        result.IsProcessed = true;

        _logger.LogWarning("Payment failed: {PaymentIntentId}, Error: {Error}",
            paymentIntent.Id, paymentIntent.LastPaymentError?.Message);

        return result;
    }
}