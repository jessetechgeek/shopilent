using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Application.UnitTests.Testing.Builders;

public class OrderBuilder
{
    private Guid _id = Guid.NewGuid();
    private User _user;
    private Guid? _userId = Guid.NewGuid();
    private Address _shippingAddress;
    private Address _billingAddress;
    private decimal _subtotal = 100.00m;
    private decimal _tax = 10.00m;
    private decimal _shippingCost = 5.00m;
    private string _currency = "USD";
    private string _shippingMethod = "Standard";
    private OrderStatus _status = OrderStatus.Pending;
    private PaymentStatus _paymentStatus = PaymentStatus.Pending;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;
    private readonly Dictionary<string, object> _metadata = new();

    public OrderBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public OrderBuilder WithUser(User user)
    {
        _user = user;
        _userId = user?.Id;
        return this;
    }

    public OrderBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public OrderBuilder WithShippingAddress(Address address)
    {
        _shippingAddress = address;
        return this;
    }

    public OrderBuilder WithBillingAddress(Address address)
    {
        _billingAddress = address;
        return this;
    }

    public OrderBuilder WithPricing(decimal subtotal, decimal tax, decimal shippingCost, string currency = "USD")
    {
        _subtotal = subtotal;
        _tax = tax;
        _shippingCost = shippingCost;
        _currency = currency;
        return this;
    }

    public OrderBuilder WithShippingMethod(string shippingMethod)
    {
        _shippingMethod = shippingMethod;
        return this;
    }

    public OrderBuilder WithStatus(OrderStatus status)
    {
        _status = status;
        return this;
    }

    public OrderBuilder WithPaymentStatus(PaymentStatus paymentStatus)
    {
        _paymentStatus = paymentStatus;
        return this;
    }

    public OrderBuilder CreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public OrderBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public Order Build()
    {
        var subtotal = Money.Create(_subtotal, _currency);
        var tax = Money.Create(_tax, _currency);
        var shippingCost = Money.Create(_shippingCost, _currency);

        if (subtotal.IsFailure)
            throw new InvalidOperationException($"Invalid subtotal: {_subtotal} {_currency}");
        if (tax.IsFailure)
            throw new InvalidOperationException($"Invalid tax: {_tax} {_currency}");
        if (shippingCost.IsFailure)
            throw new InvalidOperationException($"Invalid shipping cost: {_shippingCost} {_currency}");

        // Create default user if not provided
        if (_user == null && _userId.HasValue)
        {
            _user = new UserBuilder().WithId(_userId.Value).Build();
        }

        // Create default addresses if not provided
        if (_shippingAddress == null)
        {
            _shippingAddress = new AddressBuilder().WithUser(_user).WithAddressType(Domain.Shipping.Enums.AddressType.Shipping).Build();
        }

        if (_billingAddress == null)
        {
            _billingAddress = new AddressBuilder().WithUser(_user).WithAddressType(Domain.Shipping.Enums.AddressType.Billing).Build();
        }

        var orderResult = Order.Create(
            _user.Id,
            _shippingAddress.Id,
            _billingAddress.Id,
            subtotal.Value,
            tax.Value,
            shippingCost.Value,
            _shippingMethod);

        if (orderResult.IsFailure)
            throw new InvalidOperationException($"Failed to create order: {orderResult.Error.Message}");

        var order = orderResult.Value;

        // Use reflection to set private properties
        SetPrivatePropertyValue(order, "Id", _id);
        SetPrivatePropertyValue(order, "CreatedAt", _createdAt);
        SetPrivatePropertyValue(order, "UpdatedAt", _updatedAt);

        if (_userId.HasValue && _user == null)
        {
            SetPrivatePropertyValue(order, "UserId", _userId.Value);
        }

        // Set metadata
        foreach (var metadata in _metadata)
        {
            order.Metadata[metadata.Key] = metadata.Value;
        }

        // Set status if different from default
        if (_status != OrderStatus.Pending)
        {
            // Use reflection since status changes might have complex logic
            SetPrivatePropertyValue(order, "Status", _status);
        }

        if (_paymentStatus != PaymentStatus.Pending)
        {
            SetPrivatePropertyValue(order, "PaymentStatus", _paymentStatus);
        }

        return order;
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
