using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Specifications;

namespace Shopilent.Domain.Sales.Specifications;

public class PaidOrderSpecification : Specification<Order>
{
    public override bool IsSatisfiedBy(Order order)
    {
        return order.PaymentStatus == PaymentStatus.Succeeded;
    }
}
