using Bogus;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class AddressBuilder
{
    private User _user;
    private string _streetAddress1 = "123 Main St";
    private string _streetAddress2 = null;
    private string _city = "Anytown";
    private string _state = "CA";
    private string _postalCode = "12345";
    private string _country = "US";
    private AddressType _addressType = AddressType.Shipping;
    private string _phoneNumber = null;
    private bool _isDefault = false;
    private readonly Faker _faker = new();

    public AddressBuilder WithUser(User user)
    {
        _user = user;
        return this;
    }

    public AddressBuilder WithStreetAddress(string streetAddress1, string streetAddress2 = null)
    {
        _streetAddress1 = streetAddress1;
        _streetAddress2 = streetAddress2;
        return this;
    }

    public AddressBuilder WithLocation(string city, string state, string postalCode, string country = "US")
    {
        _city = city;
        _state = state;
        _postalCode = postalCode;
        _country = country;
        return this;
    }

    public AddressBuilder WithAddressType(AddressType addressType)
    {
        _addressType = addressType;
        return this;
    }

    public AddressBuilder WithPhoneNumber(string phoneNumber)
    {
        _phoneNumber = phoneNumber;
        return this;
    }

    public AddressBuilder IsDefault()
    {
        _isDefault = true;
        return this;
    }

    public AddressBuilder IsNotDefault()
    {
        _isDefault = false;
        return this;
    }

    public AddressBuilder WithRandomData()
    {
        _streetAddress1 = _faker.Address.StreetAddress();
        _streetAddress2 = _faker.Random.Bool(0.3f) ? _faker.Address.SecondaryAddress() : null;
        _city = _faker.Address.City();
        _state = _faker.Address.StateAbbr();
        _postalCode = _faker.Address.ZipCode();
        _country = "US";
        _phoneNumber = _faker.Phone.PhoneNumber("##########"); // Generate digits only since PhoneNumber normalizes
        return this;
    }

    public AddressBuilder AsBilling()
    {
        _addressType = AddressType.Billing;
        return this;
    }

    public AddressBuilder AsShipping()
    {
        _addressType = AddressType.Shipping;
        return this;
    }

    public AddressBuilder AsBoth()
    {
        _addressType = AddressType.Both;
        return this;
    }

    public Address Build()
    {
        // Create a default user if none provided
        if (_user == null)
        {
            _user = new UserBuilder().Build();
        }

        // Create postal address
        var postalAddressResult = PostalAddress.Create(
            _streetAddress1,
            _city,
            _state,
            _country,
            _postalCode,
            _streetAddress2);

        if (postalAddressResult.IsFailure)
            throw new InvalidOperationException($"Invalid postal address: {postalAddressResult.Error.Message}");

        // Create phone number if provided
        PhoneNumber phone = null;
        if (!string.IsNullOrEmpty(_phoneNumber))
        {
            var phoneResult = PhoneNumber.Create(_phoneNumber);
            if (phoneResult.IsFailure)
                throw new InvalidOperationException($"Invalid phone number: {_phoneNumber}");
            phone = phoneResult.Value;
        }

        // Create address through User aggregate since Address.Create is internal
        var addressResult = _user.AddAddress(postalAddressResult.Value, _addressType, phone, _isDefault);
        if (addressResult.IsFailure)
            throw new InvalidOperationException($"Failed to create address: {addressResult.Error.Message}");

        return addressResult.Value;
    }

    public static AddressBuilder Default()
    {
        return new AddressBuilder();
    }

    public static AddressBuilder DefaultShipping()
    {
        return new AddressBuilder()
            .AsShipping()
            .IsDefault();
    }

    public static AddressBuilder DefaultBilling()
    {
        return new AddressBuilder()
            .AsBilling()
            .IsDefault();
    }

    public static AddressBuilder DefaultBoth()
    {
        return new AddressBuilder()
            .AsBoth()
            .IsDefault();
    }

    public static AddressBuilder Random()
    {
        return new AddressBuilder()
            .WithRandomData();
    }

    public static AddressBuilder RandomForUser(User user)
    {
        return new AddressBuilder()
            .WithUser(user)
            .WithRandomData();
    }

    /// <summary>
    /// Creates a unique address to avoid database constraint violations in tests
    /// </summary>
    public AddressBuilder WithUniqueData()
    {
        var timestamp = DateTime.Now.Ticks;
        
        _streetAddress1 = $"{timestamp % 9999} Test Street";
        _city = $"TestCity{timestamp % 999}";
        _state = "CA";
        _postalCode = $"{(timestamp % 90000) + 10000:D5}";
        _country = "US";
        
        if (_phoneNumber != null)
        {
            _phoneNumber = $"{(timestamp % 900) + 100:D3}{(timestamp % 900) + 100:D3}{(timestamp % 9000) + 1000:D4}"; // Generate digits only
        }
        
        return this;
    }
}