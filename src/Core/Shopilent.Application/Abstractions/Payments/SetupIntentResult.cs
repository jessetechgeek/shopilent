using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Application.Abstractions.Payments;

public class SetupIntentResult
{
    public string SetupIntentId { get; set; }
    public PaymentStatus Status { get; set; }
    public string ClientSecret { get; set; }
    public bool RequiresAction { get; set; }
    public string NextActionType { get; set; }
    public string PaymentMethodId { get; set; }
    public string CustomerId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string Usage { get; set; }
}