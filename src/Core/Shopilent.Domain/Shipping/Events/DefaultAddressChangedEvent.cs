using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Events;

namespace Shopilent.Domain.Shipping.Events;

public class DefaultAddressChangedEvent : DomainEvent
{
    public DefaultAddressChangedEvent(Guid addressId, Guid userId)
    {
        AddressId = addressId;
        UserId = userId;
    }

    public Guid AddressId { get; }
    public Guid UserId { get; }
}