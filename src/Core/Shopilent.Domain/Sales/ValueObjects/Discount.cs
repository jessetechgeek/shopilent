using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Domain.Sales.ValueObjects;

public class Discount : ValueObject
{
    public decimal Value { get; private set; }
    public DiscountType Type { get; private set; }
    public string Code { get; private set; }

    protected Discount()
    {
    }

    private Discount(decimal value, DiscountType type, string code = null)
    {
        Value = value;
        Type = type;
        Code = code;
    }

    public static Result<Discount> CreatePercentage(decimal percentage, string code = null)
    {
        if (percentage < 0)
            return Result.Failure<Discount>(OrderErrors.NegativeDiscount);

        if (percentage > 100)
            return Result.Failure<Discount>(OrderErrors.InvalidDiscountPercentage);

        return Result.Success(new Discount(percentage, DiscountType.Percentage, code));
    }

    public static Result<Discount> CreateFixedAmount(decimal amount, string code = null)
    {
        if (amount < 0)
            return Result.Failure<Discount>(OrderErrors.NegativeDiscount);

        return Result.Success(new Discount(amount, DiscountType.FixedAmount, code));
    }

    public Result<Money> CalculateDiscount(Money baseAmount)
    {
        if (baseAmount == null)
            return Result.Failure<Money>(OrderErrors.InvalidAmount);

        if (Type == DiscountType.Percentage)
        {
            var calculatedDiscount = baseAmount.Amount * (Value / 100m);
            return Money.Create(calculatedDiscount, baseAmount.Currency);
        }
        else // FixedAmount
        {
            var discountAmount = Math.Min(Value, baseAmount.Amount);
            return Money.Create(discountAmount, baseAmount.Currency);
        }
    }

    public Result<Money> ApplyDiscount(Money baseAmount)
    {
        if (baseAmount == null)
            return Result.Failure<Money>(OrderErrors.InvalidAmount);

        var discountResult = CalculateDiscount(baseAmount);
        if (discountResult.IsFailure)
            return Result.Failure<Money>(discountResult.Error);

        var discountAmount = discountResult.Value;
        return baseAmount.Subtract(discountAmount);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
        yield return Type;
        yield return Code ?? string.Empty;
    }

    public override string ToString()
    {
        return Type == DiscountType.Percentage
            ? $"{Value}%"
            : $"{Value:C}";
    }
}