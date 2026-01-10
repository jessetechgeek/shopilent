using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Domain.Payments.DTOs;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public PaymentMethodType MethodType { get; set; }
    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; }
    public string ExternalReference { get; set; }
    public string TransactionId { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}