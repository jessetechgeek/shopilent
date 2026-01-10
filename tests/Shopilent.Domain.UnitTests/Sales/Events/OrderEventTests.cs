using FluentAssertions;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;
using Shopilent.Domain.Tests.Common;

namespace Shopilent.Domain.Tests.Sales.Events;

public class OrderEventTests
{
    private User CreateTestUser()
    {
        var emailResult = Email.Create("test@example.com");
        emailResult.IsSuccess.Should().BeTrue();

        var fullNameResult = FullName.Create("Test", "User");
        fullNameResult.IsSuccess.Should().BeTrue();

        var userResult = User.Create(
            emailResult.Value,
            "hashed_password",
            fullNameResult.Value);

        userResult.IsSuccess.Should().BeTrue();
        return userResult.Value;
    }

    private Address CreateTestAddress(User user)
    {
        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345");
        postalAddressResult.IsSuccess.Should().BeTrue();

        var addressResult = Address.CreateShipping(
            user.Id,
            postalAddressResult.Value);

        addressResult.IsSuccess.Should().BeTrue();
        return addressResult.Value;
    }

    [Fact]
    public void Order_WhenCreated_ShouldRaiseOrderCreatedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        // Act
        var orderResult = Order.Create(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        // Assert
        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreatedEvent);
        var domainEvent = order.DomainEvents.First(e => e is OrderCreatedEvent);
        var createdEvent = (OrderCreatedEvent)domainEvent;
        createdEvent.OrderId.Should().Be(order.Id);
    }

    [Fact]
    public void Order_WhenStatusChanged_ShouldRaiseOrderStatusChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.ClearDomainEvents(); // Clear the creation event

        // Act
        var updateResult = order.UpdateOrderStatus(OrderStatus.Processing);
        updateResult.IsSuccess.Should().BeTrue();

        // Assert
        order.DomainEvents.Should().ContainSingle(e => e is OrderStatusChangedEvent);
        var domainEvent = order.DomainEvents.First(e => e is OrderStatusChangedEvent);
        var statusEvent = (OrderStatusChangedEvent)domainEvent;
        statusEvent.OrderId.Should().Be(order.Id);
        statusEvent.OldStatus.Should().Be(OrderStatus.Pending);
        statusEvent.NewStatus.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public void Order_WhenPaymentStatusChanged_ShouldRaiseOrderPaymentStatusChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.ClearDomainEvents(); // Clear the creation event

        // Act
        var updateResult = order.UpdatePaymentStatus(PaymentStatus.Succeeded);
        updateResult.IsSuccess.Should().BeTrue();

        // Assert
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaymentStatusChangedEvent);
        var domainEvent = order.DomainEvents.First(e => e is OrderPaymentStatusChangedEvent);
        var paymentEvent = (OrderPaymentStatusChangedEvent)domainEvent;
        paymentEvent.OrderId.Should().Be(order.Id);
        paymentEvent.OldStatus.Should().Be(PaymentStatus.Pending);
        paymentEvent.NewStatus.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public void Order_WhenMarkedAsPaid_ShouldRaiseOrderPaidEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.ClearDomainEvents(); // Clear the creation event

        // Act
        var paidResult = order.MarkAsPaid();
        paidResult.IsSuccess.Should().BeTrue();

        // Assert
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidEvent);
        var domainEvent = order.DomainEvents.First(e => e is OrderPaidEvent);
        var paidEvent = (OrderPaidEvent)domainEvent;
        paidEvent.OrderId.Should().Be(order.Id);

        // Also check status events are raised
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaymentStatusChangedEvent);
        order.DomainEvents.Should().ContainSingle(e => e is OrderStatusChangedEvent);
    }

    [Fact]
    public void Order_WhenMarkedAsShipped_ShouldRaiseOrderShippedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.ClearDomainEvents(); // Clear previous events

        // Act
        var shippedResult = order.MarkAsShipped("TRACK123");
        shippedResult.IsSuccess.Should().BeTrue();

        // Assert
        order.DomainEvents.Should().ContainSingle(e => e is OrderShippedEvent);
        var domainEvent = order.DomainEvents.First(e => e is OrderShippedEvent);
        var shippedEvent = (OrderShippedEvent)domainEvent;
        shippedEvent.OrderId.Should().Be(order.Id);
    }

    [Fact]
    public void Order_WhenMarkedAsDelivered_ShouldRaiseOrderDeliveredEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();

        order.ClearDomainEvents(); // Clear previous events

        // Act
        var deliveredResult = order.MarkAsDelivered();
        deliveredResult.IsSuccess.Should().BeTrue();

        // Assert
        order.DomainEvents.Should().ContainSingle(e => e is OrderDeliveredEvent);
        var domainEvent = order.DomainEvents.First(e => e is OrderDeliveredEvent);
        var deliveredEvent = (OrderDeliveredEvent)domainEvent;
        deliveredEvent.OrderId.Should().Be(order.Id);
    }

    [Fact]
    public void Order_WhenCancelled_ShouldRaiseOrderCancelledEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.ClearDomainEvents(); // Clear the creation event

        // Act
        var cancelResult = order.Cancel("Customer request");
        cancelResult.IsSuccess.Should().BeTrue();

        // Assert
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
        var domainEvent = order.DomainEvents.First(e => e is OrderCancelledEvent);
        var cancelledEvent = (OrderCancelledEvent)domainEvent;
        cancelledEvent.OrderId.Should().Be(order.Id);
    }
}
