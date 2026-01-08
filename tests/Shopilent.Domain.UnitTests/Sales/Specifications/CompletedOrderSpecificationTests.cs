using FluentAssertions;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Specifications;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Tests.Sales.Specifications;

public class CompletedOrderSpecificationTests
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
    public void IsSatisfiedBy_WithDeliveredOrder_ShouldReturnTrue()
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
            user,
            address,
            address,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();

        var deliveredResult = order.MarkAsDelivered();
        deliveredResult.IsSuccess.Should().BeTrue();

        var specification = new CompletedOrderSpecification();

        // Act
        var result = specification.IsSatisfiedBy(order);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithProcessingOrder_ShouldReturnFalse()
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
            user,
            address,
            address,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var paidResult = order.MarkAsPaid();
        paidResult.IsSuccess.Should().BeTrue();

        var specification = new CompletedOrderSpecification();

        // Act
        var result = specification.IsSatisfiedBy(order);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithShippedOrder_ShouldReturnFalse()
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
            user,
            address,
            address,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();

        var specification = new CompletedOrderSpecification();

        // Act
        var result = specification.IsSatisfiedBy(order);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithCancelledOrder_ShouldReturnFalse()
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
            user,
            address,
            address,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var cancelResult = order.Cancel();
        cancelResult.IsSuccess.Should().BeTrue();

        var specification = new CompletedOrderSpecification();

        // Act
        var result = specification.IsSatisfiedBy(order);

        // Assert
        result.Should().BeFalse();
    }
}
