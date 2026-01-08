using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Application.UnitTests.Testing.Builders;

public class AddressBuilder
{
    private Guid _id = Guid.NewGuid();
    private User _user;
    private Guid _userId = Guid.NewGuid();
    private string _fullName = "John Doe";
    private string _company = null;
    private string _streetAddress1 = "123 Main St";
    private string _streetAddress2 = null;
    private string _city = "Anytown";
    private string _state = "CA";
    private string _postalCode = "12345";
    private string _country = "US";
    private AddressType _addressType = AddressType.Shipping;
    private string _phoneNumber = null;
    private bool _isDefault = false;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;

    public AddressBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }
    
    public AddressBuilder WithUser(User user)
    {
        _user = user;
        _userId = user.Id;
        return this;
    }
    
    public AddressBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }
    
    public AddressBuilder WithFullName(string fullName)
    {
        _fullName = fullName;
        return this;
    }
    
    public AddressBuilder WithCompany(string company)
    {
        _company = company;
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
    
    public AddressBuilder CreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public Address Build()
    {
        // Create a dummy user if none provided
        if (_user == null)
        {
            var userBuilder = new UserBuilder().WithId(_userId);
            _user = userBuilder.Build();
        }

        var postalAddressResult = PostalAddress.Create(
            _streetAddress1,
            _city,
            _state,
            _country,
            _postalCode,
            _streetAddress2);
            
        if (postalAddressResult.IsFailure)
            throw new InvalidOperationException($"Invalid postal address: {postalAddressResult.Error.Message}");

        PhoneNumber phone = null;
        if (!string.IsNullOrEmpty(_phoneNumber))
        {
            var phoneResult = PhoneNumber.Create(_phoneNumber);
            if (phoneResult.IsFailure)
                throw new InvalidOperationException($"Invalid phone number: {_phoneNumber}");
            phone = phoneResult.Value;
        }

        // Need to use User.AddAddress method since Address.Create is internal
        var addressResult = _user.AddAddress(postalAddressResult.Value, _addressType, phone, _isDefault);
        if (addressResult.IsFailure)
            throw new InvalidOperationException($"Failed to create address: {addressResult.Error.Message}");
            
        var address = addressResult.Value;
        
        // Use reflection to set private properties
        SetPrivatePropertyValue(address, "Id", _id);
        SetPrivatePropertyValue(address, "CreatedAt", _createdAt);
        SetPrivatePropertyValue(address, "UpdatedAt", _updatedAt);
        
        return address;
    }
    
    private static void SetPrivatePropertyValue<T>(object obj, string propertyName, T value)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName);
        if (propertyInfo != null)
        {
            propertyInfo.SetValue(obj, value, null);
        }
        else
        {
            var fieldInfo = obj.GetType().GetField(propertyName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
                
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
            }
            else
            {
                throw new InvalidOperationException($"Property or field {propertyName} not found on type {obj.GetType().Name}");
            }
        }
    }
}