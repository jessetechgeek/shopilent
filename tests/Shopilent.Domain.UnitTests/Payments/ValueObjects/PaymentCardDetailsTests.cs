using FluentAssertions;
using Shopilent.Domain.Payments.ValueObjects;

namespace Shopilent.Domain.Tests.Payments.ValueObjects;

public class PaymentCardDetailsTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateCardDetails()
    {
        // Arrange
        var brand = "Visa";
        var lastFourDigits = "4242";
        var expiryDate = DateTime.UtcNow.AddYears(1);

        // Act
        var cardDetailsResult = PaymentCardDetails.Create(brand, lastFourDigits, expiryDate);

        // Assert
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;
        cardDetails.Brand.Should().Be(brand);
        cardDetails.LastFourDigits.Should().Be(lastFourDigits);
        cardDetails.ExpiryDate.Should().Be(expiryDate);
    }

    [Fact]
    public void Create_WithEmptyBrand_ShouldReturnFailure()
    {
        // Arrange
        var brand = string.Empty;
        var lastFourDigits = "4242";
        var expiryDate = DateTime.UtcNow.AddYears(1);

        // Act
        var cardDetailsResult = PaymentCardDetails.Create(brand, lastFourDigits, expiryDate);

        // Assert
        cardDetailsResult.IsFailure.Should().BeTrue();
        cardDetailsResult.Error.Code.Should().Be("PaymentMethod.InvalidCardDetails");
    }

    [Fact]
    public void Create_WithInvalidLastFourDigits_ShouldReturnFailure()
    {
        // Arrange
        var brand = "Visa";
        var lastFourDigits = "123"; // Not 4 digits
        var expiryDate = DateTime.UtcNow.AddYears(1);

        // Act
        var cardDetailsResult = PaymentCardDetails.Create(brand, lastFourDigits, expiryDate);

        // Assert
        cardDetailsResult.IsFailure.Should().BeTrue();
        cardDetailsResult.Error.Code.Should().Be("PaymentMethod.InvalidCardDetails");
    }

    [Fact]
    public void Create_WithNonDigitLastFour_ShouldReturnFailure()
    {
        // Arrange
        var brand = "Visa";
        var lastFourDigits = "123A"; // Contains a letter
        var expiryDate = DateTime.UtcNow.AddYears(1);

        // Act
        var cardDetailsResult = PaymentCardDetails.Create(brand, lastFourDigits, expiryDate);

        // Assert
        cardDetailsResult.IsFailure.Should().BeTrue();
        cardDetailsResult.Error.Code.Should().Be("PaymentMethod.InvalidCardDetails");
    }

    [Fact]
    public void Create_WithPastExpiryDate_ShouldReturnFailure()
    {
        // Arrange
        var brand = "Visa";
        var lastFourDigits = "4242";
        var expiryDate = DateTime.UtcNow.AddYears(-1); // Expired

        // Act
        var cardDetailsResult = PaymentCardDetails.Create(brand, lastFourDigits, expiryDate);

        // Assert
        cardDetailsResult.IsFailure.Should().BeTrue();
        cardDetailsResult.Error.Code.Should().Be("PaymentMethod.ExpiredCard");
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var expiryDate = new DateTime(2030, 12, 31); // Use fixed date for comparison
        var detailsResult1 = PaymentCardDetails.Create("Visa", "4242", expiryDate);
        var detailsResult2 = PaymentCardDetails.Create("Visa", "4242", expiryDate);

        detailsResult1.IsSuccess.Should().BeTrue();
        detailsResult2.IsSuccess.Should().BeTrue();

        var details1 = detailsResult1.Value;
        var details2 = detailsResult2.Value;

        // Act & Assert
        details1.Equals(details2).Should().BeTrue();
        (details1 == details2).Should().BeTrue();
        (details1 != details2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentBrand_ShouldReturnFalse()
    {
        // Arrange
        var expiryDate = new DateTime(2040, 12, 31);
        var detailsResult1 = PaymentCardDetails.Create("Visa", "4242", expiryDate);
        var detailsResult2 = PaymentCardDetails.Create("Mastercard", "4242", expiryDate);

        detailsResult1.IsSuccess.Should().BeTrue();
        detailsResult2.IsSuccess.Should().BeTrue();

        var details1 = detailsResult1.Value;
        var details2 = detailsResult2.Value;

        // Act & Assert
        details1.Equals(details2).Should().BeFalse();
        (details1 == details2).Should().BeFalse();
        (details1 != details2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentLastFour_ShouldReturnFalse()
    {
        // Arrange
        var expiryDate = new DateTime(2030, 12, 31);
        var detailsResult1 = PaymentCardDetails.Create("Visa", "4242", expiryDate);
        var detailsResult2 = PaymentCardDetails.Create("Visa", "5555", expiryDate);

        detailsResult1.IsSuccess.Should().BeTrue();
        detailsResult2.IsSuccess.Should().BeTrue();

        var details1 = detailsResult1.Value;
        var details2 = detailsResult2.Value;

        // Act & Assert
        details1.Equals(details2).Should().BeFalse();
        (details1 == details2).Should().BeFalse();
        (details1 != details2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentExpiryDate_ShouldReturnFalse()
    {
        // Arrange
        var detailsResult1 = PaymentCardDetails.Create("Visa", "4242", new DateTime(2030, 12, 31));
        var detailsResult2 = PaymentCardDetails.Create("Visa", "4242", new DateTime(2031, 12, 31));

        detailsResult1.IsSuccess.Should().BeTrue();
        detailsResult2.IsSuccess.Should().BeTrue();

        var details1 = detailsResult1.Value;
        var details2 = detailsResult2.Value;

        // Act & Assert
        details1.Equals(details2).Should().BeFalse();
        (details1 == details2).Should().BeFalse();
        (details1 != details2).Should().BeTrue();
    }
}
