using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.API.Endpoints.Payments.ProcessWebhook.V1;

public class ProcessWebhookResponseV1
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public PaymentProvider Provider { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public PaymentStatus? PaymentStatus { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public string ProcessingMessage { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}