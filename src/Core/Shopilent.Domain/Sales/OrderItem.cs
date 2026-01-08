using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Domain.Sales;

public class OrderItem : Entity
{
    private OrderItem()
    {
        // Required by EF Core
    }

    private OrderItem(Order order, Product product, int quantity, Money unitPrice, ProductVariant variant = null)
    {
        OrderId = order.Id;
        ProductId = product.Id;
        VariantId = variant?.Id;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalPrice = unitPrice.Multiply(quantity);

        // Create snapshot of product data
        ProductData = new Dictionary<string, object>
        {
            { "name", product.Name }, { "sku", product.Sku }, { "slug", product.Slug?.Value }
        };

        if (variant != null)
        {
            ProductData["variant_sku"] = variant.Sku;
            ProductData["variant_attributes"] = variant.VariantAttributes;
        }
    }

    // Internal factory method for use by Order aggregate
    internal static OrderItem Create(Order order, Product product, int quantity, Money unitPrice,
        ProductVariant variant = null)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        if (product == null)
            throw new ArgumentNullException(nameof(product));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        if (unitPrice == null || unitPrice.Amount < 0)
            throw new ArgumentException("Unit price must be valid and non-negative", nameof(unitPrice));

        if (variant != null && variant.StockQuantity < quantity)
            throw new ArgumentException(
                $"Insufficient stock. Available: {variant.StockQuantity}, Requested: {quantity}", nameof(quantity));

        return new OrderItem(order, product, quantity, unitPrice, variant);
    }

    // For use by the Order aggregate which should validate inputs
    internal static Result<OrderItem> Create(Result<Order> orderResult, Product product, int quantity, Money unitPrice,
        ProductVariant variant = null)
    {
        if (orderResult.IsFailure)
            return Result.Failure<OrderItem>(orderResult.Error);

        if (product == null)
            return Result.Failure<OrderItem>(ProductErrors.NotFound(Guid.Empty));

        if (quantity <= 0)
            return Result.Failure<OrderItem>(OrderErrors.InvalidQuantity);

        if (unitPrice == null || unitPrice.Amount < 0)
            return Result.Failure<OrderItem>(OrderErrors.NegativeAmount);

        if (variant != null && variant.StockQuantity < quantity)
            return Result.Failure<OrderItem>(ProductVariantErrors.InsufficientStock(quantity, variant.StockQuantity));

        return Result.Success(new OrderItem(orderResult.Value, product, quantity, unitPrice, variant));
    }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? VariantId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money TotalPrice { get; private set; }
    public Dictionary<string, object> ProductData { get; private set; } = new();

    // Internal method for Order aggregate to update quantity
    internal Result UpdateQuantity(int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(OrderErrors.InvalidQuantity);

        var oldQuantity = Quantity;
        Quantity = quantity;
        TotalPrice = UnitPrice.Multiply(quantity);

        return Result.Success();
    }
}
