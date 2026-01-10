using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Events;

namespace Shopilent.Domain.Sales;

public class Cart : AggregateRoot
{
    private Cart()
    {
        // Required by EF Core
    }

    private Cart(Guid? userId = null)
    {
        UserId = userId;
        Metadata = new Dictionary<string, object>();
        _items = new List<CartItem>();
    }

    public static Result<Cart> Create(Guid? userId = null)
    {
        var cart = new Cart(userId);
        cart.AddDomainEvent(new CartCreatedEvent(cart.Id));
        return Result.Success(cart);
    }

    public static Result<Cart> CreateWithMetadata(Guid userId, Dictionary<string, object> metadata)
    {
        if (userId == Guid.Empty)
            return Result.Failure<Cart>(CartErrors.InvalidUserId);

        if (metadata == null)
            return Result.Failure<Cart>(CartErrors.InvalidMetadata);

        var result = Create(userId);
        if (result.IsFailure)
            return result;

        var cart = result.Value;
        foreach (var item in metadata)
        {
            cart.Metadata[item.Key] = item.Value;
        }

        return Result.Success(cart);
    }

    public Guid? UserId { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

    private readonly List<CartItem> _items = new();
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public Result AssignToUser(Guid userId)
    {
        if (userId == Guid.Empty)
            return Result.Failure<Cart>(CartErrors.InvalidUserId);

        UserId = userId;
        AddDomainEvent(new CartAssignedToUserEvent(Id, userId));
        return Result.Success();
    }

    public Result<CartItem> AddItem(Guid productId, int quantity = 1, Guid? variantId = null)
    {
        if (productId == Guid.Empty)
            return Result.Failure<CartItem>(CartErrors.InvalidProductId);

        if (quantity <= 0)
            return Result.Failure<CartItem>(CartErrors.InvalidQuantity);

        // Check if the item already exists
        var existingItem = _items.Find(i =>
            i.ProductId == productId &&
            ((variantId == null && i.VariantId == null) || i.VariantId == variantId));

        if (existingItem != null)
        {
            var updateResult = existingItem.UpdateQuantity(existingItem.Quantity + quantity);
            if (updateResult.IsFailure)
                return Result.Failure<CartItem>(updateResult.Error);

            AddDomainEvent(new CartItemUpdatedEvent(Id, existingItem.Id));
            return Result.Success(existingItem);
        }

        var item = CartItem.Create(Id, productId, quantity, variantId);
        _items.Add(item);

        AddDomainEvent(new CartItemAddedEvent(Id, item.Id));
        return Result.Success(item);
    }

    public Result UpdateItemQuantity(Guid itemId, int quantity)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item == null)
            return Result.Failure(CartErrors.ItemNotFound(itemId));

        if (quantity <= 0)
        {
            return RemoveItem(itemId);
        }

        var updateResult = item.UpdateQuantity(quantity);
        if (updateResult.IsFailure)
            return updateResult;

        AddDomainEvent(new CartItemUpdatedEvent(Id, itemId));
        return Result.Success();
    }

    public Result RemoveItem(Guid itemId)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item == null)
            return Result.Failure(CartErrors.ItemNotFound(itemId));

        _items.Remove(item);
        AddDomainEvent(new CartItemRemovedEvent(Id, itemId));
        return Result.Success();
    }

    public Result Clear()
    {
        _items.Clear();
        AddDomainEvent(new CartClearedEvent(Id));
        return Result.Success();
    }

    public Result UpdateMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(CartErrors.InvalidMetadataKey);

        Metadata[key] = value;
        return Result.Success();
    }

    public Result MarkAsExpired()
    {
        AddDomainEvent(new CartExpiredEvent(Id));
        return Result.Success();
    }
}
