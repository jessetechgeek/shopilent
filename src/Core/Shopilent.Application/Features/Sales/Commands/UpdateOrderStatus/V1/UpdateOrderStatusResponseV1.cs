using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.Application.Features.Sales.Commands.UpdateOrderStatus.V1;

public sealed class UpdateOrderStatusResponseV1
{
    public Guid Id { get; init; }
    public OrderStatus Status { get; init; }
    public PaymentStatus PaymentStatus { get; init; }
    public DateTime UpdatedAt { get; init; }
}
