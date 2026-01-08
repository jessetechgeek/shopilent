using Shopilent.Domain.Common.Errors;

namespace Shopilent.Domain.Payments.Errors;

public static class PaymentMethodErrors
{
    public static Error TokenRequired => Error.Validation(
        code: "PaymentMethod.TokenRequired",
        message: "Payment method token cannot be empty.");

    public static Error DisplayNameRequired => Error.Validation(
        code: "PaymentMethod.DisplayNameRequired",
        message: "Payment method display name cannot be empty.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "PaymentMethod.NotFound",
        message: $"Payment method with ID {id} was not found.");

    public static Error InvalidCardDetails => Error.Validation(
        code: "PaymentMethod.InvalidCardDetails",
        message: "The provided card details are invalid.");

    public static Error ExpiredCard => Error.Validation(
        code: "PaymentMethod.ExpiredCard",
        message: "The card has expired.");

    public static Error InactivePaymentMethod => Error.Validation(
        code: "PaymentMethod.Inactive",
        message: "The payment method is inactive.");

    public static Error NoDefaultPaymentMethod => Error.NotFound(
        code: "PaymentMethod.NoDefault",
        message: "No default payment method found for the user.");

    public static Error InvalidMetadataKey => Error.Validation(
        code: "PaymentMethod.InvalidMetadataKey",
        message: "Metadata key cannot be empty.");

    public static Error InvalidProviderType => Error.Validation(
        code: "PaymentMethod.InvalidProviderType",
        message: "The provider type is not valid for this payment method type.");

    public static Error DuplicateTokenForUser => Error.Conflict(
        code: "PaymentMethod.DuplicateToken",
        message: "A payment method with this token already exists for this user.");

    public static Error InvalidUserId => Error.Validation(
        code: "PaymentMethod.InvalidUserId",
        message: "User ID cannot be empty.");

    public static Error EmailRequired => Error.Validation(
        code: "PaymentMethod.EmailRequired",
        message: "Email cannot be empty.");
}
