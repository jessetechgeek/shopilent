using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Application.Abstractions.Payments;

public class PaymentResult
{
    public string TransactionId { get; set; }
    public PaymentStatus Status { get; set; }
    public string ClientSecret { get; set; }
    public bool RequiresAction { get; set; }
    public string NextActionType { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string DeclineReason { get; set; }
    public string RiskLevel { get; set; }
    public string FailureReason { get; set; }
}