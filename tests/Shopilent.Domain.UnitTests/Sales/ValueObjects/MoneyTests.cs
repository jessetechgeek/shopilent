using FluentAssertions;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Sales.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateMoney()
    {
        // Arrange
        var amount = 100m;
        var currency = "USD";

        // Act
        var result = Money.Create(amount, currency);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var money = result.Value;
        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldReturnFailure()
    {
        // Arrange
        var amount = -10m;
        var currency = "USD";

        // Act
        var result = Money.Create(amount, currency);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NegativeAmount");
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldReturnFailure()
    {
        // Arrange
        var amount = 100m;
        var currency = string.Empty;

        // Act
        var result = Money.Create(amount, currency);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.InvalidCurrency");
    }

    [Fact]
    public void FromDollars_ShouldCreateMoneyWithUSD()
    {
        // Arrange
        var amount = 99.99m;

        // Act
        var result = Money.FromDollars(amount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var money = result.Value;
        money.Amount.Should().Be(amount);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void FromEuros_ShouldCreateMoneyWithEUR()
    {
        // Arrange
        var amount = 99.99m;

        // Act
        var result = Money.FromEuros(amount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var money = result.Value;
        money.Amount.Should().Be(amount);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Zero_ShouldCreateZeroMoney()
    {
        // Act
        var money = Money.Zero();

        // Assert
        money.Amount.Should().Be(0m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Zero_WithCurrency_ShouldCreateZeroMoneyWithCurrency()
    {
        // Arrange
        var currency = "EUR";

        // Act
        var money = Money.Zero(currency);

        // Assert
        money.Amount.Should().Be(0m);
        money.Currency.Should().Be(currency);
    }

    [Fact]
    public void Add_ShouldAddAmounts()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromDollars(50);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;
        
        var expected = 150m;

        // Act
        var result = money1.Add(money2);

        // Assert
        result.Amount.Should().Be(expected);
        result.Currency.Should().Be(money1.Currency);
    }

    [Fact]
    public void Add_WithDifferentCurrencies_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromEuros(50);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;

        // Act & Assert
        Action act = () => money1.Add(money2);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddSafe_WithSameCurrency_ShouldAddAmounts()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromDollars(50);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;
        
        var expected = 150m;

        // Act
        var result = money1.AddSafe(money2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(expected);
        result.Value.Currency.Should().Be(money1.Currency);
    }

    [Fact]
    public void AddSafe_WithDifferentCurrencies_ShouldReturnFailure()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromEuros(50);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;

        // Act
        var result = money1.AddSafe(money2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.CurrencyMismatch");
    }

    [Fact]
    public void Subtract_ShouldSubtractAmounts()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromDollars(30);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;
        
        var expected = 70m;

        // Act
        var result = money1.Subtract(money2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(expected);
        result.Value.Currency.Should().Be(money1.Currency);
    }

    [Fact]
    public void Subtract_WithDifferentCurrencies_ShouldReturnFailure()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromEuros(50);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;

        // Act
        var result = money1.Subtract(money2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.CurrencyMismatch");
    }

    [Fact]
    public void Subtract_ResultingInNegative_ShouldReturnFailure()
    {
        // Arrange
        var money1Result = Money.FromDollars(30);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromDollars(50);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;

        // Act
        var result = money1.Subtract(money2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NegativeAmount");
    }

    [Fact]
    public void Multiply_ShouldMultiplyAmount()
    {
        // Arrange
        var moneyResult = Money.FromDollars(10);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;
        
        var multiplier = 3.5m;
        var expected = 35m;

        // Act
        var result = money.Multiply(multiplier);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(expected);
        result.Value.Currency.Should().Be(money.Currency);
    }

    [Fact]
    public void Multiply_WithNegativeMultiplier_ShouldReturnFailure()
    {
        // Arrange
        var moneyResult = Money.FromDollars(10);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;
        
        var multiplier = -2m;

        // Act
        var result = money.Multiply(multiplier);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NegativeAmount");
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromDollars(100);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;

        // Act & Assert
        money1.Equals(money2).Should().BeTrue();
        (money1 == money2).Should().BeTrue();
        (money1 != money2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentAmounts_ShouldReturnFalse()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromDollars(50);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;

        // Act & Assert
        money1.Equals(money2).Should().BeFalse();
        (money1 == money2).Should().BeFalse();
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentCurrencies_ShouldReturnFalse()
    {
        // Arrange
        var money1Result = Money.FromDollars(100);
        money1Result.IsSuccess.Should().BeTrue();
        var money1 = money1Result.Value;
        
        var money2Result = Money.FromEuros(100);
        money2Result.IsSuccess.Should().BeTrue();
        var money2 = money2Result.Value;

        // Act & Assert
        money1.Equals(money2).Should().BeFalse();
        (money1 == money2).Should().BeFalse();
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var moneyResult = Money.FromDollars(123.45m);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;
        
        var expected = "123.45 USD";

        // Act
        var result = money.ToString();

        // Assert
        result.Should().Be(expected);
    }
}