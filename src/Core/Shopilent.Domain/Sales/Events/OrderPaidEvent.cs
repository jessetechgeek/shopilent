using Shopilent.Domain.Common.Events;

namespace Shopilent.Domain.Sales.Events;

public class OrderPaidEvent : DomainEvent
{
    public OrderPaidEvent(Guid orderId)
    {
        OrderId = orderId;
    }

    public Guid OrderId { get; }
}
