using Shopilent.Domain.Identity;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.ValueObjects;

namespace Shopilent.Application.UnitTests.Testing.Builders;

public class PaymentMethodBuilder
{
    private Guid _id = Guid.NewGuid();
    private User _user;
    private Guid _userId = Guid.NewGuid();
    private PaymentMethodType _type = PaymentMethodType.CreditCard;
    private PaymentProvider _provider = PaymentProvider.Stripe;
    private string _token = "pm_test_123456789";
    private string _displayName = "Test Card";
    private PaymentCardDetails _cardDetails = null;
    private bool _isDefault = false;
    private bool _isActive = true;
    private DateTime? _expiryDate = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;
    private readonly Dictionary<string, object> _metadata = new();

    public PaymentMethodBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public PaymentMethodBuilder WithUser(User user)
    {
        _user = user;
        _userId = user.Id;
        return this;
    }

    public PaymentMethodBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public PaymentMethodBuilder WithType(PaymentMethodType type)
    {
        _type = type;
        return this;
    }

    public PaymentMethodBuilder WithProvider(PaymentProvider provider)
    {
        _provider = provider;
        return this;
    }

    public PaymentMethodBuilder WithToken(string token)
    {
        _token = token;
        return this;
    }

    public PaymentMethodBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public PaymentMethodBuilder WithCardDetails(string brand, string lastFourDigits, int expiryMonth, int expiryYear)
    {
        var expiryDate = new DateTime(expiryYear, expiryMonth, 1).AddMonths(1).AddDays(-1); // Last day of expiry month
        var cardDetailsResult = PaymentCardDetails.Create(brand, lastFourDigits, expiryDate);
        if (cardDetailsResult.IsFailure)
            throw new InvalidOperationException($"Invalid card details: {cardDetailsResult.Error.Message}");

        _cardDetails = cardDetailsResult.Value;
        return this;
    }

    public PaymentMethodBuilder IsDefault()
    {
        _isDefault = true;
        return this;
    }

    public PaymentMethodBuilder IsInactive()
    {
        _isActive = false;
        return this;
    }

    public PaymentMethodBuilder WithExpiryDate(DateTime expiryDate)
    {
        _expiryDate = expiryDate;
        return this;
    }

    public PaymentMethodBuilder CreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public PaymentMethodBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public PaymentMethod Build()
    {
        // Create a dummy user if none provided
        if (_user == null)
        {
            var userBuilder = new UserBuilder().WithId(_userId);
            _user = userBuilder.Build();
        }

        var paymentMethodResult = _cardDetails != null
            ? PaymentMethod.CreateCardMethod(_user.Id, _provider, _token, _cardDetails, _isDefault)
            : PaymentMethod.Create(_user.Id, _type, _provider, _token, _displayName, _isDefault);

        if (paymentMethodResult.IsFailure)
            throw new InvalidOperationException($"Failed to create payment method: {paymentMethodResult.Error.Message}");

        var paymentMethod = paymentMethodResult.Value;

        // Use reflection to set private properties
        SetPrivatePropertyValue(paymentMethod, "Id", _id);
        SetPrivatePropertyValue(paymentMethod, "CreatedAt", _createdAt);
        SetPrivatePropertyValue(paymentMethod, "UpdatedAt", _updatedAt);

        if (_expiryDate.HasValue)
        {
            SetPrivatePropertyValue(paymentMethod, "ExpiryDate", _expiryDate.Value);
        }

        // Set metadata
        foreach (var metadata in _metadata)
        {
            paymentMethod.Metadata[metadata.Key] = metadata.Value;
        }

        // Set inactive if needed
        if (!_isActive)
        {
            paymentMethod.Deactivate();
        }

        return paymentMethod;
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
