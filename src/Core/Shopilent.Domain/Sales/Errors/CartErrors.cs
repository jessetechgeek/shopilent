using Shopilent.Domain.Common.Errors;

namespace Shopilent.Domain.Sales.Errors;

public static class CartErrors
{
    public static Error ItemNotFound(Guid itemId) => Error.NotFound(
        code: "Cart.ItemNotFound",
        message: $"Cart item with ID {itemId} was not found.");

    public static Error InvalidQuantity => Error.Validation(
        code: "Cart.InvalidQuantity",
        message: "Cart item quantity must be positive.");

    public static Error EmptyCart => Error.Validation(
        code: "Cart.Empty",
        message: "Cannot perform operation on an empty cart.");

    public static Error ProductUnavailable(Guid productId) => Error.Validation(
        code: "Cart.ProductUnavailable",
        message: $"Product with ID {productId} is not available for purchase.");

    public static Error CartNotFound(Guid id) => Error.NotFound(
        code: "Cart.NotFound",
        message: $"Cart with ID {id} was not found.");

    public static Error CartExpired => Error.Validation(
        code: "Cart.Expired",
        message: "The shopping cart has expired. Please start a new session.");

    public static Error ProductVariantNotFound(Guid variantId) => Error.NotFound(
        code: "Cart.ProductVariantNotFound",
        message: $"Product variant with ID {variantId} was not found.");

    public static Error InvalidMetadataKey => Error.Validation(
        code: "Cart.InvalidMetadataKey",
        message: "Metadata key cannot be empty.");

    public static Error ProductVariantNotAvailable(Guid variantId) => Error.Validation(
        code: "Cart.ProductVariantNotAvailable",
        message: $"Product variant with ID {variantId} is not available for purchase.");

    public static Error InvalidMetadata => Error.Validation(
        code: "Cart.InvalidMetadata",
        message: "The provided metadata is invalid.");

    public static Error InvalidUserId => Error.Validation(
        code: "Cart.InvalidUserId",
        message: "User ID cannot be empty.");

    public static Error InvalidProductId => Error.Validation(
        code: "Cart.InvalidProductId",
        message: "Product ID cannot be empty.");
}
