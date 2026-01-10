using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Specifications;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Domain.Payments.Specifications;

public class SuccessfulPaymentSpecification : Specification<Payment>
{
    public override bool IsSatisfiedBy(Payment payment)
    {
        return payment.Status == PaymentStatus.Succeeded;
    }
}