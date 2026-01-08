using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Payments.Commands.ProcessWebhook.V1;

internal sealed class ProcessWebhookCommandHandlerV1 : ICommandHandler<ProcessWebhookCommandV1, WebhookResult>
{
    private readonly IPaymentService _paymentService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly IPaymentWriteRepository _paymentWriteRepository;
    private readonly ILogger<ProcessWebhookCommandHandlerV1> _logger;

    public ProcessWebhookCommandHandlerV1(
        IPaymentService paymentService,
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        IPaymentWriteRepository paymentWriteRepository,
        ILogger<ProcessWebhookCommandHandlerV1> logger)
    {
        _paymentService = paymentService;
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _paymentWriteRepository = paymentWriteRepository;
        _logger = logger;
    }

    public async Task<Result<WebhookResult>> Handle(ProcessWebhookCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing webhook for provider: {Provider}", request.Provider);

            // Parse provider enum
            if (!Enum.TryParse<PaymentProvider>(request.Provider, true, out var provider))
            {
                _logger.LogError("Unknown payment provider: {Provider}", request.Provider);
                return Result.Failure<WebhookResult>(
                    PaymentErrors.InvalidProvider);
            }

            // Process the webhook through the payment service
            var result = await _paymentService.ProcessWebhookAsync(
                provider,
                request.WebhookPayload,
                request.Signature,
                request.Headers,
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Webhook processing failed for provider {Provider}: {Error}",
                    request.Provider, result.Error.Message);
                return Result.Failure<WebhookResult>(result.Error);
            }

            // Handle order/payment updates based on webhook event
            await HandleWebhookEventAsync(result.Value, cancellationToken);

            _logger.LogInformation(
                "Webhook processed successfully for provider {Provider}. EventId: {EventId}, IsProcessed: {IsProcessed}",
                request.Provider, result.Value.EventId, result.Value.IsProcessed);

