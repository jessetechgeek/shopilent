using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;

namespace Shopilent.Domain.Common.ValueObjects;

public class PhoneNumber : ValueObject
{
    public string Value { get; private set; }

    protected PhoneNumber()
    {
    }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static Result<PhoneNumber> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<PhoneNumber>(UserErrors.PhoneRequired);

        var digitsOnly = value.StartsWith("+")
            ? "+" + new string(value.Substring(1).Where(char.IsDigit).ToArray())
            : new string(value.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 7)
            return Result.Failure<PhoneNumber>(UserErrors.InvalidPhoneFormat);

        return Result.Success(new PhoneNumber(digitsOnly));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}