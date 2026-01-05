using Shopilent.Domain.Common.Errors;

namespace Shopilent.Domain.Catalog.Errors;

public static class ProductErrors
{
    public static Error NameRequired => Error.Validation(
        code: "Product.NameRequired",
        message: "Product name cannot be empty.");

    public static Error SlugRequired => Error.Validation(
        code: "Product.SlugRequired",
        message: "Product slug cannot be empty.");

    public static Error NegativePrice => Error.Validation(
        code: "Product.NegativePrice",
        message: "Product price cannot be negative.");

    public static Error DuplicateSlug(string slug) => Error.Conflict(
        code: "Product.DuplicateSlug",
        message: $"A product with slug '{slug}' already exists.");

    public static Error DuplicateSku(string sku) => Error.Conflict(
        code: "Product.DuplicateSku",
        message: $"A product with SKU '{sku}' already exists.");

    public static Error NotFound(Guid id) => Error.NotFound(
        code: "Product.NotFound",
        message: $"Product with ID {id} was not found.");

    public static Error NotFoundBySlug(string slug) => Error.NotFound(
        code: "Product.NotFound",
        message: $"Product with slug '{slug}' was not found.");

    public static Error InactiveProduct => Error.Validation(
        code: "Product.Inactive",
        message: "Cannot perform operation on inactive product.");
    
    public static Error InvalidMetadataKey => Error.Validation(
        code: "Product.InvalidMetadataKey",
        message: "Metadata key cannot be empty.");
}