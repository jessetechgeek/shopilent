using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Events;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Domain.Payments.Events;

public class PaymentStatusChangedEvent : DomainEvent
{
    public PaymentStatusChangedEvent(Guid paymentId, PaymentStatus oldStatus, PaymentStatus newStatus)
    {
        PaymentId = paymentId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    public Guid PaymentId { get; }
    public PaymentStatus OldStatus { get; }
    public PaymentStatus NewStatus { get; }
}