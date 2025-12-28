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

internal sealed  class OrderPaidEventHandler : INotificationHandler<DomainEventNotification<OrderPaidEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<OrderPaidEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderPaidEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        ILogger<OrderPaidEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<OrderPaidEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order paid. OrderId: {OrderId}", domainEvent.OrderId);

        try
        {
            // Get order details
            var order = await _unitOfWork.OrderReader.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null)
            {
                // Clear caches
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                // If order has user, send receipt email
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                    if (user != null)
                    {
                        // In real app, use a template for this email with proper receipt formatting
                        string subject = $"Receipt for Order #{order.Id}";
                        string message =
                            $"Thank you for your purchase! Your payment of {order.Total} {order.Currency} has been processed successfully.";

                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderPaidEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
