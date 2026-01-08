using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Payments.Events;

namespace Shopilent.Domain.Payments;

public class Payment : AggregateRoot
{
    private Payment()
    {
        // Required by EF Core
    }

    private Payment(
        Guid orderId,
        Guid? userId,
        Money amount,
        PaymentMethodType methodType,
        PaymentProvider provider,
        string externalReference = null)
    {
        OrderId = orderId;
        UserId = userId;
        Amount = amount;
        Currency = amount.Currency; // Set currency from Money object to match DB schema
        MethodType = methodType;
        Provider = provider;
        Status = PaymentStatus.Pending;
        ExternalReference = externalReference;
        Metadata = new Dictionary<string, object>();
    }

    // Internal factory method for use by Order aggregate
    internal static Payment CreateInternal(
        Guid orderId,
        Guid? userId,
        Money amount,
        PaymentMethodType methodType,
        PaymentProvider provider,
        string externalReference = null)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<Payment>(PaymentErrors.InvalidOrderId);


        if (amount == null)
            throw new ArgumentException("Amount cannot be null", nameof(amount));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        var payment = new Payment(orderId, userId, amount, methodType, provider, externalReference);
        payment.AddDomainEvent(new PaymentCreatedEvent(payment.Id));
        return payment;
    }

    // Public factory methods that call the internal ones
    public static Result<Payment> Create(
        Guid orderId,
        Guid? userId,
        Money amount,
        PaymentMethodType methodType,
        PaymentProvider provider,
        string externalReference = null)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<Payment>(PaymentErrors.InvalidOrderId);

        if (amount == null)
            return Result.Failure<Payment>(PaymentErrors.NegativeAmount);

        if (amount.Amount < 0)
            return Result.Failure<Payment>(PaymentErrors.NegativeAmount);

        var payment = CreateInternal(orderId, userId, amount, methodType, provider, externalReference);
        return Result.Success(payment);
    }

    // With PaymentMethod entity reference
    internal static Payment CreateInternalWithPaymentMethod(
        Guid orderId,
        Guid? userId,
        Money amount,
        PaymentMethod paymentMethod,
        string externalReference = null)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<Payment>(PaymentErrors.InvalidOrderId);

        if (amount == null || amount.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        if (paymentMethod == null)
            throw new ArgumentException("Payment method cannot be null", nameof(paymentMethod));

        if (!paymentMethod.IsActive)
            throw new ArgumentException("Payment method is not active", nameof(paymentMethod));

        var payment = new Payment(
            orderId,
            userId,
            amount,
            paymentMethod.Type,
            paymentMethod.Provider,
            externalReference);

        payment.PaymentMethodId = paymentMethod.Id;
        payment.AddDomainEvent(new PaymentCreatedEvent(payment.Id));
        return payment;
    }

    public static Result<Payment> CreateWithPaymentMethod(
        Guid orderId,
        Guid? userId,
        Money amount,
        PaymentMethod paymentMethod,
        string externalReference = null)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<Payment>(PaymentErrors.InvalidOrderId);

        if (amount == null || amount.Amount <= 0)
            return Result.Failure<Payment>(PaymentErrors.NegativeAmount);

        if (paymentMethod == null)
            return Result.Failure<Payment>(PaymentErrors.PaymentMethodNotFound(Guid.Empty));

        if (!paymentMethod.IsActive)
            return Result.Failure<Payment>(PaymentMethodErrors.InactivePaymentMethod);

        var payment = CreateInternalWithPaymentMethod(orderId, userId, amount, paymentMethod, externalReference);
        return Result.Success(payment);
    }

    public Guid OrderId { get; private set; }
    public Guid? UserId { get; private set; }
    public Money Amount { get; private set; }
    public string Currency { get; private set; } // Added to match DB schema
    public PaymentMethodType MethodType { get; private set; } // Renamed from Method to be more clear
    public PaymentProvider Provider { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string ExternalReference { get; private set; }
    public string TransactionId { get; private set; }
    public Guid? PaymentMethodId { get; private set; } // Reference to the payment method entity
    public Dictionary<string, object> Metadata { get; private set; } = new();
    public DateTime? ProcessedAt { get; private set; }
    public string ErrorMessage { get; private set; }

    public Result UpdateStatus(PaymentStatus newStatus, string transactionId = null, string errorMessage = null)
    {
        if (Status == newStatus)
            return Result.Success();

        var oldStatus = Status;
        Status = newStatus;

        if (!string.IsNullOrWhiteSpace(transactionId))
            TransactionId = transactionId;

        if (newStatus == PaymentStatus.Succeeded)
            ProcessedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(errorMessage))
            ErrorMessage = errorMessage;

        AddDomainEvent(new PaymentStatusChangedEvent(Id, oldStatus, newStatus));
        return Result.Success();
    }

    public Result MarkAsSucceeded(string transactionId)
    {
        if (Status == PaymentStatus.Succeeded)
            return Result.Success();

        if (string.IsNullOrWhiteSpace(transactionId))
            return Result.Failure(PaymentErrors.TokenRequired);

        var updateResult = UpdateStatus(PaymentStatus.Succeeded, transactionId);
        if (updateResult.IsFailure)
            return updateResult;

        AddDomainEvent(new PaymentSucceededEvent(Id, OrderId));
        return Result.Success();
    }

    public Result MarkAsFailed(string errorMessage = null)
    {
        if (Status == PaymentStatus.Failed)
            return Result.Success();

        var updateResult = UpdateStatus(PaymentStatus.Failed, null, errorMessage);
        if (updateResult.IsFailure)
            return updateResult;

        AddDomainEvent(new PaymentFailedEvent(Id, OrderId, errorMessage));
        return Result.Success();
    }

    public Result MarkAsRefunded(string transactionId)
    {
        if (Status == PaymentStatus.Refunded)
            return Result.Success();

        if (Status != PaymentStatus.Succeeded)
            return Result.Failure(PaymentErrors.InvalidPaymentStatus("refund"));

        if (string.IsNullOrWhiteSpace(transactionId))
            return Result.Failure(PaymentErrors.TokenRequired);

        var updateResult = UpdateStatus(PaymentStatus.Refunded, transactionId);
        if (updateResult.IsFailure)
            return updateResult;

        AddDomainEvent(new PaymentRefundedEvent(Id, OrderId));
        return Result.Success();
    }

    public Result UpdateExternalReference(string externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
            return Result.Failure(PaymentErrors.TokenRequired);

        ExternalReference = externalReference;
        AddDomainEvent(new PaymentUpdatedEvent(Id));
        return Result.Success();
    }

    public Result UpdateMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(PaymentErrors.InvalidMetadataKey);

        Metadata[key] = value;
        AddDomainEvent(new PaymentUpdatedEvent(Id));
        return Result.Success();
    }

    public Result SetPaymentMethod(PaymentMethod paymentMethod)
    {
        if (paymentMethod == null)
            return Result.Failure(PaymentErrors.PaymentMethodNotFound(Guid.Empty));

        if (!paymentMethod.IsActive)
            return Result.Failure(PaymentMethodErrors.InactivePaymentMethod);

        PaymentMethodId = paymentMethod.Id;
        MethodType = paymentMethod.Type;
        Provider = paymentMethod.Provider;
        AddDomainEvent(new PaymentUpdatedEvent(Id));
        return Result.Success();
    }

    public Result Cancel(string reason = null)
    {
        if (Status == PaymentStatus.Succeeded || Status == PaymentStatus.Refunded)
            return Result.Failure(PaymentErrors.InvalidPaymentStatus("cancel"));

        var oldStatus = Status;
        Status = PaymentStatus.Canceled;

        if (reason != null)
            Metadata["cancellationReason"] = reason;

        AddDomainEvent(new PaymentStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new PaymentCancelledEvent(Id, OrderId));
        return Result.Success();
    }
}
