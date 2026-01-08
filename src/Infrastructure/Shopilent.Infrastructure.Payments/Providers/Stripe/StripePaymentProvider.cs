using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Infrastructure.Payments.Models;
using Shopilent.Infrastructure.Payments.Providers.Base;
using Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;
using Shopilent.Infrastructure.Payments.Settings;
using Stripe;

namespace Shopilent.Infrastructure.Payments.Providers.Stripe;

internal class StripePaymentProvider : PaymentProviderBase
{
    private readonly StripeSettings _settings;
    private readonly PaymentIntentService _paymentIntentService;
    private readonly RefundService _refundService;
    private readonly CustomerService _customerService;
    private readonly SetupIntentService _setupIntentService;
    private readonly PaymentMethodService _paymentMethodService;
    private readonly ChargeService _chargeService;
    private readonly EventService _eventService;
    private readonly StripeWebhookHandlerFactory _webhookHandlerFactory;

    public override PaymentProvider Provider => PaymentProvider.Stripe;

    public StripePaymentProvider(
        IOptions<StripeSettings> settings,
        ILogger<StripePaymentProvider> logger,
        StripeWebhookHandlerFactory webhookHandlerFactory) : base(logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _webhookHandlerFactory =
            webhookHandlerFactory ?? throw new ArgumentNullException(nameof(webhookHandlerFactory));

        StripeConfiguration.ApiKey = _settings.SecretKey;

        _paymentIntentService = new PaymentIntentService();
        _refundService = new RefundService();
        _customerService = new CustomerService();
        _setupIntentService = new SetupIntentService();
        _paymentMethodService = new PaymentMethodService();
        _chargeService = new ChargeService();
        _eventService = new EventService();
    }

    public override async Task<Result<PaymentResult>> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Processing Stripe payment for amount {Amount}", request.Amount);

            var options = new PaymentIntentCreateOptions
            {
                Amount = ConvertToStripeAmount(request.Amount),
                Currency = request.Amount.Currency.ToLowerInvariant(),
                PaymentMethod = request.PaymentMethodToken,
                ConfirmationMethod = "automatic",
                Confirm = true,
                ReturnUrl = GetReturnUrl(request),
                Metadata = ConvertMetadata(request.Metadata),
                // Enable 3D Secure authentication when needed
                PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                {
                    Card = new PaymentIntentPaymentMethodOptionsCardOptions { RequestThreeDSecure = "automatic" }
                }
            };

            // Add customer if provided
            if (!string.IsNullOrEmpty(request.CustomerId))
            {
                options.Customer = request.CustomerId;
                Logger.LogInformation("Using customer {CustomerId} for payment processing", request.CustomerId);
            }

            var paymentIntent = await _paymentIntentService.CreateAsync(options, cancellationToken: cancellationToken);

            Logger.LogInformation("Stripe payment intent created: {PaymentIntentId} with status: {Status}",
                paymentIntent.Id, paymentIntent.Status);

            var paymentProcessingResult = await BuildPaymentResultAsync(paymentIntent);

