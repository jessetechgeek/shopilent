using Shopilent.Domain.Identity;
using Shopilent.Domain.Sales;

namespace Shopilent.Application.UnitTests.Testing.Builders;

public class CartBuilder
{
    private Guid _id = Guid.NewGuid();
    private User _user;
    private Guid? _userId = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;
    private readonly Dictionary<string, object> _metadata = new();

    public CartBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public CartBuilder WithUser(User user)
    {
        _user = user;
        _userId = user?.Id;
        return this;
    }

    public CartBuilder WithUserId(Guid? userId)
    {
        _userId = userId;
        return this;
    }

    public CartBuilder CreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public CartBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public Cart Build()
    {
        var cartResult = Cart.Create(_user?.Id);

        if (cartResult.IsFailure)
            throw new InvalidOperationException($"Failed to create cart: {cartResult.Error.Message}");

        var cart = cartResult.Value;

        // Use reflection to set private properties
        SetPrivatePropertyValue(cart, "Id", _id);
        SetPrivatePropertyValue(cart, "CreatedAt", _createdAt);
        SetPrivatePropertyValue(cart, "UpdatedAt", _updatedAt);

        if (_userId.HasValue && _user == null)
        {
            SetPrivatePropertyValue(cart, "UserId", _userId.Value);
        }

        // Set metadata
        foreach (var metadata in _metadata)
        {
            cart.Metadata[metadata.Key] = metadata.Value;
        }

        return cart;
    }

    private static void SetPrivatePropertyValue<T>(object obj, string propertyName, T value)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName);
        if (propertyInfo != null)
        {
            propertyInfo.SetValue(obj, value, null);
        }
        else
        {
            var fieldInfo = obj.GetType().GetField(propertyName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
            }
            else
            {
                throw new InvalidOperationException($"Property or field {propertyName} not found on type {obj.GetType().Name}");
            }
        }
    }
}
