using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Sales;

public class OrderItem : Entity
{
    private OrderItem()
    {
        // Required by EF Core
    }

    private OrderItem(
        Order order,
        Guid productId,
        Guid? variantId,
        int quantity,
        Money unitPrice,
        ProductSnapshot productSnapshot)
    {
        OrderId = order.Id;
        ProductId = productId;
        VariantId = variantId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalPrice = unitPrice.Multiply(quantity);

        // Store the snapshot
        ProductData = productSnapshot.ToDictionary();
    }

    // Internal factory method for use by Order aggregate
    internal static Result<OrderItem> Create(
        Order order,
        Guid productId,
        Guid? variantId,
        int quantity,
        Money unitPrice,
        ProductSnapshot productSnapshot)
    {
        if (order == null)
            return Result.Failure<OrderItem>(OrderErrors.OrderRequired);

        if (productId == Guid.Empty)
            return Result.Failure<OrderItem>(OrderErrors.ProductIdRequired);

        if (quantity <= 0)
            return Result.Failure<OrderItem>(OrderErrors.InvalidQuantity);

        if (unitPrice == null || unitPrice.Amount < 0)
            return Result.Failure<OrderItem>(OrderErrors.NegativeAmount);

        if (productSnapshot == null)
            return Result.Failure<OrderItem>(OrderErrors.ProductSnapshotRequired);

        return Result.Success(new OrderItem(order, productId, variantId, quantity, unitPrice, productSnapshot));
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
