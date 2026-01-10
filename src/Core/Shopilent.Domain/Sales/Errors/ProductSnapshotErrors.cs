using Shopilent.Domain.Common.Errors;

namespace Shopilent.Domain.Sales.Errors;

public static class ProductSnapshotErrors
{
    public static Error NameRequired => Error.Validation(
        code: "ProductSnapshot.NameRequired",
        message: "Product name is required.");

    public static Error SkuRequired => Error.Validation(
        code: "ProductSnapshot.SkuRequired",
        message: "Product SKU is required.");

    public static Error InvalidData => Error.Validation(
        code: "ProductSnapshot.InvalidData",
        message: "Product snapshot data is null or invalid.");
}
