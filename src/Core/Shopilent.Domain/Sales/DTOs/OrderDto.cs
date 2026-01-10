using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Shipping.DTOs;

namespace Shopilent.Domain.Sales.DTOs;

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? BillingAddressId { get; set; }
    public Guid? ShippingAddressId { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string ShippingMethod { get; set; }
    public AddressDto ShippingAddress { get; set; }
    public AddressDto BillingAddress { get; set; }
    public string TrackingNumber { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public decimal RefundedAmount { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string RefundReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
