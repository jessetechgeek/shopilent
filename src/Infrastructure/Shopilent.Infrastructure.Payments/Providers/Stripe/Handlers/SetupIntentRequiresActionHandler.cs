using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;

internal class SetupIntentRequiresActionHandler : IStripeWebhookHandler
{
    private readonly ILogger<SetupIntentRequiresActionHandler> _logger;

    public SetupIntentRequiresActionHandler(ILogger<SetupIntentRequiresActionHandler> logger)
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
        result.PaymentStatus = PaymentStatus.RequiresAction;
        result.CustomerId = setupIntent.CustomerId;
        result.EventData.Add("payment_method_id", setupIntent.PaymentMethodId ?? string.Empty);
        result.EventData.Add("usage", setupIntent.Usage ?? string.Empty);

        // Add next action information
        if (setupIntent.NextAction != null)
        {
            result.EventData.Add("next_action_type", setupIntent.NextAction.Type);
            result.EventData.Add("client_secret", setupIntent.ClientSecret ?? string.Empty);
        }

        if (setupIntent.Metadata != null)
        {
            result.EventData.Add("metadata", setupIntent.Metadata);
        }

        result.OrderId = setupIntent.Metadata?.GetValueOrDefault("orderId");
        result.ProcessingMessage = "Setup intent requires action - 3D Secure authentication needed";
        result.IsProcessed = true;

        _logger.LogInformation("Setup intent requires action: {SetupIntentId} for customer {CustomerId}, action type: {NextActionType}",
            setupIntent.Id, setupIntent.CustomerId, setupIntent.NextAction?.Type);

        return result;
    }
}