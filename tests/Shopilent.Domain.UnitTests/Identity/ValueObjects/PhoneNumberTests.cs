using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Domain.Tests.Identity.ValueObjects;

public class PhoneNumberTests
{
    [Fact]
    public void Create_WithValidPhoneNumber_ShouldCreatePhoneNumber()
    {
        // Arrange
        var phoneNumberStr = "+1-555-123-4567";
        var expected = "+15551234567"; // Only digits and + retained

        // Act
        var result = PhoneNumber.Create(phoneNumberStr);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Fact]
    public void Create_WithEmptyPhoneNumber_ShouldReturnFailure()
    {
        // Arrange
        var phoneNumberStr = string.Empty;

        // Act
        var result = PhoneNumber.Create(phoneNumberStr);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.PhoneRequired");
    }

    [Fact]
    public void Create_WithWhitespacePhoneNumber_ShouldReturnFailure()
    {
        // Arrange
        var phoneNumberStr = "   ";

        // Act
        var result = PhoneNumber.Create(phoneNumberStr);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.PhoneRequired");
    }

    [Fact]
    public void Create_WithShortPhoneNumber_ShouldReturnFailure()
    {
        // Arrange
        var phoneNumberStr = "12345"; // Less than 7 digits

        // Act
        var result = PhoneNumber.Create(phoneNumberStr);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.InvalidPhoneFormat");
    }

    [Fact]
    public void Create_WithSpecialCharacters_ShouldRemoveThem()
    {
        // Arrange
        var phoneNumberStr = "(555) 123-4567";
        var expected = "5551234567";

        // Act
        var result = PhoneNumber.Create(phoneNumberStr);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Fact]
    public void Create_WithLeadingPlus_ShouldPreserveIt()
    {
        // Arrange
        var phoneNumberStr = "+1 555 123 4567";
        var expected = "+15551234567";

        // Act
        var result = PhoneNumber.Create(phoneNumberStr);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Fact]
    public void Create_WithLetters_ShouldRemoveThemAndReturnFailureIfTooShort()
    {
        // Arrange
        var phoneNumberStr = "555-CALL-NOW";

        // Act
        var result = PhoneNumber.Create(phoneNumberStr);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.InvalidPhoneFormat");
    }

    [Fact]
    public void Create_WithValidFormattedNumber_ShouldNormalizeFormat()
    {
        // Arrange
        var phoneNumberVariations = new[]
        {
            "+1 (555) 123-4567",
            "+1.555.123.4567",
            "+1-555-123-4567",
            "1-555-123-4567"
        };
        var expected = "+15551234567"; // With leading plus
        var expectedWithoutPlus = "15551234567"; // Without leading plus

        // Act & Assert
        foreach (var number in phoneNumberVariations)
        {
            var result = PhoneNumber.Create(number);
            result.IsSuccess.Should().BeTrue();
            
            if (number.StartsWith("+"))
            {
                result.Value.Value.Should().Be(expected);
            }
            else
            {
                result.Value.Value.Should().Be(expectedWithoutPlus);
            }
        }
    }

    [Fact]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var result1 = PhoneNumber.Create("+1-555-123-4567");
        var result2 = PhoneNumber.Create("+1 (555) 123-4567");
        
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        
        var phoneNumber1 = result1.Value;
        var phoneNumber2 = result2.Value;

        // Act & Assert
        phoneNumber1.Equals(phoneNumber2).Should().BeTrue();
        (phoneNumber1 == phoneNumber2).Should().BeTrue();
        (phoneNumber1 != phoneNumber2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentValue_ShouldReturnFalse()
    {
        // Arrange
        var result1 = PhoneNumber.Create("+1-555-123-4567");
        var result2 = PhoneNumber.Create("+1-555-123-4568");
        
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        
        var phoneNumber1 = result1.Value;
        var phoneNumber2 = result2.Value;

        // Act & Assert
        phoneNumber1.Equals(phoneNumber2).Should().BeFalse();
        (phoneNumber1 == phoneNumber2).Should().BeFalse();
        (phoneNumber1 != phoneNumber2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        // Arrange
        var phoneNumberStr = "+1-555-123-4567";
        var expected = "+15551234567";
        var result = PhoneNumber.Create(phoneNumberStr);
        result.IsSuccess.Should().BeTrue();
        var phoneNumber = result.Value;

        // Act
        var stringResult = phoneNumber.ToString();

        // Assert
        stringResult.Should().Be(expected);
    }
}