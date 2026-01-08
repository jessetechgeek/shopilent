using Shopilent.Domain.Common.Events;
using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Domain.Sales.Events;

public class OrderPartiallyRefundedEvent : DomainEvent
{
    public OrderPartiallyRefundedEvent(Guid orderId, Money amount, string reason = null)
    {
        OrderId = orderId;
        Amount = amount;
        Reason = reason;
    }

    public Guid OrderId { get; }
    public Money Amount { get; }
    public string Reason { get; }
}
