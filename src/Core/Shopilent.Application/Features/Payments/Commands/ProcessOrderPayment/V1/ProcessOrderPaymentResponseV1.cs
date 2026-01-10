using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Application.Features.Payments.Commands.ProcessOrderPayment.V1;

public sealed class ProcessOrderPaymentResponseV1
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; }
    public PaymentStatus Status { get; init; }
    public PaymentMethodType MethodType { get; init; }
    public PaymentProvider Provider { get; init; }
    public string TransactionId { get; init; }
    public DateTime ProcessedAt { get; init; }
    public string Message { get; init; }
    
    // Enhanced payment processing details
    public string ClientSecret { get; init; }
    public bool RequiresAction { get; init; }
    public string NextActionType { get; init; }
    public string DeclineReason { get; init; }
    public string RiskLevel { get; init; }
    public string FailureReason { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}