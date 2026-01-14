using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Errors;
using Shopilent.Domain.Shipping.Events;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Shipping;

public class Address : AggregateRoot
{
    private Address()
    {
        // Required by EF Core
    }

    private Address(
        Guid userId,
        PostalAddress postalAddress,
        AddressType addressType = AddressType.Shipping,
        PhoneNumber phone = null,
        bool isDefault = false)
    {
        UserId = userId;
        PostalAddress = postalAddress;
        Phone = phone;
        IsDefault = isDefault;
        AddressType = addressType;
    }

    // Internal factory method for use by User aggregate
    internal static Address Create(
        Guid userId,
        PostalAddress postalAddress,
        AddressType addressType = AddressType.Shipping,
        PhoneNumber phone = null,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (postalAddress == null)
            throw new ArgumentNullException(nameof(postalAddress), "Postal address cannot be null");

        var address = new Address(userId, postalAddress, addressType, phone, isDefault);
        address.AddDomainEvent(new AddressCreatedEvent(address.Id, userId));
        return address;
    }

    // Public factory methods that use the internal ones
    public static Result<Address> CreateShipping(
        Guid userId,
        PostalAddress postalAddress,
        PhoneNumber phone = null,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
            return Result.Failure<Address>(AddressErrors.InvalidUserId);

        if (postalAddress == null)
            return Result.Failure<Address>(AddressErrors.AddressLine1Required);

        var address = Create(userId, postalAddress, AddressType.Shipping, phone, isDefault);
        return Result.Success(address);
    }

    public static Result<Address> CreateBilling(
        Guid userId,
        PostalAddress postalAddress,
        PhoneNumber phone = null,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
            return Result.Failure<Address>(AddressErrors.InvalidUserId);

        if (postalAddress == null)
            return Result.Failure<Address>(AddressErrors.AddressLine1Required);

        var address = Create(userId, postalAddress, AddressType.Billing, phone, isDefault);
        return Result.Success(address);
    }

    public static Result<Address> CreateBoth(
        Guid userId,
        PostalAddress postalAddress,
        PhoneNumber phone = null,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
            return Result.Failure<Address>(AddressErrors.InvalidUserId);

        if (postalAddress == null)
            return Result.Failure<Address>(AddressErrors.AddressLine1Required);

        var address = Create(userId, postalAddress, AddressType.Both, phone, isDefault);
        return Result.Success(address);
    }

    public Guid UserId { get; private set; }

    public PostalAddress PostalAddress { get; private set; }

    public string AddressLine1 => PostalAddress.AddressLine1;
    public string AddressLine2 => PostalAddress.AddressLine2;
    public string City => PostalAddress.City;
    public string State => PostalAddress.State;
    public string Country => PostalAddress.Country;
    public string PostalCode => PostalAddress.PostalCode;

    public PhoneNumber Phone { get; private set; }
    public bool IsDefault { get; private set; }
    public AddressType AddressType { get; private set; }

    public Result Update(
        PostalAddress postalAddress,
        PhoneNumber phone = null)
    {
        if (postalAddress == null)
            return Result.Failure(AddressErrors.AddressLine1Required);

        PostalAddress = postalAddress;
        Phone = phone;

        AddDomainEvent(new AddressUpdatedEvent(Id));
        return Result.Success();
    }

    public Result SetAddressType(AddressType addressType)
    {
        if (AddressType == addressType)
            return Result.Success();

        AddressType = addressType;

        AddDomainEvent(new AddressUpdatedEvent(Id));
        return Result.Success();
    }

    public Result SetDefault(bool isDefault)
    {
        if (IsDefault == isDefault)
            return Result.Success();

        IsDefault = isDefault;

        if (isDefault)
            AddDomainEvent(new DefaultAddressChangedEvent(Id, UserId));

        AddDomainEvent(new AddressUpdatedEvent(Id));
        return Result.Success();
    }

    public Result Delete()
    {
        AddDomainEvent(new AddressDeletedEvent(Id, UserId));
        return Result.Success();
    }
}
