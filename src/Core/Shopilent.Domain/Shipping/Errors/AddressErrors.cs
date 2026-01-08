using Shopilent.Domain.Common.Errors;

namespace Shopilent.Domain.Shipping.Errors;

public static class AddressErrors
{
    public static Error AddressLine1Required => Error.Validation(
        code: "Address.AddressLine1Required",
        message: "Address line 1 cannot be empty.");
        
    public static Error CityRequired => Error.Validation(
        code: "Address.CityRequired",
        message: "City cannot be empty.");
        
    public static Error StateRequired => Error.Validation(
        code: "Address.StateRequired",
        message: "State cannot be empty.");
        
    public static Error CountryRequired => Error.Validation(
        code: "Address.CountryRequired",
        message: "Country cannot be empty.");
        
    public static Error PostalCodeRequired => Error.Validation(
        code: "Address.PostalCodeRequired",
        message: "Postal code cannot be empty.");
        
    public static Error InvalidPostalCode(string country) => Error.Validation(
        code: "Address.InvalidPostalCode",
        message: $"Invalid postal code format for {country}.");
        
    public static Error NotFound(Guid id) => Error.NotFound(
        code: "Address.NotFound",
        message: $"Address with ID {id} was not found.");
        
    public static Error NoDefaultAddress(string addressType) => Error.NotFound(
        code: "Address.NoDefaultAddress",
        message: $"No default {addressType} address found for the user.");
        
    public static Error InvalidAddressType => Error.Validation(
        code: "Address.InvalidAddressType",
        message: "Invalid address type specified.");

    public static Error InvalidUserId => Error.Validation(
        code: "Address.InvalidUserId",
        message: "User ID cannot be empty.");
}