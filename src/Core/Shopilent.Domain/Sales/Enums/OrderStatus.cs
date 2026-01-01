namespace Shopilent.Domain.Sales.Enums;

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Returned,
    ReturnedAndRefunded,
    Cancelled
}