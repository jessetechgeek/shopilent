using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Infrastructure.Payments.Models;

public class PaymentRequest
{
    public Money Amount { get; init; }
    public PaymentMethodType MethodType { get; init; }
    public string PaymentMethodToken { get; init; }
    public string CustomerId { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public bool SavePaymentMethod { get; init; }
}
