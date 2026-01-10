using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Application.Abstractions.Payments;

public class WebhookResult
{
    public string EventId { get; set; }
    public string EventType { get; set; }
    public PaymentProvider Provider { get; set; }
    public string OrderId { get; set; }
    public string TransactionId { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public string CustomerId { get; set; }
    public bool IsProcessed { get; set; }
    public string ProcessingMessage { get; set; }
    public Dictionary<string, object> EventData { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}