using Bogus;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class OrderBuilder
{
    private User _user;
    private Address _shippingAddress;
    private Address _billingAddress;
    private Money _subtotal;
    private Money _tax;
    private Money _shippingCost;
    private string _shippingMethod;
    private OrderStatus _status = OrderStatus.Pending;
    private PaymentStatus _paymentStatus = PaymentStatus.Pending;
    private readonly Faker _faker = new();

    public OrderBuilder()
    {
        // Set default values
        var currency = "USD";
        _subtotal = Money.Create(99.99m, currency).Value;
        _tax = Money.Create(8.99m, currency).Value;
        _shippingCost = Money.Create(9.99m, currency).Value;
        _shippingMethod = _faker.Commerce.ProductName() + " Shipping";
    }

    public OrderBuilder WithUser(User user)
    {
        _user = user;
        return this;
    }

    public OrderBuilder WithShippingAddress(Address shippingAddress)
    {
        _shippingAddress = shippingAddress;
        return this;
    }

    public OrderBuilder WithBillingAddress(Address billingAddress)
    {
        _billingAddress = billingAddress;
        return this;
    }

    public OrderBuilder WithSubtotal(decimal amount, string currency = "USD")
    {
        _subtotal = Money.Create(amount, currency).Value;
        return this;
    }

    public OrderBuilder WithTax(decimal amount, string currency = "USD")
    {
        _tax = Money.Create(amount, currency).Value;
        return this;
    }

    public OrderBuilder WithShippingCost(decimal amount, string currency = "USD")
    {
        _shippingCost = Money.Create(amount, currency).Value;
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

    public OrderBuilder AsPaidOrder()
    {
        _status = OrderStatus.Processing;
        _paymentStatus = PaymentStatus.Succeeded;
        return this;
    }

    public OrderBuilder AsShippedOrder()
    {
        _status = OrderStatus.Shipped;
        _paymentStatus = PaymentStatus.Succeeded;
        return this;
    }

    public OrderBuilder AsDeliveredOrder()
    {
        _status = OrderStatus.Delivered;
        _paymentStatus = PaymentStatus.Succeeded;
        return this;
    }

    public OrderBuilder AsCancelledOrder()
    {
        _status = OrderStatus.Cancelled;
        _paymentStatus = PaymentStatus.Canceled;
        return this;
    }

    public static OrderBuilder Random() => new OrderBuilder().WithRandomData();

    public OrderBuilder WithRandomData()
    {
        _subtotal = Money.Create(_faker.Random.Decimal(10, 500), "USD").Value;
        _tax = Money.Create(_subtotal.Amount * 0.08m, "USD").Value;
        _shippingCost = Money.Create(_faker.Random.Decimal(5, 25), "USD").Value;
        _shippingMethod = _faker.PickRandom("Standard", "Express", "Overnight", "Two-Day");
        return this;
    }

    public Order Build()
    {
        // Ensure we have required dependencies
        if (_user == null)
        {
            _user = new UserBuilder().Build();
        }

        if (_shippingAddress == null)
        {
            _shippingAddress = new AddressBuilder().WithUser(_user).Build();
        }

        if (_billingAddress == null)
        {
            _billingAddress = _shippingAddress; // Use same address if not specified
        }

        // Create the order with appropriate factory method
        var orderResult = _status == OrderStatus.Processing && _paymentStatus == PaymentStatus.Succeeded
            ? Order.CreatePaidOrder(_user, _shippingAddress, _billingAddress, _subtotal, _tax, _shippingCost, _shippingMethod)
            : Order.Create(_user, _shippingAddress, _billingAddress, _subtotal, _tax, _shippingCost, _shippingMethod);

        if (orderResult.IsFailure)
            throw new InvalidOperationException($"Failed to create order: {orderResult.Error}");

        var order = orderResult.Value;

        // Apply status changes if different from defaults
        if (_status != OrderStatus.Pending || _paymentStatus != PaymentStatus.Pending)
        {
            // Handle different status combinations
            switch (_status)
            {
                case OrderStatus.Processing when _paymentStatus == PaymentStatus.Succeeded:
                    order.MarkAsPaid();
                    break;
                case OrderStatus.Shipped when _paymentStatus == PaymentStatus.Succeeded:
                    order.MarkAsPaid();
                    order.MarkAsShipped($"TRACK-{_faker.Random.AlphaNumeric(10)}");
                    break;
                case OrderStatus.Delivered when _paymentStatus == PaymentStatus.Succeeded:
                    order.MarkAsPaid();
                    order.MarkAsShipped($"TRACK-{_faker.Random.AlphaNumeric(10)}");
                    order.MarkAsDelivered();
                    break;
                case OrderStatus.Cancelled:
                    order.Cancel("Test cancellation", true); // Use admin privileges for test
                    break;
                default:
                    // For other status combinations, update directly
                    if (_status != order.Status)
                        order.UpdateOrderStatus(_status);
                    if (_paymentStatus != order.PaymentStatus)
                        order.UpdatePaymentStatus(_paymentStatus);
                    break;
            }
        }

        return order;
    }

    public static OrderBuilder Default() => new OrderBuilder();

    public static OrderBuilder WithTestUser(User user) => new OrderBuilder().WithUser(user);

    public static OrderBuilder PaidOrder() => new OrderBuilder().AsPaidOrder();

    public static OrderBuilder ShippedOrder() => new OrderBuilder().AsShippedOrder();

    public static OrderBuilder DeliveredOrder() => new OrderBuilder().AsDeliveredOrder();

    public static OrderBuilder CancelledOrder() => new OrderBuilder().AsCancelledOrder();
}