            return Result.Success(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing webhook for provider: {Provider}", request.Provider);
            return Result.Failure<WebhookResult>(
                PaymentErrors.ProcessingFailed($"Unexpected error: {ex.Message}"));
        }
    }

    private async Task HandleWebhookEventAsync(WebhookResult webhookResult,
        CancellationToken cancellationToken)
    {
        if (!webhookResult.IsProcessed || string.IsNullOrEmpty(webhookResult.TransactionId))
        {
            _logger.LogInformation(
                "Webhook event {EventType} not processed or missing transaction ID, skipping order/payment updates",
                webhookResult.EventType);
            return;
        }

        try
        {
            switch (webhookResult.EventType)
            {
                case "payment_intent.succeeded":
                case "charge.succeeded":
                    await HandlePaymentSucceededAsync(webhookResult, cancellationToken);
                    break;

                case "payment_intent.payment_failed":
                case "payment_intent.canceled":
                    await HandlePaymentFailedAsync(webhookResult, cancellationToken);
                    break;

                case "charge.dispute.created":
                    await HandlePaymentDisputeAsync(webhookResult, cancellationToken);
                    break;

                case "charge.refunded":
                case "payment_intent.amount_capturable_updated":
                    await HandlePaymentRefundedAsync(webhookResult, cancellationToken);
                    break;

                case "setup_intent.succeeded":
                    await HandleSetupIntentSucceededAsync(webhookResult, cancellationToken);
                    break;

                case "setup_intent.canceled":
                    await HandleSetupIntentCanceledAsync(webhookResult, cancellationToken);
                    break;

                case "setup_intent.requires_action":
                    _logger.LogInformation("Setup intent {SetupIntentId} requires action - handled client-side",
                        webhookResult.TransactionId);
                    break;

                default:
                    _logger.LogInformation("Webhook event type {EventType} does not require order/payment updates",
                        webhookResult.EventType);
                    break;
            }

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully processed webhook event {EventType} for transaction {TransactionId}",
                webhookResult.EventType, webhookResult.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling webhook event {EventType} for transaction {TransactionId}",
                webhookResult.EventType, webhookResult.TransactionId);
            throw;
        }
    }

    private async Task HandlePaymentSucceededAsync(WebhookResult webhookResult,
        CancellationToken cancellationToken)
    {
        // Find payment by transaction ID
        var payment =
            await _paymentWriteRepository.GetByExternalReferenceAsync(webhookResult.TransactionId, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning("Payment not found for transaction ID {TransactionId}", webhookResult.TransactionId);
            return;
        }

        // Update payment status
        payment.MarkAsSucceeded(webhookResult.TransactionId);

        // Update associated order if exists
        if (payment.OrderId != Guid.Empty)
        {
            var order = await _orderWriteRepository.GetByIdAsync(payment.OrderId, cancellationToken);
            if (order != null)
            {
                order.MarkAsPaid();

                _logger.LogInformation("Marked order {OrderId} as paid due to successful payment {TransactionId}",
                    order.Id, webhookResult.TransactionId);
            }
        }

        _logger.LogInformation("Updated payment {PaymentId} status to succeeded for transaction {TransactionId}",
            payment.Id, webhookResult.TransactionId);
    }

    private async Task HandlePaymentFailedAsync(WebhookResult webhookResult,
        CancellationToken cancellationToken)
    {
        // Find payment by transaction ID
        var payment =
            await _paymentWriteRepository.GetByExternalReferenceAsync(webhookResult.TransactionId, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning("Payment not found for transaction ID {TransactionId}", webhookResult.TransactionId);
            return;
        }

        // Update payment status with failure reason
        var errorMessage = webhookResult.EventData?.ContainsKey("failure_reason") == true
            ? webhookResult.EventData["failure_reason"]?.ToString()
            : "Payment failed";

        payment.MarkAsFailed(errorMessage);

        // Update associated order if exists
        if (payment.OrderId != Guid.Empty)
        {
            var order = await _orderWriteRepository.GetByIdAsync(payment.OrderId, cancellationToken);
            if (order != null)
            {
                // This restores stock via OrderCancelledEvent
                if (webhookResult.EventType == "payment_intent.canceled" ||
                    webhookResult.EventType == "payment_intent.payment_failed")
                {
                    var cancelReason = webhookResult.EventType == "payment_intent.canceled"
                        ? "Payment was canceled"
                        : $"Payment failed: {errorMessage}";

                    var cancelResult = order.Cancel(cancelReason);

                    if (cancelResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "Canceled order {OrderId} due to payment failure. TransactionId: {TransactionId}, EventType: {EventType}, Reason: {Reason}",
                            order.Id, webhookResult.TransactionId, webhookResult.EventType, cancelReason);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to cancel order {OrderId} after payment failure: {Error}. Order may already be cancelled.",
                            order.Id, cancelResult.Error.Message);
                    }
                }
            }
        }

        _logger.LogInformation("Updated payment {PaymentId} status to failed for transaction {TransactionId}",
            payment.Id, webhookResult.TransactionId);
    }

    private async Task HandlePaymentDisputeAsync(WebhookResult webhookResult,
        CancellationToken cancellationToken)
    {
        // Find payment by transaction ID (for disputes, we might need to look at charge ID)
        var transactionId = webhookResult.TransactionId;

        // For disputes, the transaction ID might be the charge ID, try to find payment
        var payment =
            await _paymentWriteRepository.GetByExternalReferenceAsync(webhookResult.TransactionId, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning("Payment not found for disputed transaction ID {TransactionId}", transactionId);
            return;
        }

        // Update payment status to disputed
        payment.UpdateStatus(PaymentStatus.Disputed);

        _logger.LogInformation("Marked payment {PaymentId} as disputed for transaction {TransactionId}",
            payment.Id, transactionId);
    }

    private async Task HandlePaymentRefundedAsync(WebhookResult webhookResult,
        CancellationToken cancellationToken)
    {
        // Find payment by transaction ID
        var payment =
            await _paymentWriteRepository.GetByExternalReferenceAsync(webhookResult.TransactionId, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning("Payment not found for refunded transaction ID {TransactionId}",
                webhookResult.TransactionId);
            return;
        }

        // Update payment status to refunded
        payment.MarkAsRefunded(webhookResult.TransactionId);

        // Update associated order if exists
        if (payment.OrderId != Guid.Empty)
        {
            var order = await _orderWriteRepository.GetByIdAsync(payment.OrderId, cancellationToken);
            if (order != null)
            {
                // Process order refund - check if it's full or partial refund
                var isFullRefund = true;
                Result refundResult;

                // Extract refund amount from event data if available
                if (webhookResult.EventData?.ContainsKey("amount_refunded") == true)
                {
                    if (decimal.TryParse(webhookResult.EventData["amount_refunded"]?.ToString(),
                            out var refundAmountDecimal))
                    {
                        // Convert from cents to dollars for Stripe
                        var refundAmountResult =
                            Money.Create(refundAmountDecimal / 100, payment.Amount.Currency);
                        if (refundAmountResult.IsSuccess)
                        {
                            var refundAmount = refundAmountResult.Value;
                            // Check if this is a partial refund
                            if (refundAmount.Amount < payment.Amount.Amount)
                            {
                                isFullRefund = false;
                                refundResult = order.ProcessPartialRefund(refundAmount,
                                    "Partial refund processed via payment webhook");
                            }
                            else
                            {
                                refundResult = order.ProcessRefund("Full refund processed via payment webhook");
                            }
                        }
                        else
                        {
                            // Fallback to full refund if amount parsing fails
                            refundResult = order.ProcessRefund("Refund processed via payment webhook");
                        }
                    }
                    else
                    {
                        refundResult = order.ProcessRefund("Refund processed via payment webhook");
                    }
                }
                else
                {
                    refundResult = order.ProcessRefund("Refund processed via payment webhook");
                }

                if (refundResult.IsSuccess)
                {
                    var refundType = isFullRefund ? "full" : "partial";
                    _logger.LogInformation(
                        "Processed {RefundType} refund for order {OrderId} due to payment refund {TransactionId}",
                        refundType, order.Id, webhookResult.TransactionId);
                }
                else
                {
                    _logger.LogWarning("Failed to process order refund for order {OrderId}: {Error}",
                        order.Id, refundResult.Error.Message);
                }
            }
        }

        _logger.LogInformation("Updated payment {PaymentId} status to refunded for transaction {TransactionId}",
            payment.Id, webhookResult.TransactionId);
    }

    private async Task HandleSetupIntentSucceededAsync(WebhookResult webhookResult,
        CancellationToken cancellationToken)
    {
        // Setup intents don't directly create payments, but they set up payment methods for future use
        // We can track this in the payment method system if needed

        var customerId = webhookResult.CustomerId;
        var paymentMethodId = webhookResult.EventData.GetValueOrDefault("payment_method_id")?.ToString();

        if (!string.IsNullOrEmpty(customerId) && !string.IsNullOrEmpty(paymentMethodId))
        {
            // Here we could update the payment method status in the database
            // For now, we'll just log the successful setup
            _logger.LogInformation(
                "Payment method {PaymentMethodId} successfully set up for customer {CustomerId} via setup intent {SetupIntentId}",
                paymentMethodId, customerId, webhookResult.TransactionId);
        }
    }

    private async Task HandleSetupIntentCanceledAsync(WebhookResult webhookResult,
        CancellationToken cancellationToken)
    {
        // Setup intent was canceled - log for monitoring
        var customerId = webhookResult.CustomerId;
        var cancellationReason = webhookResult.EventData.GetValueOrDefault("cancellation_reason")?.ToString();

        _logger.LogWarning(
            "Setup intent {SetupIntentId} was canceled for customer {CustomerId}, reason: {CancellationReason}",
            webhookResult.TransactionId, customerId, cancellationReason ?? "Unknown");
    }
}
