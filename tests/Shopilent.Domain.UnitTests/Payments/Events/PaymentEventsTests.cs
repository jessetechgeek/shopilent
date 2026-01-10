using FluentAssertions;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Tests.Payments.Events;

public class PaymentEventsTests
{
    private User CreateTestUser()
    {
        var emailResult = Email.Create("test@example.com");
        var fullNameResult = FullName.Create("Test", "User");
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

    private Order CreateTestOrder(User user, Address address)
    {
        var subtotalResult = Money.Create(100, "USD");
        var taxResult = Money.Create(10, "USD");
        var shippingCostResult = Money.Create(5, "USD");

        subtotalResult.IsSuccess.Should().BeTrue();
        taxResult.IsSuccess.Should().BeTrue();
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        return orderResult.Value;
    }

    [Fact]
    public void Payment_WhenCreated_ShouldRaisePaymentCreatedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);

        // Act
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);

        // Assert
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;
        var domainEvent = payment.DomainEvents.Should().ContainSingle(e => e is PaymentCreatedEvent).Subject;
        var createdEvent = (PaymentCreatedEvent)domainEvent;
        createdEvent.PaymentId.Should().Be(payment.Id);
    }

    [Fact]
    public void Payment_WhenStatusChanged_ShouldRaisePaymentStatusChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.ClearDomainEvents(); // Clear the creation event

        var oldStatus = payment.Status;
        var newStatus = PaymentStatus.Processing;

        // Act
        var updateResult = payment.UpdateStatus(newStatus);
        updateResult.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = payment.DomainEvents.Should().ContainSingle(e => e is PaymentStatusChangedEvent).Subject;
        var statusEvent = (PaymentStatusChangedEvent)domainEvent;
        statusEvent.PaymentId.Should().Be(payment.Id);
        statusEvent.OldStatus.Should().Be(oldStatus);
        statusEvent.NewStatus.Should().Be(newStatus);
    }

    [Fact]
    public void Payment_WhenMarkedAsSucceeded_ShouldRaisePaymentSucceededEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.ClearDomainEvents(); // Clear the creation event
        var transactionId = "txn_123";

        // Act
        var succeededResult = payment.MarkAsSucceeded(transactionId);
        succeededResult.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = payment.DomainEvents.Should().ContainSingle(e => e is PaymentSucceededEvent).Subject;
        var succeededEvent = (PaymentSucceededEvent)domainEvent;
        succeededEvent.PaymentId.Should().Be(payment.Id);
        succeededEvent.OrderId.Should().Be(order.Id);
    }

    [Fact]
    public void Payment_WhenMarkedAsFailed_ShouldRaisePaymentFailedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.ClearDomainEvents(); // Clear the creation event
        var errorMessage = "Card declined";

        // Act
        var failedResult = payment.MarkAsFailed(errorMessage);
        failedResult.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = payment.DomainEvents.Should().ContainSingle(e => e is PaymentFailedEvent).Subject;
        var failedEvent = (PaymentFailedEvent)domainEvent;
        failedEvent.PaymentId.Should().Be(payment.Id);
        failedEvent.OrderId.Should().Be(order.Id);
        failedEvent.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void Payment_WhenMarkedAsRefunded_ShouldRaisePaymentRefundedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        // First mark as succeeded
        var succeededResult = payment.MarkAsSucceeded("txn_123");
        succeededResult.IsSuccess.Should().BeTrue();

        payment.ClearDomainEvents(); // Clear previous events
        var refundId = "ref_123";

        // Act
        var refundedResult = payment.MarkAsRefunded(refundId);
        refundedResult.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = payment.DomainEvents.Should().ContainSingle(e => e is PaymentRefundedEvent).Subject;
        var refundedEvent = (PaymentRefundedEvent)domainEvent;
        refundedEvent.PaymentId.Should().Be(payment.Id);
        refundedEvent.OrderId.Should().Be(order.Id);
    }
}
