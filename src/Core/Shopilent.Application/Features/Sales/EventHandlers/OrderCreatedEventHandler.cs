using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Sales.Events;

namespace Shopilent.Application.Features.Sales.EventHandlers;

internal sealed class OrderCreatedEventHandler : INotificationHandler<DomainEventNotification<OrderCreatedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<OrderCreatedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderCreatedEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        ILogger<OrderCreatedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _userReadRepository = userReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<OrderCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order created with ID: {OrderId}", domainEvent.OrderId);

        try
        {
            // Get order details
            var order = await _unitOfWork.OrderReader.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null && order.UserId.HasValue)
            {
                // Get user information
                var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);

                if (user != null)
                {
                    // Send order confirmation email
                    await _emailService.SendEmailAsync(
                        user.Email,
                        $"Order Confirmation #{order.Id}",
                        $"Thank you for your order. Your order #{order.Id} has been received and is being processed.");
                }
            }

            // Clear order caches
            await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - we don't want to fail the entire event processing
            _logger.LogError(ex, "Error processing OrderCreatedEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
