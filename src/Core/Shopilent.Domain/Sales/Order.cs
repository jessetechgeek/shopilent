using System.Text.Json;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Events;

namespace Shopilent.Domain.Sales;

public class Order : AggregateRoot
{
    private Order()
    {
        // Required by EF Core
        _items = new List<OrderItem>();
        InitializeRefundProperties();
    }

    private Order(
        Guid userId,
        Guid shippingAddressId,
        Guid billingAddressId,
        Money subtotal,
        Money tax,
        Money shippingCost,
        string shippingMethod = null)
    {
        UserId = userId;
        ShippingAddressId = shippingAddressId;
        BillingAddressId = billingAddressId;
        Subtotal = subtotal;
        Tax = tax;
        ShippingCost = shippingCost;
        Total = subtotal.Add(tax).Add(shippingCost);
        ShippingMethod = shippingMethod;
        Status = OrderStatus.Pending;
        PaymentStatus = PaymentStatus.Pending;
        Metadata = new Dictionary<string, object>();

        _items = new List<OrderItem>();
        InitializeRefundProperties();
    }

    public static Result<Order> Create(
        Guid userId,
        Guid shippingAddressId,
        Guid billingAddressId,
        Money subtotal,
        Money tax,
        Money shippingCost,
        string shippingMethod = null)
    {
        if (subtotal == null)
            return Result.Failure<Order>(PaymentErrors.NegativeAmount);

        if (tax == null)
            return Result.Failure<Order>(PaymentErrors.NegativeAmount);

        if (shippingCost == null)
            return Result.Failure<Order>(PaymentErrors.NegativeAmount);

        if (shippingAddressId == Guid.Empty)
            return Result.Failure<Order>(OrderErrors.ShippingAddressRequired);

        var order = new Order(userId, shippingAddressId, billingAddressId, subtotal, tax, shippingCost, shippingMethod);
        order.AddDomainEvent(new OrderCreatedEvent(order.Id));
        return Result.Success(order);
    }

    public static Result<Order> CreatePaidOrder(
        Guid userId,
        Guid shippingAddressId,
        Guid billingAddressId,
        Money subtotal,
        Money tax,
        Money shippingCost,
        string shippingMethod = null)
    {
        var result = Create(userId, shippingAddressId, billingAddressId, subtotal, tax, shippingCost, shippingMethod);
        if (result.IsFailure)
            return result;

        var order = result.Value;
        var markAsPaidResult = order.MarkAsPaid();
        if (markAsPaidResult.IsFailure)
            return Result.Failure<Order>(markAsPaidResult.Error);

        return Result.Success(order);
    }

    public Guid? UserId { get; private set; }
    public Guid? BillingAddressId { get; private set; }
    public Guid? ShippingAddressId { get; private set; }
    public Guid? PaymentMethodId { get; private set; }
    public Money Subtotal { get; private set; }
    public Money Tax { get; private set; }
    public Money ShippingCost { get; private set; }
    public Money Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public string ShippingMethod { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

    // Refund-related properties
    public Money RefundedAmount { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public string RefundReason { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private void InitializeRefundProperties()
    {
        if (Total != null)
            RefundedAmount = Money.Zero(Total.Currency);
        else
            RefundedAmount = Money.Zero("USD");
    }

    public Result<OrderItem> AddItem(Product product, int quantity, Money unitPrice, ProductVariant variant = null)
    {
        if (product == null)
            return Result.Failure<OrderItem>(ProductErrors.NotFound(Guid.Empty));

        if (quantity <= 0)
            return Result.Failure<OrderItem>(OrderErrors.InvalidQuantity);

        if (unitPrice == null)
            return Result.Failure<OrderItem>(OrderErrors.NegativeAmount);

        if (Status != OrderStatus.Pending)
            return Result.Failure<OrderItem>(OrderErrors.InvalidOrderStatus("add item"));

        var item = OrderItem.Create(this, product, quantity, unitPrice, variant);
        _items.Add(item);

        RecalculateOrderTotals();

        AddDomainEvent(new OrderItemAddedEvent(Id, item.Id));
        return Result.Success(item);
    }

    public Result UpdateOrderStatus(OrderStatus status)
    {
        if (Status == status)
            return Result.Success();

        var oldStatus = Status;
        Status = status;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, status));
        return Result.Success();
    }

    public Result UpdatePaymentStatus(PaymentStatus status)
    {
        if (PaymentStatus == status)
            return Result.Success();

        var oldStatus = PaymentStatus;
        PaymentStatus = status;

        AddDomainEvent(new OrderPaymentStatusChangedEvent(Id, oldStatus, status));
        return Result.Success();
    }

    public Result MarkAsPaid()
    {
        if (PaymentStatus == PaymentStatus.Succeeded)
            return Result.Success();

        // Store old status values before changing them
        var oldPaymentStatus = PaymentStatus;
        var oldStatus = Status;

        // Update statuses
        PaymentStatus = PaymentStatus.Succeeded;
        if (Status == OrderStatus.Pending)
            Status = OrderStatus.Processing;

        // Add domain events
        if (oldPaymentStatus != PaymentStatus)
            AddDomainEvent(new OrderPaymentStatusChangedEvent(Id, oldPaymentStatus, PaymentStatus));

        if (oldStatus != Status)
            AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));

