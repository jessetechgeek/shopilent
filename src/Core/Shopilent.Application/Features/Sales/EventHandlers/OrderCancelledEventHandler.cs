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

internal sealed class OrderCancelledEventHandler : INotificationHandler<DomainEventNotification<OrderCancelledEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly IProductVariantWriteRepository _productVariantWriteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderCancelledEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderCancelledEventHandler(
        IUserReadRepository userReadRepository,
        IOrderReadRepository orderReadRepository,
        IProductVariantWriteRepository productVariantWriteRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCancelledEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<OrderCancelledEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order cancelled. OrderId: {OrderId}", domainEvent.OrderId);

        try
        {
            // Get order details
            var order = await _orderReadRepository.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null)
            {
                if (order.Items != null && order.Items.Any())
                {
                    _logger.LogInformation(
                        "Restoring stock for cancelled order {OrderId} with {ItemCount} items",
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
                                    "Variant {VariantId} not found when restoring stock for cancelled order {OrderId}",
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
                                "Restored {Quantity} units of stock for variant {VariantId} (SKU: {Sku}). New stock: {NewStock}",
                                item.Quantity, variant.Id, variant.Sku, variant.StockQuantity);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error restoring stock for variant {VariantId} in cancelled order {OrderId}",
                                item.VariantId.Value, domainEvent.OrderId);
                            // Continue processing other items even if one fails
                        }
                    }

                    // Commit stock restoration changes
                    try
                    {
                        await _unitOfWork.CommitAsync(cancellationToken);
                        _logger.LogInformation(
                            "Successfully committed stock restoration for order {OrderId}",
                            domainEvent.OrderId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to commit stock restoration for order {OrderId}",
                            domainEvent.OrderId);
                    }
                }

                // Clear caches
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                // Extract cancellation reason if available
                string cancellationReason = null;
                if (order.Metadata != null && order.Metadata.ContainsKey("cancellationReason"))
                {
                    cancellationReason = order.Metadata["cancellationReason"]?.ToString();
                }

                // If order has user, send cancellation notification
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                    if (user != null)
                    {
                        string subject = $"Your Order #{order.Id} Has Been Cancelled";
                        string message = $"Your order #{order.Id} has been cancelled.";

                        if (!string.IsNullOrEmpty(cancellationReason))
                        {
                            message += $" Reason: {cancellationReason}";
                        }

                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCancelledEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
