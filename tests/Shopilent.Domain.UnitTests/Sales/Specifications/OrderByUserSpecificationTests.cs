using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Specifications;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Tests.Sales.Specifications;

public class OrderByUserSpecificationTests
{
    private User CreateTestUser(string email = "test@example.com")
    {
        var emailResult = Email.Create(email);
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
    public void IsSatisfiedBy_WithOrderBySpecifiedUser_ShouldReturnTrue()
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

        var specification = new OrderByUserSpecification(user.Id);

        // Act
        var result = specification.IsSatisfiedBy(order);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithOrderByDifferentUser_ShouldReturnFalse()
    {
        // Arrange
        var user1 = CreateTestUser("user1@example.com");
        var user2 = CreateTestUser("user2@example.com");
        var address = CreateTestAddress(user1);

        var subtotalResult = Money.FromDollars(100);
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.FromDollars(10);
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.FromDollars(5);
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user1.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var specification = new OrderByUserSpecification(user2.Id);

        // Act
        var result = specification.IsSatisfiedBy(order);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithOrderWithoutUser_ShouldReturnFalse()
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
            Guid.Empty,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var specification = new OrderByUserSpecification(user.Id);

        // Act
        var result = specification.IsSatisfiedBy(order);

        // Assert
        result.Should().BeFalse();
    }
}
