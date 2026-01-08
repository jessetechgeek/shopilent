using FluentAssertions;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Specifications;
using Shopilent.Domain.Payments.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Tests.Payments.Specifications;

public class PaymentSpecificationsTests
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
            user,
            address,
            address,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        return orderResult.Value;
    }

    [Fact]
    public void ActivePaymentMethodSpecification_WithActiveMethod_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetailsResult.Value);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var specification = new ActivePaymentMethodSpecification();

        // Act
        var result = specification.IsSatisfiedBy(paymentMethod);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ActivePaymentMethodSpecification_WithInactiveMethod_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetailsResult.Value);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var deactivateResult = paymentMethod.Deactivate();
        deactivateResult.IsSuccess.Should().BeTrue();

        var specification = new ActivePaymentMethodSpecification();

        // Act
        var result = specification.IsSatisfiedBy(paymentMethod);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DefaultPaymentMethodSpecification_WithDefaultMethod_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetailsResult.Value,
            isDefault: true);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var specification = new DefaultPaymentMethodSpecification();

        // Act
        var result = specification.IsSatisfiedBy(paymentMethod);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DefaultPaymentMethodSpecification_WithNonDefaultMethod_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetailsResult.Value,
            isDefault: false);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var specification = new DefaultPaymentMethodSpecification();

        // Act
        var result = specification.IsSatisfiedBy(paymentMethod);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void PaymentByOrderSpecification_WithMatchingOrder_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order,
            user,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var specification = new PaymentByOrderSpecification(order.Id);

        // Act
        var result = specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void PaymentByOrderSpecification_WithDifferentOrder_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var differentOrderId = Guid.NewGuid();

        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order,
            user,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var specification = new PaymentByOrderSpecification(differentOrderId);

        // Act
        var result = specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SuccessfulPaymentSpecification_WithSuccessfulPayment_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order,
            user,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var successResult = payment.MarkAsSucceeded("txn_123");
        successResult.IsSuccess.Should().BeTrue();

        var specification = new SuccessfulPaymentSpecification();

        // Act
        var result = specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SuccessfulPaymentSpecification_WithPendingPayment_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order,
            user,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        // Still pending

        var specification = new SuccessfulPaymentSpecification();

        // Act
        var result = specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SuccessfulPaymentSpecification_WithFailedPayment_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();

        var paymentResult = Payment.Create(
            order,
            user,
            amountResult.Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var failedResult = payment.MarkAsFailed("Payment failed");
        failedResult.IsSuccess.Should().BeTrue();

        var specification = new SuccessfulPaymentSpecification();

        // Act
        var result = specification.IsSatisfiedBy(payment);

        // Assert
        result.Should().BeFalse();
    }
}
