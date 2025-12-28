using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Sales.EventHandlers;

internal sealed class
    OrderCreatedFromCartEventHandler : INotificationHandler<DomainEventNotification<OrderCreatedFromCartEvent>>
{
    private readonly ICartReadRepository _cartReadRepository;
    private readonly ILogger<OrderCreatedFromCartEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;

    public OrderCreatedFromCartEventHandler(
        ICartReadRepository cartReadRepository,
        ILogger<OrderCreatedFromCartEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService)
    {
        _cartReadRepository = cartReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
    }

    public async Task Handle(DomainEventNotification<OrderCreatedFromCartEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order created from cart. OrderId: {OrderId}, CartId: {CartId}",
            domainEvent.OrderId,
            domainEvent.CartId);

        try
        {
            // Clear cart cache
            await _cacheService.RemoveAsync($"cart-{domainEvent.CartId}", cancellationToken);

            // Clear order caches
            await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

            // Get cart details to check if it's associated with a user
            var cart = await _cartReadRepository.GetByIdAsync(domainEvent.CartId, cancellationToken);

            if (cart != null && cart.UserId.HasValue)
            {
                // Clear user cart cache
                await _cacheService.RemoveByPatternAsync($"user-{cart.UserId.Value}-cart", cancellationToken);

                // Clear user orders cache
                await _cacheService.RemoveByPatternAsync($"user-{cart.UserId.Value}-orders", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreatedFromCartEvent for OrderId: {OrderId}, CartId: {CartId}",
                domainEvent.OrderId, domainEvent.CartId);
        }
    }
}
