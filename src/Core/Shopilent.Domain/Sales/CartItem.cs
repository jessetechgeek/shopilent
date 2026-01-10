using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Domain.Sales;

public class CartItem : Entity
{
    private CartItem()
    {
        // Required by EF Core
    }

    private CartItem(Guid cartId, Guid productId, int quantity = 1, Guid? variantId = null)
    {
        CartId = cartId;
        ProductId = productId;
        VariantId = variantId;
        Quantity = quantity;
    }

    // Static factory method for internal use by Cart aggregate
    internal static CartItem Create(Guid cartId, Guid productId, int quantity = 1, Guid? variantId = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
        return new CartItem(cartId, productId, quantity, variantId);
    }

    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? VariantId { get; private set; }
    public int Quantity { get; private set; }

    // Internal method for Cart aggregate to update quantity
    internal Result UpdateQuantity(int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(CartErrors.InvalidQuantity);

        Quantity = quantity;
        return Result.Success();
    }
}
