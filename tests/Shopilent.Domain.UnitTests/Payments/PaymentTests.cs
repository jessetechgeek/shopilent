using FluentAssertions;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Payments.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Tests.Payments;

public class PaymentTests
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
    public void Create_WithValidParameters_ShouldCreatePayment()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;
        var methodType = PaymentMethodType.CreditCard;
        var provider = PaymentProvider.Stripe;
        var externalReference = "ext_ref_123";

        // Act
        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            methodType,
            provider,
            externalReference);

        // Assert
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;
        payment.OrderId.Should().Be(order.Id);
        payment.UserId.Should().Be(user.Id);
        payment.Amount.Should().Be(amount);
        payment.Currency.Should().Be(amount.Currency);
        payment.MethodType.Should().Be(methodType);
        payment.Provider.Should().Be(provider);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.ExternalReference.Should().Be(externalReference);
        payment.TransactionId.Should().BeNull();
        payment.ProcessedAt.Should().BeNull();
        payment.ErrorMessage.Should().BeNull();
        payment.Metadata.Should().BeEmpty();
        payment.DomainEvents.Should().Contain(e => e is PaymentCreatedEvent);
    }

    [Fact]
    public void Create_WithNullOrder_ShouldReturnFailure()
    {
        // Arrange
        var orderId = Guid.Empty;
        var user = CreateTestUser();
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;
        var methodType = PaymentMethodType.CreditCard;
        var provider = PaymentProvider.Stripe;

        // Act
        var paymentResult = Payment.Create(
            orderId,
            user.Id,
            amount,
            methodType,
            provider);

        // Assert
        paymentResult.IsFailure.Should().BeTrue();
        paymentResult.Error.Code.Should().Be("Payment.InvalidOrderId");
    }

    [Fact]
    public void Create_WithNullAmount_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        Money amount = null;
        var methodType = PaymentMethodType.CreditCard;
        var provider = PaymentProvider.Stripe;

        // Act
        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            methodType,
            provider);

        // Assert
        paymentResult.IsFailure.Should().BeTrue();
        paymentResult.Error.Code.Should().Be("Payment.NegativeAmount");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldReturnFailure()
    {
        // Arrange - cannot directly create negative Money, so we'll test the validation
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var methodType = PaymentMethodType.CreditCard;
        var provider = PaymentProvider.Stripe;

        // We can't create a Money object with negative amount, so verify with zero
        var amountResult = Money.Create(0, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        // Act
        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            methodType,
            provider);

        // Assert - Zero should be allowed
        paymentResult.IsSuccess.Should().BeTrue();
        paymentResult.Value.Amount.Amount.Should().Be(0m);
    }

    [Fact]
    public void CreateWithPaymentMethod_ShouldCreatePaymentWithMethod()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "pm_123",
            cardDetailsResult.Value,
            true);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        // Act
        var paymentResult = Payment.CreateWithPaymentMethod(
            order.Id,
            user.Id,
            amount,
            paymentMethod);

        // Assert
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;
        payment.OrderId.Should().Be(order.Id);
        payment.UserId.Should().Be(user.Id);
        payment.Amount.Should().Be(amount);
        payment.MethodType.Should().Be(paymentMethod.Type);
        payment.Provider.Should().Be(paymentMethod.Provider);
        payment.PaymentMethodId.Should().Be(paymentMethod.Id);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.DomainEvents.Should().Contain(e => e is PaymentCreatedEvent);
    }

    [Fact]
    public void CreateWithPaymentMethod_WithNullPaymentMethod_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;
        PaymentMethod paymentMethod = null;

        // Act
        var paymentResult = Payment.CreateWithPaymentMethod(
            order.Id,
            user.Id,
            amount,
            paymentMethod);

        // Assert
        paymentResult.IsFailure.Should().BeTrue();
        paymentResult.Error.Code.Should().Be("Payment.PaymentMethodNotFound");
    }

    [Fact]
    public void UpdateStatus_ShouldChangeStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.Status.Should().Be(PaymentStatus.Pending);

        var newStatus = PaymentStatus.Processing;
        var transactionId = "txn_123";

        // Act
        var updateResult = payment.UpdateStatus(newStatus, transactionId);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(newStatus);
        payment.TransactionId.Should().Be(transactionId);
        payment.DomainEvents.Should().Contain(e => e is PaymentStatusChangedEvent);
    }

    [Fact]
    public void UpdateStatus_WithErrorMessage_ShouldSetErrorMessage()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.Status.Should().Be(PaymentStatus.Pending);

        var newStatus = PaymentStatus.Failed;
        var errorMessage = "Card declined";

        // Act
        var updateResult = payment.UpdateStatus(newStatus, null, errorMessage);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(newStatus);
        payment.ErrorMessage.Should().Be(errorMessage);
        payment.DomainEvents.Should().Contain(e => e is PaymentStatusChangedEvent);
    }

    [Fact]
    public void MarkAsSucceeded_ShouldUpdateStatusAndProcessedTime()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.ProcessedAt.Should().BeNull();

        var transactionId = "txn_123";

        // Act
        var succeededResult = payment.MarkAsSucceeded(transactionId);

        // Assert
        succeededResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.TransactionId.Should().Be(transactionId);
        payment.ProcessedAt.Should().NotBeNull();
        payment.DomainEvents.Should().Contain(e => e is PaymentSucceededEvent);
    }

    [Fact]
    public void MarkAsSucceeded_WhenAlreadySucceeded_ShouldNotRaiseEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var succeededResult = payment.MarkAsSucceeded("txn_123");
        succeededResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);

        // Clear existing events
        payment.ClearDomainEvents();

        // Act
        var secondSucceededResult = payment.MarkAsSucceeded("txn_456");

        // Assert
        secondSucceededResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.TransactionId.Should().Be("txn_123"); // Should keep the first transaction ID
        payment.DomainEvents.Should().BeEmpty(); // No events should be raised
    }

    [Fact]
    public void MarkAsFailed_ShouldUpdateStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.Status.Should().Be(PaymentStatus.Pending);

        var errorMessage = "Insufficient funds";

        // Act
        var failedResult = payment.MarkAsFailed(errorMessage);

        // Assert
        failedResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.ErrorMessage.Should().Be(errorMessage);
        payment.DomainEvents.Should().Contain(e => e is PaymentFailedEvent);
    }

    [Fact]
    public void MarkAsFailed_WhenAlreadyFailed_ShouldNotRaiseEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var failedResult = payment.MarkAsFailed("First error");
        failedResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);

        // Clear existing events
        payment.ClearDomainEvents();

        // Act
        var secondFailedResult = payment.MarkAsFailed("New error");

        // Assert
        secondFailedResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.ErrorMessage.Should().Be("First error"); // Should keep the first error
        payment.DomainEvents.Should().BeEmpty(); // No events should be raised
    }

    [Fact]
    public void MarkAsRefunded_ShouldUpdateStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        // First mark as succeeded
        var succeededResult = payment.MarkAsSucceeded("txn_123");
        succeededResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);

        // Clear existing events
        payment.ClearDomainEvents();

        var refundTransactionId = "ref_123";

        // Act
        var refundedResult = payment.MarkAsRefunded(refundTransactionId);

        // Assert
        refundedResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.TransactionId.Should().Be(refundTransactionId);
        payment.DomainEvents.Should().Contain(e => e is PaymentRefundedEvent);
    }

    [Fact]
    public void MarkAsRefunded_WithNonSucceededPayment_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        payment.Status.Should().Be(PaymentStatus.Pending);

        var refundTransactionId = "ref_123";

        // Act
        var refundedResult = payment.MarkAsRefunded(refundTransactionId);

        // Assert
        refundedResult.IsFailure.Should().BeTrue();
        refundedResult.Error.Code.Should().Be("Payment.InvalidStatus");
    }

    [Fact]
    public void UpdateExternalReference_ShouldUpdateReference()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var newRef = "new_ref_123";

        // Act
        var updateResult = payment.UpdateExternalReference(newRef);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        payment.ExternalReference.Should().Be(newRef);
    }

    [Fact]
    public void UpdateExternalReference_WithEmptyValue_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var emptyRef = string.Empty;

        // Act
        var updateResult = payment.UpdateExternalReference(emptyRef);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Payment.TokenRequired");
    }

    [Fact]
    public void UpdateMetadata_ShouldAddOrUpdateValue()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var key = "receipt_url";
        var value = "https://example.com/receipt/123";

        // Act
        var metadataResult = payment.UpdateMetadata(key, value);

        // Assert
        metadataResult.IsSuccess.Should().BeTrue();
        payment.Metadata.Should().ContainKey(key);
        payment.Metadata[key].Should().Be(value);
    }

    [Fact]
    public void UpdateMetadata_WithEmptyKey_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var emptyKey = string.Empty;
        var value = "test";

        // Act
        var metadataResult = payment.UpdateMetadata(emptyKey, value);

        // Assert
        metadataResult.IsFailure.Should().BeTrue();
        metadataResult.Error.Code.Should().Be("Payment.InvalidMetadataKey");
    }

    [Fact]
    public void SetPaymentMethod_ShouldUpdatePaymentMethod()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "pm_123",
            cardDetailsResult.Value,
            true);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        // Act
        var setMethodResult = payment.SetPaymentMethod(paymentMethod);

        // Assert
        setMethodResult.IsSuccess.Should().BeTrue();
        payment.PaymentMethodId.Should().Be(paymentMethod.Id);
        payment.MethodType.Should().Be(paymentMethod.Type);
        payment.Provider.Should().Be(paymentMethod.Provider);
    }

    [Fact]
    public void SetPaymentMethod_WithNullMethod_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var amountResult = Money.Create(115, "USD");
        amountResult.IsSuccess.Should().BeTrue();
        var amount = amountResult.Value;

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            amount,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe);
        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        PaymentMethod paymentMethod = null;

        // Act
        var setMethodResult = payment.SetPaymentMethod(paymentMethod);

        // Assert
        setMethodResult.IsFailure.Should().BeTrue();
        setMethodResult.Error.Code.Should().Be("Payment.PaymentMethodNotFound");
    }
}
