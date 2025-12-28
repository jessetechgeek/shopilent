using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Sales.EventHandlers;

internal sealed class OrderCancelledEventHandler : INotificationHandler<DomainEventNotification<OrderCancelledEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly ILogger<OrderCancelledEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderCancelledEventHandler(
        IUserReadRepository userReadRepository,
        IOrderReadRepository orderReadRepository,
        ILogger<OrderCancelledEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _userReadRepository = userReadRepository;
        _orderReadRepository = orderReadRepository;
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
