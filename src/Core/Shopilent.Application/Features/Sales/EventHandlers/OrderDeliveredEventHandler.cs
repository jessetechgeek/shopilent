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

internal sealed  class OrderDeliveredEventHandler : INotificationHandler<DomainEventNotification<OrderDeliveredEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<OrderDeliveredEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderDeliveredEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        ILogger<OrderDeliveredEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<OrderDeliveredEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order delivered. OrderId: {OrderId}", domainEvent.OrderId);

        try
        {
            // Get order details
            var order = await _unitOfWork.OrderReader.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null)
            {
                // Clear caches
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                // If order has user, send delivery notification and feedback request
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                    if (user != null)
                    {
                        string subject = $"Your Order #{order.Id} Has Been Delivered";
                        string message =
                            $"Your order #{order.Id} has been delivered. Thank you for shopping with us!\n\n" +
                            $"We'd love to hear your feedback on your purchase. Please take a moment to rate your experience.";

                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderDeliveredEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
