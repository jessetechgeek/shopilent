using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Domain.Common.ValueObjects;

public class Money : ValueObject
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }

    protected Money()
    {
    }

    private Money(decimal amount, string currency = "USD")
    {
        Amount = amount;
        Currency = currency;
    }

    public Money Add(Money money)
    {
        if (Currency != money.Currency)
            throw new InvalidOperationException(
                $"Cannot add money with different currencies: {Currency} != {money.Currency}");

        return new Money(Amount + money.Amount, Currency);
    }

    public Result<Money> AddSafe(Money money)
    {
        if (Currency != money.Currency)
            return Result.Failure<Money>(OrderErrors.CurrencyMismatch);

        return Result.Success(new Money(Amount + money.Amount, Currency));
    }

    public static Result<Money> Create(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            return Result.Failure<Money>(OrderErrors.NegativeAmount);

        if (string.IsNullOrWhiteSpace(currency))
            return Result.Failure<Money>(OrderErrors.InvalidCurrency);

        return Result.Success(new Money(amount, currency));
    }

    public static Result<Money> FromDollars(decimal dollars)
    {
        return Create(dollars, "USD");
    }

    public static Result<Money> FromEuros(decimal euros)
    {
        return Create(euros, "EUR");
    }

    public static Money Zero(string currency = "USD") => new Money(0, currency);

    public Result<Money> Subtract(Money money)
    {
        if (Currency != money.Currency)
            return Result.Failure<Money>(OrderErrors.CurrencyMismatch);

        var result = Amount - money.Amount;
        if (result < 0)
            return Result.Failure<Money>(OrderErrors.NegativeAmount);

        return Result.Success(new Money(result, Currency));
    }

    public Result<Money> Multiply(decimal multiplier)
    {
        if (multiplier < 0)
            return Result.Failure<Money>(OrderErrors.NegativeAmount);

        return Result.Success(new Money(Amount * multiplier, Currency));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
