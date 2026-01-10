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

namespace Shopilent.Domain.Tests.Payments;

public class PaymentRefundTests
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

    private Payment CreateTestPayment(Order order, User user)
    {
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

        // Mark as succeeded before refund tests
        var succeededResult = payment.MarkAsSucceeded("txn_123");
        succeededResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);

        payment.ClearDomainEvents(); // Clear previous events
        return payment;
    }

    [Fact]
    public void MarkAsRefunded_WithSucceededPayment_ShouldUpdateStatusAndRaiseEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var payment = CreateTestPayment(order, user);
        var refundTransactionId = "ref_123";

        // Act
        var refundResult = payment.MarkAsRefunded(refundTransactionId);

        // Assert
        refundResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.TransactionId.Should().Be(refundTransactionId);

        var domainEvent = payment.DomainEvents.Should().ContainSingle(e => e is PaymentRefundedEvent).Subject;
        var refundedEvent = (PaymentRefundedEvent)domainEvent;
        refundedEvent.PaymentId.Should().Be(payment.Id);
        refundedEvent.OrderId.Should().Be(order.Id);
    }

    [Fact]
    public void MarkAsRefunded_WithPendingPayment_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);

        // Create payment but don't mark as succeeded
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

        var refundTransactionId = "ref_123";

        // Act
        var refundResult = payment.MarkAsRefunded(refundTransactionId);

        // Assert
        refundResult.IsFailure.Should().BeTrue();
        refundResult.Error.Code.Should().Be("Payment.InvalidStatus");
        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void MarkAsRefunded_WithEmptyTransactionId_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var payment = CreateTestPayment(order, user);
        var emptyTransactionId = string.Empty;

        // Act
        var refundResult = payment.MarkAsRefunded(emptyTransactionId);

        // Assert
        refundResult.IsFailure.Should().BeTrue();
        refundResult.Error.Code.Should().Be("Payment.TokenRequired");
        payment.Status.Should().Be(PaymentStatus.Succeeded); // Unchanged
    }

    [Fact]
    public void MarkAsRefunded_WhenAlreadyRefunded_ShouldNotRaiseEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var payment = CreateTestPayment(order, user);

        // First refund
        var firstResult = payment.MarkAsRefunded("ref_123");
        firstResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);

        payment.ClearDomainEvents(); // Clear events from first refund

        // Act - attempt second refund
        var secondResult = payment.MarkAsRefunded("ref_456");

        // Assert
        secondResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.TransactionId.Should().Be("ref_123"); // Original transaction ID should be preserved
        payment.DomainEvents.Should().BeEmpty(); // No events should be raised
    }
}
