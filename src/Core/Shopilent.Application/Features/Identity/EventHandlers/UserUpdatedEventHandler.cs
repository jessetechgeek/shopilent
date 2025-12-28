using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Identity.EventHandlers;

internal sealed class UserUpdatedEventHandler : INotificationHandler<DomainEventNotification<UserUpdatedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly ILogger<UserUpdatedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;

    public UserUpdatedEventHandler(
        IUserReadRepository userReadRepository,
        IOrderReadRepository orderReadRepository,
        ILogger<UserUpdatedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService)
    {
        _userReadRepository = userReadRepository;
        _orderReadRepository = orderReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
    }

    public async Task Handle(DomainEventNotification<UserUpdatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("User updated. UserId: {UserId}", domainEvent.UserId);

        try
        {
            // Clear user caches
            await _cacheService.RemoveAsync($"user-{domainEvent.UserId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("users-*", cancellationToken);

            // Get user details to clear more specific caches
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Clear user-related caches
                await _cacheService.RemoveByPatternAsync($"user-{domainEvent.UserId}-*", cancellationToken);

                // Check if user has orders that might display user information
                var orders = await _orderReadRepository.GetByUserIdAsync(domainEvent.UserId, cancellationToken);
                if (orders != null && orders.Count > 0)
                {
                    // Clear order caches that might display user information
                    foreach (var order in orders)
                    {
                        await _cacheService.RemoveAsync($"order-{order.Id}", cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserUpdatedEvent for UserId: {UserId}", domainEvent.UserId);
        }
    }
}
