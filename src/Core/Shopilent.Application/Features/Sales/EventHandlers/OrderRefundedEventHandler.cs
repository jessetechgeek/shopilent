using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Sales.EventHandlers;

internal sealed class OrderRefundedEventHandler : INotificationHandler<DomainEventNotification<OrderRefundedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly IProductVariantWriteRepository _productVariantWriteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderRefundedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderRefundedEventHandler(
        IUserReadRepository userReadRepository,
        IOrderReadRepository orderReadRepository,
        IProductVariantWriteRepository productVariantWriteRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderRefundedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _userReadRepository = userReadRepository;
        _orderReadRepository = orderReadRepository;
        _productVariantWriteRepository = productVariantWriteRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<OrderRefundedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order refunded. OrderId: {OrderId}", domainEvent.OrderId);

        try
        {
            // Get order details
            var order = await _orderReadRepository.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null)
            {
                if (order.Items != null && order.Items.Any())
                {
                    _logger.LogInformation(
                        "Restoring stock for refunded order {OrderId} with {ItemCount} items",
                        domainEvent.OrderId, order.Items.Count);

                    foreach (var item in order.Items)
                    {
                        // Only restore stock for items with variants
                        if (!item.VariantId.HasValue)
                            continue;

                        try
                        {
                            var variant = await _productVariantWriteRepository.GetByIdAsync(
                                item.VariantId.Value,
                                cancellationToken);

                            if (variant == null)
                            {
                                _logger.LogWarning(
                                    "Variant {VariantId} not found when restoring stock for refunded order {OrderId}",
                                    item.VariantId.Value, domainEvent.OrderId);
                                continue;
                            }

                            // Restore stock by adding back the ordered quantity
                            var addStockResult = variant.AddStock(item.Quantity);
                            if (addStockResult.IsFailure)
                            {
                                _logger.LogError(
                                    "Failed to restore stock for variant {VariantId}: {Error}",
                                    variant.Id, addStockResult.Error.Message);
                                continue;
                            }

                            await _productVariantWriteRepository.UpdateAsync(variant, cancellationToken);

                            _logger.LogInformation(
                                "Restored {Quantity} units of stock for variant {VariantId} (SKU: {Sku}) due to refund. New stock: {NewStock}",
                                item.Quantity, variant.Id, variant.Sku, variant.StockQuantity);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error restoring stock for variant {VariantId} in refunded order {OrderId}",
                                item.VariantId.Value, domainEvent.OrderId);
                            // Continue processing other items even if one fails
                        }
                    }

                    // Commit stock restoration changes
                    try
                    {
                        await _unitOfWork.CommitAsync(cancellationToken);
                        _logger.LogInformation(
                            "Successfully committed stock restoration for refunded order {OrderId}",
                            domainEvent.OrderId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to commit stock restoration for refunded order {OrderId}",
                            domainEvent.OrderId);
                    }
                }

                // Clear caches
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                // If order has user, send refund notification email
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                    if (user != null)
                    {
                        string subject = $"Refund Processed for Order #{order.Id}";
                        string message = $"Your refund for order #{order.Id} has been processed. " +
                                         $"The amount of {order.RefundedAmount} {order.Currency} will be " +
                                         $"credited back to your original payment method.";

                        if (!string.IsNullOrEmpty(domainEvent.Reason))
                        {
                            message += $"\n\nReason for refund: {domainEvent.Reason}";
                        }

                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderRefundedEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
