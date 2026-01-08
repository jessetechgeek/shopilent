using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Events;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Tests.Shipping;

public class AddressTests
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

    [Fact]
    public void Create_WithValidParameters_ShouldCreateAddress()
    {
        // Arrange
        var user = CreateTestUser();

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345",
            "Apt 4B");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        var addressType = AddressType.Shipping;

        var phoneResult = PhoneNumber.Create("555-123-4567");
        phoneResult.IsSuccess.Should().BeTrue();
        var phone = phoneResult.Value;

        var isDefault = true;

        // Act - Use CreateShipping instead of internal Create method
        var result = Address.CreateShipping(
            user.Id,
            postalAddress,
            phone,
            isDefault);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var address = result.Value;
        address.UserId.Should().Be(user.Id);
        address.PostalAddress.Should().Be(postalAddress);
        address.AddressLine1.Should().Be(postalAddress.AddressLine1);
        address.AddressLine2.Should().Be(postalAddress.AddressLine2);
        address.City.Should().Be(postalAddress.City);
        address.State.Should().Be(postalAddress.State);
        address.Country.Should().Be(postalAddress.Country);
        address.PostalCode.Should().Be(postalAddress.PostalCode);
        address.Phone.Should().Be(phone);
        address.AddressType.Should().Be(addressType);
        address.IsDefault.Should().Be(isDefault);
        address.DomainEvents.Should().Contain(e => e is AddressCreatedEvent);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.Empty;

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        // Act - Use CreateShipping instead of internal Create method
        var result = Address.CreateShipping(userId, postalAddress);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Address.InvalidUserId");
    }

    [Fact]
    public void Create_WithNullPostalAddress_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        PostalAddress postalAddress = null;

        // Act - Use CreateShipping instead of internal Create method
        var result = Address.CreateShipping(user.Id, postalAddress);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Address.AddressLine1Required");
    }

    [Fact]
    public void CreateShipping_ShouldCreateShippingAddress()
    {
        // Arrange
        var user = CreateTestUser();

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345",
            "Suite 100");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        var phoneResult = PhoneNumber.Create("555-123-4567");
        phoneResult.IsSuccess.Should().BeTrue();
        var phone = phoneResult.Value;

        var isDefault = true;

        // Act
        var result = Address.CreateShipping(
            user.Id,
            postalAddress,
            phone,
            isDefault);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var address = result.Value;
        address.UserId.Should().Be(user.Id);
        address.PostalAddress.Should().Be(postalAddress);
        address.Phone.Should().Be(phone);
        address.AddressType.Should().Be(AddressType.Shipping);
        address.IsDefault.Should().Be(isDefault);
        address.DomainEvents.Should().Contain(e => e is AddressCreatedEvent);
    }

    [Fact]
    public void CreateBilling_ShouldCreateBillingAddress()
    {
        // Arrange
        var user = CreateTestUser();

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345",
            "Suite 100");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        var phoneResult = PhoneNumber.Create("555-123-4567");
        phoneResult.IsSuccess.Should().BeTrue();
        var phone = phoneResult.Value;

        var isDefault = true;

        // Act
        var result = Address.CreateBilling(
            user.Id,
            postalAddress,
            phone,
            isDefault);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var address = result.Value;
        address.UserId.Should().Be(user.Id);
        address.PostalAddress.Should().Be(postalAddress);
        address.Phone.Should().Be(phone);
        address.AddressType.Should().Be(AddressType.Billing);
        address.IsDefault.Should().Be(isDefault);
        address.DomainEvents.Should().Contain(e => e is AddressCreatedEvent);
    }

    [Fact]
    public void CreateBoth_WithIsDefaultTrue_ShouldCreateDefaultBothAddress()
    {
        // Arrange
        var user = CreateTestUser();

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345",
            "Suite 100");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        var phoneResult = PhoneNumber.Create("555-123-4567");
        phoneResult.IsSuccess.Should().BeTrue();
        var phone = phoneResult.Value;

        var isDefault = true;

        // Act
        var result = Address.CreateBoth(
            user.Id,
            postalAddress,
            phone,
            isDefault);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var address = result.Value;
        address.UserId.Should().Be(user.Id);
        address.PostalAddress.Should().Be(postalAddress);
        address.Phone.Should().Be(phone);
        address.AddressType.Should().Be(AddressType.Both);
        address.IsDefault.Should().BeTrue();
        address.DomainEvents.Should().Contain(e => e is AddressCreatedEvent);
    }

    [Fact]
    public void CreateBoth_WithIsDefaultFalse_ShouldCreateNonDefaultBothAddress()
    {
        // Arrange
        var user = CreateTestUser();

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345",
            "Suite 100");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        var phoneResult = PhoneNumber.Create("555-123-4567");
        phoneResult.IsSuccess.Should().BeTrue();
        var phone = phoneResult.Value;

        var isDefault = false;

        // Act
        var result = Address.CreateBoth(
            user.Id,
            postalAddress,
            phone,
            isDefault);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var address = result.Value;
        address.UserId.Should().Be(user.Id);
        address.PostalAddress.Should().Be(postalAddress);
        address.Phone.Should().Be(phone);
        address.AddressType.Should().Be(AddressType.Both);
        address.IsDefault.Should().BeFalse();
        address.DomainEvents.Should().Contain(e => e is AddressCreatedEvent);
    }

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateAddress()
    {
        // Arrange
        var user = CreateTestUser();

        var originalPostalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345");

        originalPostalAddressResult.IsSuccess.Should().BeTrue();
        var originalPostalAddress = originalPostalAddressResult.Value;

        var addressResult = Address.CreateShipping(user.Id, originalPostalAddress);
        addressResult.IsSuccess.Should().BeTrue();
        var address = addressResult.Value;

        var newPostalAddressResult = PostalAddress.Create(
            "456 New St",
            "Newtown",
            "NewState",
            "NewCountry",
            "56789",
            "Suite 100");

        newPostalAddressResult.IsSuccess.Should().BeTrue();
        var newPostalAddress = newPostalAddressResult.Value;

        var newPhoneResult = PhoneNumber.Create("555-987-6543");
        newPhoneResult.IsSuccess.Should().BeTrue();
        var newPhone = newPhoneResult.Value;

        // Act
        var updateResult = address.Update(newPostalAddress, newPhone);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        address.PostalAddress.Should().Be(newPostalAddress);
        address.AddressLine1.Should().Be(newPostalAddress.AddressLine1);
        address.AddressLine2.Should().Be(newPostalAddress.AddressLine2);
        address.City.Should().Be(newPostalAddress.City);
        address.State.Should().Be(newPostalAddress.State);
        address.Country.Should().Be(newPostalAddress.Country);
        address.PostalCode.Should().Be(newPostalAddress.PostalCode);
        address.Phone.Should().Be(newPhone);
        address.DomainEvents.Should().Contain(e => e is AddressUpdatedEvent);
    }

    [Fact]
    public void Update_WithNullPostalAddress_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();

        var originalPostalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345");

        originalPostalAddressResult.IsSuccess.Should().BeTrue();
        var originalPostalAddress = originalPostalAddressResult.Value;

        var addressResult = Address.CreateShipping(user.Id, originalPostalAddress);
        addressResult.IsSuccess.Should().BeTrue();
        var address = addressResult.Value;

        PostalAddress newPostalAddress = null;

        // Act
        var updateResult = address.Update(newPostalAddress);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Address.AddressLine1Required");
    }

    [Fact]
    public void SetAddressType_ShouldUpdateAddressType()
    {
        // Arrange
        var user = CreateTestUser();

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        var addressResult = Address.CreateShipping(user.Id, postalAddress);
        addressResult.IsSuccess.Should().BeTrue();
        var address = addressResult.Value;
        address.AddressType.Should().Be(AddressType.Shipping);

        var newAddressType = AddressType.Both;

        // Act
        var result = address.SetAddressType(newAddressType);

        // Assert
        result.IsSuccess.Should().BeTrue();
        address.AddressType.Should().Be(newAddressType);
        address.DomainEvents.Should().Contain(e => e is AddressUpdatedEvent);
    }

    [Fact]
    public void SetDefault_ShouldUpdateIsDefault()
    {
        // Arrange
        var user = CreateTestUser();

        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345");

        postalAddressResult.IsSuccess.Should().BeTrue();
        var postalAddress = postalAddressResult.Value;

        var addressResult = Address.CreateShipping(user.Id, postalAddress);
        addressResult.IsSuccess.Should().BeTrue();
        var address = addressResult.Value;
        address.IsDefault.Should().BeFalse();

        address.ClearDomainEvents();

        // Act
        var result = address.SetDefault(true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        address.IsDefault.Should().BeTrue();
        address.DomainEvents.Should().Contain(e => e is DefaultAddressChangedEvent);
        address.DomainEvents.Should().Contain(e => e is AddressUpdatedEvent);

        // Act again
        address.ClearDomainEvents();
        var result2 = address.SetDefault(false);

        // Assert again
        result2.IsSuccess.Should().BeTrue();
        address.IsDefault.Should().BeFalse();
        address.DomainEvents.Should().NotContain(e => e is DefaultAddressChangedEvent);
        address.DomainEvents.Should().Contain(e => e is AddressUpdatedEvent);
    }
}
