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

internal sealed class OrderShippedEventHandler : INotificationHandler<DomainEventNotification<OrderShippedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly ILogger<OrderShippedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderShippedEventHandler(
        IUserReadRepository userReadRepository,
        IOrderReadRepository orderReadRepository,
        ILogger<OrderShippedEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<OrderShippedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order shipped. OrderId: {OrderId}", domainEvent.OrderId);

        try
        {
            // Get order details
            var order = await _orderReadRepository.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null)
            {
                // Clear caches
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                // Extract tracking number if available
                string trackingNumber = null;
                if (order.Metadata != null && order.Metadata.ContainsKey("trackingNumber"))
                {
                    trackingNumber = order.Metadata["trackingNumber"]?.ToString();
                }
                else
                {
                    trackingNumber = order.TrackingNumber;
                }

                // If order has user, send shipping notification email
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                    if (user != null)
                    {
                        string subject = $"Your Order #{order.Id} Has Shipped";
                        string message = $"Good news! Your order #{order.Id} is on its way.";

                        if (!string.IsNullOrEmpty(trackingNumber))
                        {
                            message += $" You can track your package with tracking number: {trackingNumber}";
                        }

                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderShippedEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
