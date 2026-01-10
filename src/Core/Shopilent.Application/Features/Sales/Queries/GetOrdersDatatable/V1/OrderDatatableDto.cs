using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.Application.Features.Sales.Queries.GetOrdersDatatable.V1;

public sealed class OrderDatatableDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; }
    public string UserFullName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string ShippingMethod { get; set; }
    public string TrackingNumber { get; set; }
    public int ItemsCount { get; set; }
    public decimal RefundedAmount { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string RefundReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
