using Shopilent.Domain.Common.Events;

namespace Shopilent.Domain.Sales.Events;

public class OrderReturnedEvent : DomainEvent
{
    public OrderReturnedEvent(Guid orderId, string returnReason = null)
    {
        OrderId = orderId;
        ReturnReason = returnReason;
    }

    public Guid OrderId { get; }
    public string ReturnReason { get; }
}
