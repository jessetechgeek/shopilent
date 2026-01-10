using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class ChargeSucceededHandler : IStripeWebhookHandler
{
    private readonly ILogger<ChargeSucceededHandler> _logger;

    public ChargeSucceededHandler(ILogger<ChargeSucceededHandler> logger)
    {
        _logger = logger;
    }

    public async Task<WebhookResult> HandleAsync(Event stripeEvent, WebhookResult result, CancellationToken cancellationToken)
    {
        var charge = stripeEvent.Data.Object as Charge;
        if (charge == null)
        {
            result.ProcessingMessage = "Invalid Charge data in webhook";
            return result;
        }

        result.TransactionId = charge.PaymentIntentId ?? charge.Id;
        result.PaymentStatus = PaymentStatus.Succeeded;
        result.CustomerId = charge.CustomerId;
        result.EventData.Add("charge_id", charge.Id);
        result.EventData.Add("payment_intent_id", charge.PaymentIntentId ?? string.Empty);
        result.EventData.Add("amount", charge.Amount);
        result.EventData.Add("currency", charge.Currency);
        result.EventData.Add("payment_method", charge.PaymentMethod ?? string.Empty);

        // Add charge outcome information (risk assessment, etc.)
        if (charge.Outcome != null)
        {
            result.EventData.Add("risk_level", charge.Outcome.RiskLevel ?? "unknown");
            result.EventData.Add("outcome_type", charge.Outcome.Type ?? "unknown");
            result.EventData.Add("seller_message", charge.Outcome.SellerMessage ?? string.Empty);

            if (!string.IsNullOrEmpty(charge.Outcome.Reason))
            {
                result.EventData.Add("outcome_reason", charge.Outcome.Reason);
            }
        }

        // Add payment method details if available
        if (charge.PaymentMethodDetails?.Card != null)
        {
            var card = charge.PaymentMethodDetails.Card;
            result.EventData.Add("card_brand", card.Brand ?? string.Empty);
            result.EventData.Add("card_last4", card.Last4 ?? string.Empty);
            result.EventData.Add("card_country", card.Country ?? string.Empty);

            if (card.ThreeDSecure != null)
            {
                result.EventData.Add("three_d_secure_result", card.ThreeDSecure.Result ?? string.Empty);
                result.EventData.Add("three_d_secure_version", card.ThreeDSecure.Version ?? string.Empty);
            }
        }

        // Add charge metadata
        if (charge.Metadata != null && charge.Metadata.Count > 0)
        {
            result.EventData.Add("metadata", charge.Metadata);
        }

        // Add billing details if available
        if (charge.BillingDetails != null)
        {
            result.EventData.Add("billing_email", charge.BillingDetails.Email ?? string.Empty);
            result.EventData.Add("billing_name", charge.BillingDetails.Name ?? string.Empty);
        }

        result.OrderId = charge.Metadata["orderId"];
        result.ProcessingMessage = "Charge succeeded";
        result.IsProcessed = true;

        _logger.LogInformation(
            "Charge succeeded: {ChargeId} for PaymentIntent: {PaymentIntentId}, Amount: {Amount} {Currency}",
            charge.Id, charge.PaymentIntentId, charge.Amount, charge.Currency?.ToUpperInvariant());

        // Log 3DS information if available
        if (charge.PaymentMethodDetails?.Card?.ThreeDSecure != null)
        {
            var threeDSecure = charge.PaymentMethodDetails.Card.ThreeDSecure;
            _logger.LogInformation(
                "3D Secure authentication completed for charge {ChargeId}: Result={Result}, Version={Version}",
                charge.Id, threeDSecure.Result, threeDSecure.Version);
        }

        return result;
    }
}