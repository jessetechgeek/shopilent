using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Infrastructure.Payments.Utilities;

internal static class PaymentValidators
{
    public static bool IsValidAmount(Money amount)
    {
        return amount != null && amount.Amount > 0;
    }

    public static bool IsValidTransactionId(string transactionId)
    {
        return !string.IsNullOrWhiteSpace(transactionId);
    }

    public static bool IsValidCustomerId(string customerId)
    {
        return !string.IsNullOrWhiteSpace(customerId);
    }

    public static bool IsValidPaymentMethodToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token);
    }

    public static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && email.Contains('@');
    }

    public static bool IsValidWebhookSignature(string signature)
    {
        return !string.IsNullOrWhiteSpace(signature);
    }

    public static bool IsValidWebhookPayload(string payload)
    {
        return !string.IsNullOrWhiteSpace(payload);
    }
}
