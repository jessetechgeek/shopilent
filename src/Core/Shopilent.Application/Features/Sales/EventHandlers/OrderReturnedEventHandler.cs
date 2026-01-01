using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Sales.EventHandlers;

internal sealed class OrderReturnedEventHandler : INotificationHandler<DomainEventNotification<OrderReturnedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly ILogger<OrderReturnedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;

    public OrderReturnedEventHandler(
        IUserReadRepository userReadRepository,
        IOrderReadRepository orderReadRepository,
        ILogger<OrderReturnedEventHandler> logger,
        ICacheService cacheService,
        IEmailService emailService)
    {
        _userReadRepository = userReadRepository;
        _orderReadRepository = orderReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<OrderReturnedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order returned. OrderId: {OrderId}, Reason: {Reason}",
            domainEvent.OrderId, domainEvent.ReturnReason);

        try
        {
            // Get order details
            var order = await _orderReadRepository.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null)
            {
                // Clear caches
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                _logger.LogInformation(
                    "Cleared cache for returned order {OrderId}",
                    domainEvent.OrderId);

                // Send return confirmation email to customer
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                    if (user != null)
                    {
                        string subject = $"Return Confirmed for Order #{order.Id}";
                        string message = $"Your return for order #{order.Id} has been confirmed. " +
                                         $"We will process your refund once we receive the items.";

                        if (!string.IsNullOrEmpty(domainEvent.ReturnReason))
                        {
                            message += $"\n\nReturn reason: {domainEvent.ReturnReason}";
                        }

                        await _emailService.SendEmailAsync(user.Email, subject, message);

                        _logger.LogInformation(
                            "Sent return confirmation email to {Email} for order {OrderId}",
                            user.Email, domainEvent.OrderId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderReturnedEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
