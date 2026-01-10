using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Infrastructure.Payments.Abstractions;
using Shopilent.Infrastructure.Payments.Models;

namespace Shopilent.Infrastructure.Payments.Services;

internal class PaymentService : IPaymentService
{
    private readonly Dictionary<PaymentProvider, IPaymentProvider> _providers;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IEnumerable<IPaymentProvider> providers,
        ILogger<PaymentService> logger)
    {
        _providers = providers.ToDictionary(p => p.Provider, p => p);
        _logger = logger;
    }

    public async Task<Result<PaymentResult>> ProcessPaymentAsync(
        Money amount,
        PaymentMethodType methodType,
        PaymentProvider provider,
        string paymentMethodToken,
        string customerId = null,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_providers.TryGetValue(provider, out var paymentProvider))
            {
                _logger.LogError("Payment provider not configured: {Provider}", provider);
                return Result.Failure<PaymentResult>(
                    PaymentErrors.InvalidProvider);
            }

            var request = new PaymentRequest
            {
                Amount = amount,
                MethodType = methodType,
                PaymentMethodToken = paymentMethodToken,
                CustomerId = customerId,
                Metadata = metadata ?? new Dictionary<string, object>(),
                SavePaymentMethod = false // Not saving, just processing
            };

            return await paymentProvider.ProcessPaymentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment with provider {Provider}", provider);
            return Result.Failure<PaymentResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public async Task<Result<string>> RefundPaymentAsync(
        string transactionId,
        Money amount = null,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Implementation would depend on provider identification from transaction ID
            // For now, try all providers until one handles it
            foreach (var provider in _providers.Values)
            {
                var result = await provider.RefundPaymentAsync(transactionId, amount, reason, cancellationToken);
                if (result.IsSuccess)
                {
                    return result;
                }
            }

            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed("No provider could process the refund"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for transaction {TransactionId}", transactionId);
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public async Task<Result<PaymentStatus>> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Implementation would depend on provider identification from transaction ID
            // For now, try all providers until one handles it
            foreach (var provider in _providers.Values)
            {
                var result = await provider.GetPaymentStatusAsync(transactionId, cancellationToken);
                if (result.IsSuccess)
                {
                    return result;
                }
            }

            return Result.Failure<PaymentStatus>(
                PaymentErrors.ProcessingFailed("No provider could get the payment status"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status for transaction {TransactionId}", transactionId);
            return Result.Failure<PaymentStatus>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public async Task<Result<string>> GetOrCreateCustomerAsync(
        PaymentProvider provider,
        string userId,
        string email,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_providers.TryGetValue(provider, out var paymentProvider))
            {
                _logger.LogError("Payment provider not configured: {Provider}", provider);
                return Result.Failure<string>(
                    PaymentErrors.InvalidProvider);
            }

            return await paymentProvider.GetOrCreateCustomerAsync(userId, email, metadata, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer with provider {Provider}", provider);
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public async Task<Result<string>> AttachPaymentMethodToCustomerAsync(
        PaymentProvider provider,
        string paymentMethodToken,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_providers.TryGetValue(provider, out var paymentProvider))
            {
                _logger.LogError("Payment provider not configured: {Provider}", provider);
                return Result.Failure<string>(
                    PaymentErrors.InvalidProvider);
            }

            return await paymentProvider.AttachPaymentMethodToCustomerAsync(paymentMethodToken, customerId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching payment method to customer with provider {Provider}", provider);
            return Result.Failure<string>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public async Task<Result<WebhookResult>> ProcessWebhookAsync(
        PaymentProvider provider,
        string webhookPayload,
        string signature = null,
        Dictionary<string, string> headers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_providers.TryGetValue(provider, out var paymentProvider))
            {
                _logger.LogError("Payment provider not configured: {Provider}", provider);
                return Result.Failure<WebhookResult>(
                    PaymentErrors.InvalidProvider);
            }

            var result =
                await paymentProvider.ProcessWebhookAsync(webhookPayload, signature, headers, cancellationToken);

            if (result.IsFailure)
            {
                return Result.Failure<WebhookResult>(result.Error);
            }

            // Set the provider information since it's only available at this level
            result.Value.Provider = provider;

            return Result.Success(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook with provider {Provider}", provider);
            return Result.Failure<WebhookResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public async Task<Result<SetupIntentResult>> CreateSetupIntentAsync(
        PaymentProvider provider,
        string customerId,
        string paymentMethodToken = null,
        Dictionary<string, object> metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_providers.TryGetValue(provider, out var paymentProvider))
            {
                _logger.LogError("Payment provider not configured: {Provider}", provider);
                return Result.Failure<SetupIntentResult>(
                    PaymentErrors.InvalidProvider);
            }

            return await paymentProvider.CreateSetupIntentAsync(customerId, paymentMethodToken, metadata,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating setup intent with provider {Provider}", provider);
            return Result.Failure<SetupIntentResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }

    public async Task<Result<SetupIntentResult>> ConfirmSetupIntentAsync(
        PaymentProvider provider,
        string setupIntentId,
        string paymentMethodToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_providers.TryGetValue(provider, out var paymentProvider))
            {
                _logger.LogError("Payment provider not configured: {Provider}", provider);
                return Result.Failure<SetupIntentResult>(
                    PaymentErrors.InvalidProvider);
            }

            return await paymentProvider.ConfirmSetupIntentAsync(setupIntentId, paymentMethodToken, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming setup intent with provider {Provider}", provider);
            return Result.Failure<SetupIntentResult>(
                PaymentErrors.ProcessingFailed(ex.Message));
        }
    }
}
