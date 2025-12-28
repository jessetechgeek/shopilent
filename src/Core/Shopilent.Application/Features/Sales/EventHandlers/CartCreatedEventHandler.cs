using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Sales.EventHandlers;

internal sealed class CartCreatedEventHandler : INotificationHandler<DomainEventNotification<CartCreatedEvent>>
{
    private readonly ICartReadRepository _cartReadRepository;
    private readonly ILogger<CartCreatedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;

    public CartCreatedEventHandler(
        ICartReadRepository cartReadRepository,
        ILogger<CartCreatedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService)
    {
        _cartReadRepository = cartReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
    }

    public async Task Handle(DomainEventNotification<CartCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Cart created with ID: {CartId}", domainEvent.CartId);

        // Invalidate cart caches
        await _cacheService.RemoveByPatternAsync("carts-*", cancellationToken);

        // Get cart details to check if it's associated with a user
        var cart = await _cartReadRepository.GetByIdAsync(domainEvent.CartId, cancellationToken);

        if (cart != null && cart.UserId.HasValue)
        {
            // Clear user cart cache
            await _cacheService.RemoveByPatternAsync($"user-{cart.UserId.Value}-cart", cancellationToken);
        }
    }
}