            return Result.Success(paymentProcessingResult);
        }
        catch (StripeException stripeEx)
        {
            Logger.LogError(stripeEx, "Stripe payment failed: {ErrorType} - {ErrorMessage}",
                stripeEx.StripeError?.Type, stripeEx.Message);

            var error = HandleStripeException(stripeEx);
            return Result.Failure<PaymentResult>(error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error processing Stripe payment");
            return Result.Failure<PaymentResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public override async Task<Result<string>> RefundPaymentAsync(
        string transactionId,
        Money amount = null,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Processing Stripe refund for transaction {TransactionId}", transactionId);

            var options = new RefundCreateOptions
            {
                PaymentIntent = transactionId, Reason = ConvertRefundReason(reason)
            };

            if (amount != null)
            {
                options.Amount = ConvertToStripeAmount(amount);
            }

            var refund = await _refundService.CreateAsync(options, cancellationToken: cancellationToken);

            Logger.LogInformation("Stripe refund created: {RefundId}", refund.Id);

            return Result.Success(refund.Id);
        }
        catch (StripeException stripeEx)
        {
            Logger.LogError(stripeEx, "Stripe refund failed: {ErrorMessage}", stripeEx.Message);
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(stripeEx.Message));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error processing Stripe refund");
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public override async Task<Result<PaymentStatus>> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Getting Stripe payment status for transaction {TransactionId}", transactionId);

            var paymentIntent =
                await _paymentIntentService.GetAsync(transactionId, cancellationToken: cancellationToken);

            var status = ConvertStripeStatus(paymentIntent.Status);

            Logger.LogInformation("Stripe payment status retrieved: {Status}", status);

            return Result.Success(status);
        }
        catch (StripeException stripeEx)
        {
            Logger.LogError(stripeEx, "Failed to get Stripe payment status: {ErrorMessage}", stripeEx.Message);
            return Result.Failure<PaymentStatus>(
                PaymentErrors.ProcessingFailed(stripeEx.Message));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error getting Stripe payment status");
            return Result.Failure<PaymentStatus>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    private static long ConvertToStripeAmount(Money amount)
    {
        // Stripe expects amounts in cents for most currencies
        // This is a simplified conversion - in production, you'd need to handle
        // zero-decimal currencies like JPY differently
        return (long)(amount.Amount * 100);
    }

    private static PaymentStatus ConvertStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "requires_payment_method" => PaymentStatus.Pending,
            "requires_confirmation" => PaymentStatus.RequiresConfirmation,
            "requires_action" => PaymentStatus.RequiresAction,
            "processing" => PaymentStatus.Processing,
            "succeeded" => PaymentStatus.Succeeded,
            "canceled" => PaymentStatus.Canceled,
            "payment_failed" => PaymentStatus.Failed,
            _ => PaymentStatus.Failed
        };
    }

    private static string ConvertRefundReason(string reason)
    {
        return reason switch
        {
            "duplicate" => "duplicate",
            "fraudulent" => "fraudulent",
            "requested_by_customer" => "requested_by_customer",
            _ => "requested_by_customer"
        };
    }

    private static Dictionary<string, string> ConvertMetadata(Dictionary<string, object> metadata)
    {
        var stripeMetadata = new Dictionary<string, string>();

        foreach (var kvp in metadata)
        {
            stripeMetadata.Add(kvp.Key, kvp.Value?.ToString() ?? string.Empty);
        }

        return stripeMetadata;
    }

    private static string GetReturnUrl(PaymentRequest request)
    {
        // In a real implementation, this would come from configuration
        // or be passed in the request
        return "https://your-app.com/payment/return";
    }

    public override async Task<Result<string>> GetOrCreateCustomerAsync(
        string userId,
        string email,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Creating or retrieving Stripe customer for user {UserId} using idempotency", userId);

            var options = new CustomerCreateOptions
            {
                Email = email, Metadata = new Dictionary<string, string> { ["user_id"] = userId }
            };

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    options.Metadata.Add($"custom_{kvp.Key}", kvp.Value?.ToString() ?? string.Empty);
                }
            }

            var requestOptions = new RequestOptions { IdempotencyKey = $"customer_create_{userId}" };

            var customer = await _customerService.CreateAsync(
                options,
                requestOptions,
                cancellationToken);

            Logger.LogInformation("Stripe customer resolved: {CustomerId} for user {UserId}", customer.Id, userId);

            return Result.Success(customer.Id);
        }
        catch (StripeException stripeEx)
        {
            Logger.LogError(stripeEx, "Stripe customer creation failed: {ErrorType} - {ErrorMessage}",
                stripeEx.StripeError?.Type, stripeEx.Message);
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(stripeEx.Message));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error creating Stripe customer");
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public override async Task<Result<string>> AttachPaymentMethodToCustomerAsync(
        string paymentMethodToken,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Attaching payment method {PaymentMethodToken} to customer {CustomerId}",
                paymentMethodToken, customerId);

            var options = new PaymentMethodAttachOptions { Customer = customerId };

            var paymentMethod = await _paymentMethodService.AttachAsync(
                paymentMethodToken,
                options,
                cancellationToken: cancellationToken);

            Logger.LogInformation("Payment method {PaymentMethodId} attached to customer {CustomerId}",
                paymentMethod.Id, customerId);

            return Result.Success(paymentMethod.Id);
        }
        catch (StripeException stripeEx)
        {
            Logger.LogError(stripeEx, "Stripe payment method attachment failed: {ErrorMessage}", stripeEx.Message);
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(stripeEx.Message));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error attaching payment method to customer");
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    private async Task<PaymentResult> BuildPaymentResultAsync(PaymentIntent paymentIntent)
    {
        var result = new PaymentResult
        {
            TransactionId = paymentIntent.Id,
            Status = ConvertStripeStatus(paymentIntent.Status),
            ClientSecret = paymentIntent.ClientSecret,
            RequiresAction = paymentIntent.Status == "requires_action",
            Metadata = ConvertMetadataToObject(paymentIntent.Metadata)
        };

        // Handle next action type for 3D Secure
        if (paymentIntent.NextAction != null)
        {
            result.NextActionType = paymentIntent.NextAction.Type;
        }

        // Extract risk information if available from latest charge
        if (!string.IsNullOrEmpty(paymentIntent.LatestChargeId))
        {
            try
            {
                var charge = await _chargeService.GetAsync(paymentIntent.LatestChargeId);

                if (charge?.Outcome != null)
                {
                    result.RiskLevel = charge.Outcome.RiskLevel;

                    if (charge.Outcome.Type == "issuer_declined")
                    {
                        result.DeclineReason = charge.Outcome.Reason ?? "declined_by_issuer";
                    }
                }

                if (!string.IsNullOrEmpty(charge?.FailureCode))
                {
                    result.FailureReason = charge.FailureCode;
                }
            }
            catch (StripeException ex)
            {
                Logger.LogWarning(ex, "Failed to retrieve charge details for PaymentIntent {PaymentIntentId}",
                    paymentIntent.Id);
            }
        }

        return result;
    }

    private Error HandleStripeException(StripeException stripeEx)
    {
        return stripeEx.StripeError?.Type switch
        {
            "card_error" => HandleCardError(stripeEx.StripeError),
            "validation_error" => PaymentErrors.ProcessingFailed(stripeEx.Message),
            "api_error" => PaymentErrors.ProcessingFailed(
                "Payment service temporarily unavailable"),
            "authentication_error" => PaymentErrors.ProcessingFailed(
                "Payment authentication failed"),
            "rate_limit_error" => PaymentErrors.ProcessingFailed(
                "Too many requests, please try again later"),
            "idempotency_error" => PaymentErrors.ProcessingFailed("Duplicate payment request"),
            _ => PaymentErrors.ProcessingFailed(stripeEx.Message)
        };
    }

    private Error HandleCardError(StripeError error)
    {
        return error.Code switch
        {
            "card_declined" => DetermineDeclineReason(error.DeclineCode),
            "expired_card" => PaymentErrors.ExpiredCard,
            "insufficient_funds" => PaymentErrors.InsufficientFunds,
            "incorrect_cvc" => PaymentErrors.InvalidCard,
            "incorrect_number" => PaymentErrors.InvalidCard,
            "invalid_cvc" => PaymentErrors.InvalidCard,
            "invalid_expiry_month" => PaymentErrors.InvalidCard,
            "invalid_expiry_year" => PaymentErrors.InvalidCard,
            "invalid_number" => PaymentErrors.InvalidCard,
            "processing_error" => PaymentErrors.ProcessingFailed("Payment processing error"),
            "authentication_required" => PaymentErrors.AuthenticationRequired,
            _ => PaymentErrors.CardDeclined(error.Message ?? "Unknown reason")
        };
    }

    private Error DetermineDeclineReason(string declineCode)
    {
        return declineCode switch
        {
            "insufficient_funds" => PaymentErrors.InsufficientFunds,
            "fraudulent" => PaymentErrors.FraudSuspected,
            "stolen_card" => PaymentErrors.FraudSuspected,
            "lost_card" => PaymentErrors.FraudSuspected,
            "pickup_card" => PaymentErrors.FraudSuspected,
            "restricted_card" => PaymentErrors.FraudSuspected,
            "security_violation" => PaymentErrors.FraudSuspected,
            "expired_card" => PaymentErrors.ExpiredCard,
            "incorrect_cvc" => PaymentErrors.InvalidCard,
            "processing_error" => PaymentErrors.ProcessingFailed("Payment processing error"),
            "issuer_not_available" => PaymentErrors.ProcessingFailed(
                "Card issuer temporarily unavailable"),
            "try_again_later" => PaymentErrors.ProcessingFailed("Please try again later"),
            "risk_threshold" => PaymentErrors.RiskLevelTooHigh,
            _ => PaymentErrors.CardDeclined(declineCode ?? "Unknown decline reason")
        };
    }

    private static Dictionary<string, object> ConvertMetadataToObject(Dictionary<string, string> metadata)
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in metadata)
        {
            result.Add(kvp.Key, kvp.Value);
        }

        return result;
    }

    public override async Task<Result<WebhookResult>> ProcessWebhookAsync(
        string webhookPayload,
        string signature = null,
        Dictionary<string, string> headers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Processing Stripe webhook");

            // Verify webhook signature if provided
            Event stripeEvent;
            if (!string.IsNullOrEmpty(signature) && !string.IsNullOrEmpty(_settings.WebhookSecret))
            {
                try
                {
                    stripeEvent = EventUtility.ConstructEvent(
                        webhookPayload,
                        signature,
                        _settings.WebhookSecret);
                    Logger.LogInformation("Stripe webhook signature verified successfully");
                }
                catch (StripeException ex)
                {
                    Logger.LogError(ex, "Stripe webhook signature verification failed");
                    return Result.Failure<WebhookResult>(
                        PaymentErrors.ProcessingFailed("Invalid webhook signature"));
                }
            }
            else
            {
                // Parse webhook payload without signature verification (not recommended for production)
                Logger.LogWarning("Processing Stripe webhook without signature verification");
                try
                {
                    stripeEvent = Event.FromJson(webhookPayload);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to parse Stripe webhook payload");
                    return Result.Failure<WebhookResult>(
                        PaymentErrors.ProcessingFailed("Invalid webhook payload"));
                }
            }

            // Process the webhook event
            var webhookResult = await ProcessStripeEventAsync(stripeEvent, cancellationToken);

            return Result.Success(webhookResult);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error processing Stripe webhook");
            return Result.Failure<WebhookResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    private async Task<WebhookResult> ProcessStripeEventAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var result = new WebhookResult
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            ProcessedAt = DateTime.UtcNow,
            EventData = new Dictionary<string, object>(),
            IsProcessed = false
        };

        Logger.LogInformation("Processing Stripe event: {EventType} with ID: {EventId}",
            stripeEvent.Type, stripeEvent.Id);

        try
        {
            var handler = _webhookHandlerFactory.GetHandler(stripeEvent.Type);
            if (handler != null)
            {
                result = await handler.HandleAsync(stripeEvent, result, cancellationToken);
            }
            else
            {
                Logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                result.ProcessingMessage = $"Event type {stripeEvent.Type} is not handled";
                result.IsProcessed = true; // Mark as processed to avoid retries
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing Stripe event {EventType} with ID {EventId}",
                stripeEvent.Type, stripeEvent.Id);
            result.ProcessingMessage = $"Error processing event: {ex.Message}";
            result.IsProcessed = false;
        }

        return result;
    }

    public override async Task<Result<SetupIntentResult>> CreateSetupIntentAsync(
        string customerId,
        string paymentMethodToken = null,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Creating Stripe setup intent for customer {CustomerId}", customerId);

            var options = new SetupIntentCreateOptions
            {
                Customer = customerId,
                Confirm = !string.IsNullOrEmpty(paymentMethodToken),
                PaymentMethodTypes = new List<string> { "card" }
            };

            if (!string.IsNullOrEmpty(paymentMethodToken))
            {
                options.PaymentMethod = paymentMethodToken;
            }

            if (metadata != null)
            {
                options.Metadata = ConvertMetadata(metadata);
            }

            var setupIntent = await _setupIntentService.CreateAsync(options, cancellationToken: cancellationToken);

            Logger.LogInformation("Stripe setup intent created: {SetupIntentId} with status: {Status}",
                setupIntent.Id, setupIntent.Status);

            var result = await BuildSetupIntentResultAsync(setupIntent);
            return Result.Success(result);
        }
        catch (StripeException stripeEx)
        {
            Logger.LogError(stripeEx, "Stripe setup intent creation failed: {ErrorType} - {ErrorMessage}",
                stripeEx.StripeError?.Type, stripeEx.Message);

            var error = HandleStripeException(stripeEx);
            return Result.Failure<SetupIntentResult>(error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error creating Stripe setup intent");
            return Result.Failure<SetupIntentResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public override async Task<Result<SetupIntentResult>> ConfirmSetupIntentAsync(
        string setupIntentId,
        string paymentMethodToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Confirming Stripe setup intent {SetupIntentId}", setupIntentId);

            var options = new SetupIntentConfirmOptions();

            if (!string.IsNullOrEmpty(paymentMethodToken))
            {
                options.PaymentMethod = paymentMethodToken;
            }

            var setupIntent =
                await _setupIntentService.ConfirmAsync(setupIntentId, options, cancellationToken: cancellationToken);

            Logger.LogInformation("Stripe setup intent confirmed: {SetupIntentId} with status: {Status}",
                setupIntent.Id, setupIntent.Status);

            var result = await BuildSetupIntentResultAsync(setupIntent);
            return Result.Success(result);
        }
        catch (StripeException stripeEx)
        {
            Logger.LogError(stripeEx, "Stripe setup intent confirmation failed: {ErrorType} - {ErrorMessage}",
                stripeEx.StripeError?.Type, stripeEx.Message);

            var error = HandleStripeException(stripeEx);
            return Result.Failure<SetupIntentResult>(error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error confirming Stripe setup intent");
            return Result.Failure<SetupIntentResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    private async Task<SetupIntentResult> BuildSetupIntentResultAsync(SetupIntent setupIntent)
    {
        var result = new SetupIntentResult
        {
            SetupIntentId = setupIntent.Id,
            Status = ConvertStripeSetupIntentStatus(setupIntent.Status),
            ClientSecret = setupIntent.ClientSecret,
            RequiresAction = setupIntent.Status == "requires_action",
            PaymentMethodId = setupIntent.PaymentMethodId,
            CustomerId = setupIntent.CustomerId,
            Usage = setupIntent.Usage,
            Metadata = ConvertMetadataToObject(setupIntent.Metadata)
        };

        // Handle next action type for 3D Secure
        if (setupIntent.NextAction != null)
        {
            result.NextActionType = setupIntent.NextAction.Type;
        }

        return result;
    }

    private static PaymentStatus ConvertStripeSetupIntentStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "requires_payment_method" => PaymentStatus.Pending,
            "requires_confirmation" => PaymentStatus.RequiresConfirmation,
            "requires_action" => PaymentStatus.RequiresAction,
            "processing" => PaymentStatus.Processing,
            "succeeded" => PaymentStatus.Succeeded,
            "canceled" => PaymentStatus.Canceled,
            _ => PaymentStatus.Failed
        };
    }
}
