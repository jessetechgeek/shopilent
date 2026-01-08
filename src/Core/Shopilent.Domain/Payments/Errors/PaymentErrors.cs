using Shopilent.Domain.Common.Errors;

namespace Shopilent.Domain.Payments.Errors;

public static class PaymentErrors
{
    public static Error NegativeAmount => Error.Validation(
        code: "Payment.NegativeAmount",
        message: "Payment amount cannot be negative.");

    public static Error TokenRequired => Error.Validation(
        code: "Payment.TokenRequired",
        message: "Payment token cannot be empty.");

    public static Error InvalidPaymentStatus(string operation) => Error.Validation(
        code: "Payment.InvalidStatus",
        message: $"Cannot perform {operation} operation with current payment status.");

    public static Error PaymentMethodNotFound(Guid id) => Error.NotFound(
        code: "Payment.PaymentMethodNotFound",
        message: $"Payment method with ID {id} was not found.");

    public static Error PaymentNotFound(Guid id) => Error.NotFound(
        code: "Payment.NotFound",
        message: $"Payment with ID {id} was not found.");

    public static Error ProcessingFailed(string errorMessage) => Error.Failure(
        code: "Payment.ProcessingFailed",
        message: $"Payment processing failed: {errorMessage}");

    public static Error InvalidMetadataKey => Error.Validation(
        code: "Payment.InvalidMetadataKey",
        message: "Metadata key cannot be empty.");

    public static Error InvalidProvider => Error.Validation(
        code: "Payment.InvalidProvider",
        message: "The specified payment provider is not supported.");

    public static Error InvalidMethodType => Error.Validation(
        code: "Payment.InvalidMethodType",
        message: "The specified payment method type is not supported.");

    public static Error CurrencyMismatch => Error.Validation(
        code: "Payment.CurrencyMismatch",
        message: "The payment currency does not match the order currency.");

    public static Error AmountMismatch => Error.Validation(
        code: "Payment.AmountMismatch",
        message: "The payment amount does not match the order total.");

    public static Error CardDeclined(string reason) => Error.Validation(
        code: "Payment.CardDeclined",
        message: $"Payment was declined: {reason}");

    public static Error FraudSuspected => Error.Validation(
        code: "Payment.FraudSuspected",
        message: "Payment was declined due to suspected fraud.");

    public static Error AuthenticationRequired => Error.Validation(
        code: "Payment.AuthenticationRequired",
        message: "Additional authentication is required to complete this payment.");

    public static Error InsufficientFunds => Error.Validation(
        code: "Payment.InsufficientFunds",
        message: "Payment was declined due to insufficient funds.");

    public static Error ExpiredCard => Error.Validation(
        code: "Payment.ExpiredCard",
        message: "Payment was declined because the card has expired.");

    public static Error InvalidCard => Error.Validation(
        code: "Payment.InvalidCard",
        message: "Payment was declined due to invalid card details.");

    public static Error RiskLevelTooHigh => Error.Validation(
        code: "Payment.RiskLevelTooHigh",
        message: "Payment was declined due to high risk level.");

    public static Error InvalidOrderId => Error.Validation(
        code: "Payment.InvalidOrderId",
        message: "Order ID cannot be empty.");
}