        // Add the OrderPaidEvent
        AddDomainEvent(new OrderPaidEvent(Id));
        return Result.Success();
    }

    public Result MarkAsShipped(string trackingNumber = null)
    {
        if (Status == OrderStatus.Shipped)
            return Result.Success();

        if (Status == OrderStatus.Cancelled)
            return Result.Failure(OrderErrors.InvalidOrderStatus("ship - cancelled orders cannot be shipped"));

        if (Status == OrderStatus.Delivered)
            return Result.Failure(OrderErrors.InvalidOrderStatus("ship - order already delivered"));

        if (PaymentStatus != PaymentStatus.Succeeded)
            return Result.Failure(OrderErrors.PaymentRequired);

        var oldStatus = Status;
        Status = OrderStatus.Shipped;

        if (trackingNumber != null)
            Metadata["trackingNumber"] = trackingNumber;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new OrderShippedEvent(Id));
        return Result.Success();
    }

    public Result MarkAsDelivered()
    {
        if (Status == OrderStatus.Delivered)
            return Result.Success();

        if (Status != OrderStatus.Shipped)
            return Result.Failure(OrderErrors.InvalidOrderStatus("mark as delivered"));

        var oldStatus = Status;
        Status = OrderStatus.Delivered;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new OrderDeliveredEvent(Id));
        return Result.Success();
    }

    public Result MarkAsReturned(string returnReason = null)
    {
        if (Status == OrderStatus.Returned)
            return Result.Success(); // Idempotent

        // Only delivered orders can be marked as returned
        if (Status != OrderStatus.Delivered)
            return Result.Failure(
                OrderErrors.InvalidOrderStatus("mark as returned - only delivered orders can be returned"));

        var oldStatus = Status;
        Status = OrderStatus.Returned;

        if (returnReason != null)
            Metadata["returnReason"] = returnReason;

        Metadata["returnedAt"] = DateTime.UtcNow;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new OrderReturnedEvent(Id, returnReason));

        return Result.Success();
    }

    public Result Cancel(string reason = null, bool isAdminOrManager = false)
    {
        if (Status == OrderStatus.Cancelled)
            return Result.Success();

        // Role-based cancellation rules
        if (!isAdminOrManager)
        {
            // Customers can only cancel orders that are Pending or Processing
            if (Status != OrderStatus.Pending && Status != OrderStatus.Processing)
                return Result.Failure(
                    OrderErrors.InvalidOrderStatus(
                        "cancel - only pending or processing orders can be cancelled by customers"));
        }
        else
        {
            // Admins/Managers can cancel orders up to Shipped status, but not Delivered
            if (Status == OrderStatus.Delivered)
                return Result.Failure(OrderErrors.InvalidOrderStatus("cancel - delivered orders cannot be cancelled"));
        }

        var oldStatus = Status;
        Status = OrderStatus.Cancelled;

        if (reason != null)
            Metadata["cancellationReason"] = reason;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new OrderCancelledEvent(Id));
        return Result.Success();
    }

    public Result UpdateMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(OrderErrors.InvalidMetadataKey);

        Metadata[key] = value;
        return Result.Success();
    }

    public Result SetPaymentMethod(Guid paymentMethodId)
    {
        if (paymentMethodId == Guid.Empty)
            return Result.Failure(PaymentErrors.PaymentMethodNotFound(paymentMethodId));

        PaymentMethodId = paymentMethodId;
        return Result.Success();
    }

    // Method to process a full refund
    public Result ProcessRefund(string reason = null)
    {
        // Can't refund if already fully refunded (check this first for more specific error)
        if (RefundedAmount != null && RefundedAmount.Amount == Total.Amount)
            return Result.Failure(OrderErrors.OrderAlreadyRefunded);

        // Only paid orders can be refunded
        if (PaymentStatus != PaymentStatus.Succeeded)
            return Result.Failure(OrderErrors.InvalidOrderStatus("refund"));

        // Can refund orders that are Processing, Shipped, Delivered, or Returned
        // Cannot refund Pending (not yet paid) or Cancelled orders
        if (Status == OrderStatus.Pending)
            return Result.Failure(OrderErrors.InvalidOrderStatus("refund - order must be paid first"));

        if (Status == OrderStatus.Cancelled)
            return Result.Failure(OrderErrors.InvalidOrderStatus("refund a cancelled order"));

        // Update order properties
        RefundedAmount = Total;
        RefundedAt = DateTime.UtcNow;
        RefundReason = reason;

        // Update order status
        var oldStatus = Status;
        var oldPaymentStatus = PaymentStatus;

        // Preserve the Returned status for analytics by using ReturnedAndRefunded
        Status = oldStatus == OrderStatus.Returned
            ? OrderStatus.ReturnedAndRefunded
            : OrderStatus.Cancelled;

        PaymentStatus = PaymentStatus.Refunded;

        // Add a note to metadata about the refund
        Metadata["refunded"] = true;
        Metadata["refundedAt"] = RefundedAt;
        if (!string.IsNullOrEmpty(reason))
            Metadata["refundReason"] = reason;

        // Raise domain events
        AddDomainEvent(new OrderRefundedEvent(Id, reason));
        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new OrderPaymentStatusChangedEvent(Id, oldPaymentStatus, PaymentStatus));

        return Result.Success();
    }

    // Method to process a partial refund
    public Result ProcessPartialRefund(Money amount, string reason = null)
    {
        // Only paid orders can be refunded
        if (PaymentStatus != PaymentStatus.Succeeded)
            return Result.Failure(OrderErrors.InvalidOrderStatus("refund"));

        // Can't refund cancelled orders
        if (Status == OrderStatus.Cancelled)
            return Result.Failure(OrderErrors.InvalidOrderStatus("refund a cancelled order"));

        // Validate refund amount
        if (amount == null || amount.Amount <= 0)
            return Result.Failure(OrderErrors.InvalidAmount);

        // Check if currencies match
        if (amount.Currency != Total.Currency)
            return Result.Failure(OrderErrors.CurrencyMismatch);

        // Can't refund more than the total
        if (amount.Amount > Total.Amount)
            return Result.Failure(OrderErrors.InvalidAmount);

        // If RefundedAmount hasn't been initialized yet
        if (RefundedAmount == null)
            RefundedAmount = Money.Zero(Total.Currency);

        // Calculate new total refunded amount
        var newRefundedAmount = RefundedAmount.Add(amount);

        // Can't refund more than the total
        if (newRefundedAmount.Amount > Total.Amount)
            return Result.Failure(OrderErrors.InvalidAmount);

        // Update order properties
        RefundedAmount = newRefundedAmount;
        RefundedAt = DateTime.UtcNow;

        // If it's a full refund now, cancel the order
        if (RefundedAmount.Amount == Total.Amount)
        {
            var oldStatus = Status;
            var oldPaymentStatus = PaymentStatus;
            Status = OrderStatus.Cancelled;
            PaymentStatus = PaymentStatus.Refunded;
            RefundReason = reason;

            // Add a note to metadata about the refund
            Metadata["refunded"] = true;
            Metadata["refundedAt"] = RefundedAt;
            if (!string.IsNullOrEmpty(reason))
                Metadata["refundReason"] = reason;

            // Raise domain events for full refund
            AddDomainEvent(new OrderRefundedEvent(Id, reason));
            AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
            AddDomainEvent(new OrderPaymentStatusChangedEvent(Id, oldPaymentStatus, PaymentStatus));
        }
        else
        {
            // For partial refunds, just add to metadata
            List<Dictionary<string, object>> refunds;

            if (Metadata.ContainsKey("partialRefunds"))
            {
                // Handle deserialization from database (JsonElement) or in-memory list
                var existingRefunds = Metadata["partialRefunds"];
                if (existingRefunds is List<Dictionary<string, object>> list)
                {
                    refunds = list;
                }
                else if (existingRefunds is JsonElement jsonElement)
                {
                    // Deserialize from JsonElement
                    refunds = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                        jsonElement.GetRawText()) ?? new List<Dictionary<string, object>>();
                }
                else
                {
                    // Fallback: create new list
                    refunds = new List<Dictionary<string, object>>();
                }

                refunds.Add(new Dictionary<string, object>
                {
                    { "amount", amount.Amount },
                    { "currency", amount.Currency },
                    { "date", DateTime.UtcNow },
                    { "reason", reason ?? "Partial refund" }
                });

                Metadata["partialRefunds"] = refunds;
            }
            else
            {
                refunds = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        { "amount", amount.Amount },
                        { "currency", amount.Currency },
                        { "date", DateTime.UtcNow },
                        { "reason", reason ?? "Partial refund" }
                    }
                };
                Metadata["partialRefunds"] = refunds;
            }

            // Raise domain event for partial refund
            AddDomainEvent(new OrderPartiallyRefundedEvent(Id, amount, reason));
        }

        return Result.Success();
    }

    // Method to modify an existing order item's quantity
    public Result UpdateOrderItemQuantity(Guid itemId, int quantity)
    {
        if (Status != OrderStatus.Pending)
            return Result.Failure(OrderErrors.InvalidOrderStatus("update item quantity"));

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            return Result.Failure(OrderErrors.ItemNotFound(itemId));

        if (quantity <= 0)
        {
            // Remove the item if quantity is 0 or negative
            _items.Remove(item);
            AddDomainEvent(new OrderItemRemovedEvent(Id, itemId));
            return Result.Failure(OrderErrors.InvalidQuantity);
        }
        else
        {
            // Update the item quantity
            var updateResult = item.UpdateQuantity(quantity);
            if (updateResult.IsFailure)
                return updateResult;

            AddDomainEvent(new OrderItemUpdatedEvent(Id, itemId));
        }

        // Recalculate order totals after item changes
        RecalculateOrderTotals();

        return Result.Success();
    }

    // Method to remove an item from an order
    public Result RemoveOrderItem(Guid itemId)
    {
        if (Status != OrderStatus.Pending)
            return Result.Failure(OrderErrors.InvalidOrderStatus("remove item"));

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            return Result.Failure(OrderErrors.ItemNotFound(itemId));

        _items.Remove(item);

        // Recalculate order totals after item removal
        RecalculateOrderTotals();

        AddDomainEvent(new OrderItemRemovedEvent(Id, itemId));
        return Result.Success();
    }

    private void RecalculateOrderTotals()
    {
        // Recalculate based on items
        var newSubtotal = Money.Zero(Subtotal.Currency);

        foreach (var item in _items)
        {
            newSubtotal = newSubtotal.Add(item.TotalPrice);
        }

        Subtotal = newSubtotal;
        Total = Subtotal.Add(Tax).Add(ShippingCost);
    }

    public Result CancelOrderItem(Guid itemId, string reason = null)
    {
        if (Status != OrderStatus.Pending && Status != OrderStatus.Processing)
            return Result.Failure(OrderErrors.InvalidOrderStatus("cancel item"));

        var item = _items.Find(i => i.Id == itemId);
        if (item == null)
            return Result.Failure(OrderErrors.ItemNotFound(itemId));

        AddDomainEvent(new OrderItemCancelledEvent(Id, itemId));
        return Result.Success();
    }
}
