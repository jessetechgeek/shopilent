using Shopilent.Domain.Common.Errors;

namespace Shopilent.Domain.Sales.Errors;

public static class OrderErrors
{
    public static Error NegativeAmount => Error.Validation(
        code: "Order.NegativeAmount",
        message: "Order amount cannot be negative.");

    public static Error EmptyCart => Error.Validation(
        code: "Order.EmptyCart",
        message: "Cannot create order from empty cart.");

    public static Error InvalidOrderStatus(string operation) => Error.Validation(
        code: "Order.InvalidStatus",
        message: $"Cannot perform {operation} operation with current order status.");

    public static Error ShippingAddressRequired => Error.Validation(
        code: "Order.ShippingAddressRequired",
        message: "Shipping address is required.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "Order.NotFound",
        message: $"Order with ID {id} was not found.");

    public static Error PaymentRequired => Error.Validation(
        code: "Order.PaymentRequired",
        message: "Payment is required before shipping.");

    public static Error InvalidQuantity => Error.Validation(
        code: "Order.InvalidQuantity",
        message: "Order item quantity must be positive.");

    public static Error InvalidMetadataKey => Error.Validation(
        code: "Order.InvalidMetadataKey",
        message: "Metadata key cannot be empty.");

    public static Error NegativeDiscount => Error.Validation(
        code: "Order.NegativeDiscount",
        message: "Discount amount cannot be negative.");

    public static Error InvalidDiscountPercentage => Error.Validation(
        code: "Order.InvalidDiscountPercentage",
        message: "Discount percentage cannot exceed 100%.");

    public static Error InvalidAmount => Error.Validation(
        code: "Order.InvalidAmount",
        message: "Invalid order amount.");

    public static Error InvalidCurrency => Error.Validation(
        code: "Order.InvalidCurrency",
        message: "Currency code cannot be empty.");

    public static Error CurrencyMismatch => Error.Validation(
        code: "Order.CurrencyMismatch",
        message: "Operations can only be performed on money objects with the same currency.");

    public static Error OrderAlreadyRefunded => Error.Validation(
        code: "Order.AlreadyRefunded",
        message: "This order has already been fully refunded.");

    public static Error ItemNotFound(Guid itemId) => Error.NotFound(
        code: "Order.ItemNotFound",
        message: $"Order item with ID {itemId} was not found.");

    public static Error AccessDenied => Error.Forbidden(
        code: "Order.AccessDenied",
        message: "You are not authorized to view this order.");

    public static Error InsufficientStockForOrder(List<(Guid VariantId, string Sku, int Requested, int Available)> outOfStockItems)
    {
        var itemDetails = string.Join(", ", outOfStockItems.Select(i =>
            $"{i.Sku} (requested: {i.Requested}, available: {i.Available})"));

        return Error.Validation(
            code: "Order.InsufficientStock",
            message: $"Insufficient stock for the following items: {itemDetails}. Please remove these items from your cart and try again.");
    }

    public static Error StockReductionFailed(Guid variantId) => Error.Failure(
        code: "Order.StockReductionFailed",
        message: $"Failed to reduce stock for variant {variantId}. Please try again.");

    public static Error StockRestorationFailed(Guid variantId) => Error.Failure(
        code: "Order.StockRestorationFailed",
        message: $"Failed to restore stock for variant {variantId}.");
}