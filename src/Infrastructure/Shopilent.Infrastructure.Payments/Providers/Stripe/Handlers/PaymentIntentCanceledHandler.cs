using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class PaymentIntentCanceledHandler : IStripeWebhookHandler
{
    private readonly ILogger<PaymentIntentCanceledHandler> _logger;

    public PaymentIntentCanceledHandler(ILogger<PaymentIntentCanceledHandler> logger)
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
        result.PaymentStatus = PaymentStatus.Canceled;
        result.CustomerId = paymentIntent.CustomerId;

        if (paymentIntent.Metadata != null)
        {
            result.EventData.Add("metadata", paymentIntent.Metadata);
        }

        result.OrderId = paymentIntent.Metadata["orderId"];
        result.ProcessingMessage = "Payment was canceled";
        result.IsProcessed = true;

        _logger.LogInformation("Payment canceled: {PaymentIntentId}", paymentIntent.Id);

        return result;
    }
}