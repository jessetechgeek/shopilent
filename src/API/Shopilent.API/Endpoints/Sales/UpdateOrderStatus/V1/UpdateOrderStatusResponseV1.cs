using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.API.Endpoints.Sales.UpdateOrderStatus.V1;

public class UpdateOrderStatusResponseV1
{
    public Guid Id { get; init; }
    public OrderStatus Status { get; init; }
    public PaymentStatus PaymentStatus { get; init; }
    public DateTime UpdatedAt { get; init; }
}