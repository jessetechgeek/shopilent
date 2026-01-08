using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Payments.ValueObjects;

namespace Shopilent.Domain.Payments;

public class PaymentMethod : AggregateRoot
{
    private PaymentMethod()
    {
        // Required by EF Core
    }

    private PaymentMethod(
        Guid userId,
        PaymentMethodType type,
        PaymentProvider provider,
        string token,
        string displayName,
        bool isDefault = false)
    {
        UserId = userId;
        Type = type;
        Provider = provider;
        Token = token;
        DisplayName = displayName;
        ExpiryDate = null;
        IsDefault = isDefault;
        IsActive = true;
        Metadata = new Dictionary<string, object>();
    }

    private PaymentMethod(
        Guid userId,
        PaymentMethodType type,
        PaymentProvider provider,
        string token,
        string displayName,
        PaymentCardDetails cardDetails,
        bool isDefault = false)
        : this(userId, type, provider, token, displayName, isDefault)
    {
        if (cardDetails != null)
        {
            CardBrand = cardDetails.Brand;
            LastFourDigits = cardDetails.LastFourDigits;
            ExpiryDate = cardDetails.ExpiryDate;
        }
    }

    // Internal factory method for use by User aggregate
    internal static PaymentMethod CreateInternal(
        Guid userId,
        PaymentMethodType type,
        PaymentProvider provider,
        string token,
        string displayName,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PaymentMethod>(UserErrors.NotFound(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be empty", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));
        }

        var paymentMethod = new PaymentMethod(userId, type, provider, token, displayName, isDefault);
        paymentMethod.AddDomainEvent(new PaymentMethodCreatedEvent(paymentMethod.Id, userId));
        return paymentMethod;
    }

    // For use by the User aggregate which should validate inputs
    // internal static Result<PaymentMethod> CreateFromUser(
    //     Result<User> userResult,
    //     PaymentMethodType type,
    //     PaymentProvider provider,
    //     string token,
    //     string displayName,
    //     bool isDefault = false)
    // {
    //     if (userResult.IsFailure)
    //     {
    //         return Result.Failure<PaymentMethod>(userResult.Error);
    //     }
    //
    //     if (string.IsNullOrWhiteSpace(token))
    //     {
    //         return Result.Failure<PaymentMethod>(PaymentMethodErrors.TokenRequired);
    //     }
    //
    //     if (string.IsNullOrWhiteSpace(displayName))
    //     {
    //         return Result.Failure<PaymentMethod>(PaymentMethodErrors.DisplayNameRequired);
    //     }
    //
    //     var paymentMethod = new PaymentMethod(userResult.Value, type, provider, token, displayName, isDefault);
    //     paymentMethod.AddDomainEvent(new PaymentMethodCreatedEvent(paymentMethod.Id, userResult.Value.Id));
    //     return Result.Success(paymentMethod);
    // }

    // Public factory methods that call the internal ones
    public static Result<PaymentMethod> Create(
        Guid userId,
        PaymentMethodType type,
        PaymentProvider provider,
        string token,
        string displayName,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PaymentMethod>(UserErrors.NotFound(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<PaymentMethod>(PaymentMethodErrors.TokenRequired);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<PaymentMethod>(PaymentMethodErrors.DisplayNameRequired);
        }

        PaymentMethod paymentMethod = CreateInternal(userId, type, provider, token, displayName, isDefault);
        return Result.Success(paymentMethod);
    }

    internal static PaymentMethod CreateInternalWithCardDetails(
        Guid userId,
        PaymentProvider provider,
        string token,
        PaymentCardDetails cardDetails,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PaymentMethod>(UserErrors.NotFound(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be empty", nameof(token));
        }

        if (cardDetails == null)
        {
            throw new ArgumentException("Card details cannot be null", nameof(cardDetails));
        }

        string displayName = $"{cardDetails.Brand} ending in {cardDetails.LastFourDigits}";
        var paymentMethod = new PaymentMethod(userId, PaymentMethodType.CreditCard, provider, token, displayName,
            cardDetails, isDefault);
        paymentMethod.AddDomainEvent(new PaymentMethodCreatedEvent(paymentMethod.Id, userId));
        return paymentMethod;
    }

    public static Result<PaymentMethod> CreateCardMethod(
        Guid userId,
        PaymentProvider provider,
        string token,
        PaymentCardDetails cardDetails,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PaymentMethod>(UserErrors.NotFound(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<PaymentMethod>(PaymentMethodErrors.TokenRequired);
        }

        if (cardDetails == null)
        {
            return Result.Failure<PaymentMethod>(PaymentMethodErrors.InvalidCardDetails);
        }

        if (cardDetails.ExpiryDate < DateTime.UtcNow)
        {
            return Result.Failure<PaymentMethod>(PaymentMethodErrors.ExpiredCard);
        }

        PaymentMethod paymentMethod = CreateInternalWithCardDetails(userId, provider, token, cardDetails, isDefault);
        return Result.Success(paymentMethod);
    }

    internal static PaymentMethod CreateInternalPayPalMethod(
        Guid userId,
        string token,
        string email,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PaymentMethod>(UserErrors.NotFound(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be empty", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be empty", nameof(email));
        }

        string displayName = $"PayPal ({email})";
        var paymentMethod = new PaymentMethod(userId, PaymentMethodType.PayPal, PaymentProvider.PayPal, token,
            displayName, isDefault);
        paymentMethod.UpdateMetadata("email", email);
        paymentMethod.AddDomainEvent(new PaymentMethodCreatedEvent(paymentMethod.Id, userId));
        return paymentMethod;
    }

    public static Result<PaymentMethod> CreatePayPalMethod(
        Guid userId,
        string token,
        string email,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PaymentMethod>(UserErrors.NotFound(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<PaymentMethod>(PaymentMethodErrors.TokenRequired);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<PaymentMethod>(UserErrors.EmailRequired);
        }

        PaymentMethod paymentMethod = CreateInternalPayPalMethod(userId, token, email, isDefault);
        return Result.Success(paymentMethod);
    }

    public Guid UserId { get; private set; }
    public PaymentMethodType Type { get; private set; }
    public PaymentProvider Provider { get; private set; }
    public string Token { get; private set; }
    public string DisplayName { get; private set; }
    public string CardBrand { get; private set; }
    public string LastFourDigits { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

    public Result UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure(PaymentMethodErrors.DisplayNameRequired);
        }

        DisplayName = displayName;
        AddDomainEvent(new PaymentMethodUpdatedEvent(Id));
        return Result.Success();
    }

    public Result SetDefault(bool isDefault)
    {
        if (IsDefault == isDefault)
        {
            return Result.Success();
        }

        IsDefault = isDefault;

        if (isDefault)
        {
            AddDomainEvent(new DefaultPaymentMethodChangedEvent(Id, UserId));
        }

        AddDomainEvent(new PaymentMethodUpdatedEvent(Id));
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Success();
        }

        IsActive = true;
        AddDomainEvent(new PaymentMethodUpdatedEvent(Id));
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        IsActive = false;
        AddDomainEvent(new PaymentMethodUpdatedEvent(Id));
        return Result.Success();
    }

    public Result UpdateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure(PaymentMethodErrors.TokenRequired);
        }

        Token = token;
        AddDomainEvent(new PaymentMethodUpdatedEvent(Id));
        return Result.Success();
    }

    public Result UpdateCardDetails(PaymentCardDetails cardDetails)
    {
        if (Type != PaymentMethodType.CreditCard)
        {
            return Result.Failure(PaymentMethodErrors.InvalidCardDetails);
        }

        if (cardDetails == null)
        {
            return Result.Failure(PaymentMethodErrors.InvalidCardDetails);
        }

        if (cardDetails.ExpiryDate < DateTime.UtcNow)
        {
            return Result.Failure(PaymentMethodErrors.ExpiredCard);
        }

        CardBrand = cardDetails.Brand;
        LastFourDigits = cardDetails.LastFourDigits;
        ExpiryDate = cardDetails.ExpiryDate;
        DisplayName = $"{cardDetails.Brand} ending in {cardDetails.LastFourDigits}";

        AddDomainEvent(new PaymentMethodUpdatedEvent(Id));
        return Result.Success();
    }

    public Result UpdateMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure(PaymentMethodErrors.InvalidMetadataKey);
        }

        Metadata[key] = value;

        AddDomainEvent(new PaymentMethodUpdatedEvent(Id));
        return Result.Success();
    }

    public Result Delete()
    {
        //TODO: Handler Payment Method Deletion
        // Consider if default payment methods can be deleted
        if (IsDefault)
        {
            // Either prevent deletion or handle setting a new default
        }

        AddDomainEvent(new PaymentMethodDeletedEvent(Id, UserId));
        return Result.Success();
    }
}
