using Bogus;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.ValueObjects;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class PaymentMethodBuilder
{
    private User _user;
    private PaymentMethodType _type;
    private PaymentProvider _provider;
    private string _token;
    private string _displayName;
    private PaymentCardDetails _cardDetails;
    private bool _isDefault;
    private string _email;

    public PaymentMethodBuilder()
    {
        var faker = new Faker();
        _type = PaymentMethodType.CreditCard;
        _provider = PaymentProvider.Stripe;
        _token = $"pm_{faker.Random.AlphaNumeric(24)}";
        _displayName = faker.Commerce.ProductName();
        _isDefault = false;
    }

    public static PaymentMethodBuilder Random() => new();

    public PaymentMethodBuilder WithUser(User user)
    {
        _user = user;
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

    public PaymentMethodBuilder WithCardDetails(PaymentCardDetails cardDetails)
    {
        _cardDetails = cardDetails;
        _type = PaymentMethodType.CreditCard;
        return this;
    }

    public PaymentMethodBuilder WithCreditCard()
    {
        var faker = new Faker();
        _type = PaymentMethodType.CreditCard;
        _provider = PaymentProvider.Stripe;

        var brand = faker.PickRandom("Visa", "Mastercard", "Amex");
        var lastFour = faker.Random.Int(1000, 9999).ToString();
        var expiryDate = DateTime.UtcNow.AddYears(faker.Random.Int(1, 5));

        _cardDetails = PaymentCardDetails.Create(brand, lastFour, expiryDate).Value;
        _displayName = $"{brand} ending in {lastFour}";

        return this;
    }

    public PaymentMethodBuilder WithPayPal(string email = null)
    {
        var faker = new Faker();
        _type = PaymentMethodType.PayPal;
        _provider = PaymentProvider.PayPal;
        _email = email ?? faker.Internet.Email();
        _displayName = $"PayPal ({_email})";
        return this;
    }

    public PaymentMethodBuilder AsDefault()
    {
        _isDefault = true;
        return this;
    }

    public PaymentMethod Build()
    {
        if (_user == null)
            throw new InvalidOperationException("User is required. Use WithUser() method.");

        return _type switch
        {
            PaymentMethodType.CreditCard when _cardDetails != null =>
                PaymentMethod.CreateCardMethod(_user.Id, _provider, _token, _cardDetails, _isDefault).Value,
            PaymentMethodType.PayPal =>
                PaymentMethod.CreatePayPalMethod(_user.Id, _token, _email, _isDefault).Value,
            _ => PaymentMethod.Create(_user.Id, _type, _provider, _token, _displayName, _isDefault).Value
        };
    }
}
