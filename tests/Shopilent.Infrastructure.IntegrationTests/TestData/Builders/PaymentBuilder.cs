using Bogus;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class PaymentBuilder
{
    private Order _order;
    private User _user;
    private Money _amount;
    private PaymentMethodType _methodType;
    private PaymentProvider _provider;
    private string _externalReference;
    private PaymentMethod _paymentMethod;


    public PaymentBuilder()
    {
        var faker = new Faker();
        _amount = Money.Create(faker.Random.Decimal(10, 1000), "USD").Value;
        _methodType = faker.PickRandom<PaymentMethodType>();
        _provider = faker.PickRandom<PaymentProvider>();
        _externalReference = faker.Random.AlphaNumeric(20);
    }

    public static PaymentBuilder Random() => new();

    public PaymentBuilder WithOrder(Order order)
    {
        _order = order;
        return this;
    }

    public PaymentBuilder WithUser(User user)
    {
        _user = user;
        return this;
    }

    public PaymentBuilder WithAmount(Money amount)
    {
        _amount = amount;
        return this;
    }

    public PaymentBuilder WithAmount(decimal amount, string currency = "USD")
    {
        _amount = Money.Create(amount, currency).Value;
        return this;
    }

    public PaymentBuilder WithMethodType(PaymentMethodType methodType)
    {
        _methodType = methodType;
        return this;
    }

    public PaymentBuilder WithProvider(PaymentProvider provider)
    {
        _provider = provider;
        return this;
    }

    public PaymentBuilder WithExternalReference(string externalReference)
    {
        _externalReference = externalReference;
        return this;
    }

    public PaymentBuilder WithPaymentMethod(PaymentMethod paymentMethod)
    {
        _paymentMethod = paymentMethod;
        return this;
    }

    public PaymentBuilder WithStripeCard()
    {
        _methodType = PaymentMethodType.CreditCard;
        _provider = PaymentProvider.Stripe;
        return this;
    }

    public PaymentBuilder WithPayPal()
    {
        _methodType = PaymentMethodType.PayPal;
        _provider = PaymentProvider.PayPal;
        return this;
    }

    public Payment Build()
    {
        if (_order == null)
            throw new InvalidOperationException("Order is required. Use WithOrder() method.");

        if (_paymentMethod != null)
        {
            return Payment.CreateWithPaymentMethod(_order.Id, _user.Id, _amount, _paymentMethod, _externalReference)
                .Value;
        }

        return Payment.Create(_order.Id, _user.Id, _amount, _methodType, _provider, _externalReference).Value;
    }
}
