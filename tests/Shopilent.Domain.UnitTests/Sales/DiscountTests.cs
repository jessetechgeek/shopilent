using FluentAssertions;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Sales;

public class DiscountTests
{
    [Fact]
    public void CreatePercentage_WithValidPercentage_ShouldCreateDiscount()
    {
        // Arrange
        var percentage = 15.5m;
        var code = "SUMMER15";

        // Act
        var result = Discount.CreatePercentage(percentage, code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var discount = result.Value;
        discount.Value.Should().Be(percentage);
        discount.Type.Should().Be(DiscountType.Percentage);
        discount.Code.Should().Be(code);
    }

    [Fact]
    public void CreatePercentage_WithoutCode_ShouldCreateDiscount()
    {
        // Arrange
        var percentage = 20m;

        // Act
        var result = Discount.CreatePercentage(percentage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var discount = result.Value;
        discount.Value.Should().Be(percentage);
        discount.Type.Should().Be(DiscountType.Percentage);
        discount.Code.Should().BeNull();
    }

    [Fact]
    public void CreatePercentage_WithNegativePercentage_ShouldReturnFailure()
    {
        // Arrange
        var percentage = -10m;

        // Act
        var result = Discount.CreatePercentage(percentage);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NegativeDiscount");
    }

    [Fact]
    public void CreatePercentage_WithOverHundredPercentage_ShouldReturnFailure()
    {
        // Arrange
        var percentage = 110m;

        // Act
        var result = Discount.CreatePercentage(percentage);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.InvalidDiscountPercentage");
    }

    [Fact]
    public void CreateFixedAmount_WithValidAmount_ShouldCreateDiscount()
    {
        // Arrange
        var amount = 25m;
        var code = "25OFF";

        // Act
        var result = Discount.CreateFixedAmount(amount, code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var discount = result.Value;
        discount.Value.Should().Be(amount);
        discount.Type.Should().Be(DiscountType.FixedAmount);
        discount.Code.Should().Be(code);
    }

    [Fact]
    public void CreateFixedAmount_WithoutCode_ShouldCreateDiscount()
    {
        // Arrange
        var amount = 10m;

        // Act
        var result = Discount.CreateFixedAmount(amount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var discount = result.Value;
        discount.Value.Should().Be(amount);
        discount.Type.Should().Be(DiscountType.FixedAmount);
        discount.Code.Should().BeNull();
    }

    [Fact]
    public void CreateFixedAmount_WithNegativeAmount_ShouldReturnFailure()
    {
        // Arrange
        var amount = -5m;

        // Act
        var result = Discount.CreateFixedAmount(amount);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NegativeDiscount");
    }

    [Fact]
    public void CalculateDiscount_WithPercentageDiscount_ShouldCalculateCorrectValue()
    {
        // Arrange
        var baseAmountResult = Money.FromDollars(100);
        baseAmountResult.IsSuccess.Should().BeTrue();
        var baseAmount = baseAmountResult.Value;

        var discountResult = Discount.CreatePercentage(15);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var calculatedResult = discount.CalculateDiscount(baseAmount);

        // Assert
        calculatedResult.IsSuccess.Should().BeTrue();
        var calculatedDiscount = calculatedResult.Value;
        calculatedDiscount.Amount.Should().Be(15m);
        calculatedDiscount.Currency.Should().Be(baseAmount.Currency);
    }

    [Fact]
    public void CalculateDiscount_WithFixedAmountDiscount_ShouldCalculateCorrectValue()
    {
        // Arrange
        var baseAmountResult = Money.FromDollars(100);
        baseAmountResult.IsSuccess.Should().BeTrue();
        var baseAmount = baseAmountResult.Value;

        var discountResult = Discount.CreateFixedAmount(25);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var calculatedResult = discount.CalculateDiscount(baseAmount);

        // Assert
        calculatedResult.IsSuccess.Should().BeTrue();
        var calculatedDiscount = calculatedResult.Value;
        calculatedDiscount.Amount.Should().Be(25m);
        calculatedDiscount.Currency.Should().Be(baseAmount.Currency);
    }

    [Fact]
    public void CalculateDiscount_WithFixedAmountGreaterThanBaseAmount_ShouldCapAtBaseAmount()
    {
        // Arrange
        var baseAmountResult = Money.FromDollars(50);
        baseAmountResult.IsSuccess.Should().BeTrue();
        var baseAmount = baseAmountResult.Value;

        var discountResult = Discount.CreateFixedAmount(75);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var calculatedResult = discount.CalculateDiscount(baseAmount);

        // Assert
        calculatedResult.IsSuccess.Should().BeTrue();
        var calculatedDiscount = calculatedResult.Value;
        calculatedDiscount.Amount.Should().Be(50m); // Capped at base amount
        calculatedDiscount.Currency.Should().Be(baseAmount.Currency);
    }

    [Fact]
    public void CalculateDiscount_WithNullBaseAmount_ShouldReturnFailure()
    {
        // Arrange
        Money baseAmount = null;

        var discountResult = Discount.CreatePercentage(15);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var calculatedResult = discount.CalculateDiscount(baseAmount);

        // Assert
        calculatedResult.IsFailure.Should().BeTrue();
        calculatedResult.Error.Code.Should().Be("Order.InvalidAmount");
    }

    [Fact]
    public void ApplyDiscount_WithPercentageDiscount_ShouldReturnDiscountedAmount()
    {
        // Arrange
        var baseAmountResult = Money.FromDollars(100);
        baseAmountResult.IsSuccess.Should().BeTrue();
        var baseAmount = baseAmountResult.Value;

        var discountResult = Discount.CreatePercentage(15);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var discountedResult = discount.ApplyDiscount(baseAmount);

        // Assert
        discountedResult.IsSuccess.Should().BeTrue();
        var discountedAmount = discountedResult.Value;
        discountedAmount.Amount.Should().Be(85m); // $100 - 15%
        discountedAmount.Currency.Should().Be(baseAmount.Currency);
    }

    [Fact]
    public void ApplyDiscount_WithFixedAmountDiscount_ShouldReturnDiscountedAmount()
    {
        // Arrange
        var baseAmountResult = Money.FromDollars(100);
        baseAmountResult.IsSuccess.Should().BeTrue();
        var baseAmount = baseAmountResult.Value;

        var discountResult = Discount.CreateFixedAmount(25);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var discountedResult = discount.ApplyDiscount(baseAmount);

        // Assert
        discountedResult.IsSuccess.Should().BeTrue();
        var discountedAmount = discountedResult.Value;
        discountedAmount.Amount.Should().Be(75m); // $100 - $25
        discountedAmount.Currency.Should().Be(baseAmount.Currency);
    }

    [Fact]
    public void ApplyDiscount_WithFixedAmountGreaterThanBaseAmount_ShouldReturnZero()
    {
        // Arrange
        var baseAmountResult = Money.FromDollars(50);
        baseAmountResult.IsSuccess.Should().BeTrue();
        var baseAmount = baseAmountResult.Value;

        var discountResult = Discount.CreateFixedAmount(75);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var discountedResult = discount.ApplyDiscount(baseAmount);

        // Assert
        discountedResult.IsSuccess.Should().BeTrue();
        var discountedAmount = discountedResult.Value;
        discountedAmount.Amount.Should().Be(0m); // $50 - $75 = $0 (not negative)
        discountedAmount.Currency.Should().Be(baseAmount.Currency);
    }

    [Fact]
    public void ToString_WithPercentageDiscount_ShouldFormatCorrectly()
    {
        // Arrange
        var percentage = 15.5m;
        var discountResult = Discount.CreatePercentage(percentage);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;
        var expected = "15.5%";

        // Act
        var result = discount.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToString_WithFixedAmountDiscount_ShouldFormatCorrectly()
    {
        // Arrange
        var amount = 25m;
        var discountResult = Discount.CreateFixedAmount(amount);
        discountResult.IsSuccess.Should().BeTrue();
        var discount = discountResult.Value;

        // Act
        var result = discount.ToString();

        // The exact format will depend on the current culture's currency format
        // So we just check that it contains the amount
        result.Should().Contain("25");
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var discount1Result = Discount.CreatePercentage(15, "SUMMER15");
        var discount2Result = Discount.CreatePercentage(15, "SUMMER15");

        discount1Result.IsSuccess.Should().BeTrue();
        discount2Result.IsSuccess.Should().BeTrue();

        var discount1 = discount1Result.Value;
        var discount2 = discount2Result.Value;

        // Act & Assert
        discount1.Should().Be(discount2);
    }
}