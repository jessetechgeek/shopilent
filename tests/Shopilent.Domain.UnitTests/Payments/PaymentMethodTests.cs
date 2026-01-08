using FluentAssertions;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.ValueObjects;

namespace Shopilent.Domain.Tests.Payments;

public class PaymentMethodTests
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

    [Fact]
    public void CreateCardMethod_WithValidParameters_ShouldCreateCardMethod()
    {
        // Arrange
        var user = CreateTestUser();
        var provider = PaymentProvider.Stripe;
        var token = "tok_visa_123";
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;
        var isDefault = true;

        // Act
        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            provider,
            token,
            cardDetails,
            isDefault);

        // Assert
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;
        paymentMethod.UserId.Should().Be(user.Id);
        paymentMethod.Type.Should().Be(PaymentMethodType.CreditCard);
        paymentMethod.Provider.Should().Be(provider);
        paymentMethod.Token.Should().Be(token);
        paymentMethod.DisplayName.Should().Be("Visa ending in 4242");
        paymentMethod.CardBrand.Should().Be(cardDetails.Brand);
        paymentMethod.LastFourDigits.Should().Be(cardDetails.LastFourDigits);
        paymentMethod.ExpiryDate.Should().Be(cardDetails.ExpiryDate);
        paymentMethod.IsDefault.Should().Be(isDefault);
        paymentMethod.IsActive.Should().BeTrue();
        paymentMethod.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void CreateCardMethod_WithNullUser_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.Empty;
        var provider = PaymentProvider.Stripe;
        var token = "tok_visa_123";
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        // Act
        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            userId,
            provider,
            token,
            cardDetails);

        // Assert
        paymentMethodResult.IsFailure.Should().BeTrue();
        paymentMethodResult.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public void CreateCardMethod_WithEmptyToken_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var provider = PaymentProvider.Stripe;
        var token = string.Empty;
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        // Act
        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            provider,
            token,
            cardDetails);

        // Assert
        paymentMethodResult.IsFailure.Should().BeTrue();
        paymentMethodResult.Error.Code.Should().Be("PaymentMethod.TokenRequired");
    }

    [Fact]
    public void CreateCardMethod_WithNullCardDetails_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var provider = PaymentProvider.Stripe;
        var token = "tok_visa_123";
        PaymentCardDetails cardDetails = null;

        // Act
        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            provider,
            token,
            cardDetails);

        // Assert
        paymentMethodResult.IsFailure.Should().BeTrue();
        paymentMethodResult.Error.Code.Should().Be("PaymentMethod.InvalidCardDetails");
    }

    [Fact]
    public void CreatePayPalMethod_WithValidParameters_ShouldCreatePayPalMethod()
    {
        // Arrange
        var user = CreateTestUser();
        var token = "paypal_token_123";
        var email = "customer@example.com";
        var isDefault = true;

        // Act
        var paymentMethodResult = PaymentMethod.CreatePayPalMethod(
            user.Id,
            token,
            email,
            isDefault);

        // Assert
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;
        paymentMethod.UserId.Should().Be(user.Id);
        paymentMethod.Type.Should().Be(PaymentMethodType.PayPal);
        paymentMethod.Provider.Should().Be(PaymentProvider.PayPal);
        paymentMethod.Token.Should().Be(token);
        paymentMethod.DisplayName.Should().Be($"PayPal ({email})");
        paymentMethod.CardBrand.Should().BeNull();
        paymentMethod.LastFourDigits.Should().BeNull();
        paymentMethod.ExpiryDate.Should().BeNull();
        paymentMethod.IsDefault.Should().Be(isDefault);
        paymentMethod.IsActive.Should().BeTrue();
        paymentMethod.Metadata.Should().ContainKey("email");
        paymentMethod.Metadata["email"].Should().Be(email);
    }

    [Fact]
    public void UpdateDisplayName_ShouldUpdateName()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var newDisplayName = "My Primary Card";

        // Act
        var updateResult = paymentMethod.UpdateDisplayName(newDisplayName);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        paymentMethod.DisplayName.Should().Be(newDisplayName);
    }

    [Fact]
    public void UpdateDisplayName_WithEmptyValue_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var emptyName = string.Empty;

        // Act
        var updateResult = paymentMethod.UpdateDisplayName(emptyName);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("PaymentMethod.DisplayNameRequired");
    }

    [Fact]
    public void SetDefault_ShouldUpdateIsDefault()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails,
            false);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        paymentMethod.IsDefault.Should().BeFalse();

        // Act
        var updateResult = paymentMethod.SetDefault(true);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        paymentMethod.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Activate_ShouldActivatePaymentMethod()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var deactivateResult = paymentMethod.Deactivate();
        deactivateResult.IsSuccess.Should().BeTrue();
        paymentMethod.IsActive.Should().BeFalse();

        // Act
        var activateResult = paymentMethod.Activate();

        // Assert
        activateResult.IsSuccess.Should().BeTrue();
        paymentMethod.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_ShouldDeactivatePaymentMethod()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        paymentMethod.IsActive.Should().BeTrue();

        // Act
        var deactivateResult = paymentMethod.Deactivate();

        // Assert
        deactivateResult.IsSuccess.Should().BeTrue();
        paymentMethod.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpdateToken_ShouldUpdateToken()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var newToken = "tok_visa_456";

        // Act
        var updateResult = paymentMethod.UpdateToken(newToken);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        paymentMethod.Token.Should().Be(newToken);
    }

    [Fact]
    public void UpdateToken_WithEmptyValue_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var emptyToken = string.Empty;

        // Act
        var updateResult = paymentMethod.UpdateToken(emptyToken);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("PaymentMethod.TokenRequired");
    }

    [Fact]
    public void UpdateCardDetails_ShouldUpdateCardDetails()
    {
        // Arrange
        var user = CreateTestUser();
        var oldCardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        oldCardDetailsResult.IsSuccess.Should().BeTrue();
        var oldCardDetails = oldCardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            oldCardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var newCardDetailsResult = PaymentCardDetails.Create("Mastercard", "5678", DateTime.UtcNow.AddYears(2));
        newCardDetailsResult.IsSuccess.Should().BeTrue();
        var newCardDetails = newCardDetailsResult.Value;

        // Act
        var updateResult = paymentMethod.UpdateCardDetails(newCardDetails);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        paymentMethod.CardBrand.Should().Be(newCardDetails.Brand);
        paymentMethod.LastFourDigits.Should().Be(newCardDetails.LastFourDigits);
        paymentMethod.ExpiryDate.Should().Be(newCardDetails.ExpiryDate);
        paymentMethod.DisplayName.Should().Be($"{newCardDetails.Brand} ending in {newCardDetails.LastFourDigits}");
    }

    [Fact]
    public void UpdateCardDetails_WithNullDetails_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        PaymentCardDetails newCardDetails = null;

        // Act
        var updateResult = paymentMethod.UpdateCardDetails(newCardDetails);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("PaymentMethod.InvalidCardDetails");
    }

    [Fact]
    public void UpdateCardDetails_WithNonCardMethod_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var paypalMethodResult = PaymentMethod.CreatePayPalMethod(
            user.Id,
            "paypal_token_123",
            "customer@example.com");
        paypalMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paypalMethodResult.Value;

        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        // Act
        var updateResult = paymentMethod.UpdateCardDetails(cardDetails);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("PaymentMethod.InvalidCardDetails");
    }

    [Fact]
    public void UpdateMetadata_ShouldAddOrUpdateValue()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var key = "billing_zip";
        var value = "90210";

        // Act
        var updateResult = paymentMethod.UpdateMetadata(key, value);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        paymentMethod.Metadata.Should().ContainKey(key);
        paymentMethod.Metadata[key].Should().Be(value);
    }

    [Fact]
    public void UpdateMetadata_WithEmptyKey_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var cardDetailsResult = PaymentCardDetails.Create("Visa", "4242", DateTime.UtcNow.AddYears(1));
        cardDetailsResult.IsSuccess.Should().BeTrue();
        var cardDetails = cardDetailsResult.Value;

        var paymentMethodResult = PaymentMethod.CreateCardMethod(
            user.Id,
            PaymentProvider.Stripe,
            "tok_visa_123",
            cardDetails);
        paymentMethodResult.IsSuccess.Should().BeTrue();
        var paymentMethod = paymentMethodResult.Value;

        var emptyKey = string.Empty;
        var value = "test";

        // Act
        var updateResult = paymentMethod.UpdateMetadata(emptyKey, value);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("PaymentMethod.InvalidMetadataKey");
    }
}
