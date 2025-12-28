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

internal sealed  class OrderPartiallyRefundedEventHandler : INotificationHandler<DomainEventNotification<OrderPartiallyRefundedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<OrderPartiallyRefundedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderPartiallyRefundedEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        ILogger<OrderPartiallyRefundedEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<OrderPartiallyRefundedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Order partially refunded. OrderId: {OrderId}, Refund Amount: {Amount} {Currency}",
            domainEvent.OrderId,
            domainEvent.Amount.Amount,
            domainEvent.Amount.Currency);

        try
        {
            // Get order details
            var order = await _unitOfWork.OrderReader.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null)
            {
                // Clear caches
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                // If order has user, send partial refund notification email
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                    if (user != null)
                    {
                        string subject = $"Partial Refund Processed for Order #{order.Id}";
                        string message = $"A partial refund of {domainEvent.Amount.Amount} {domainEvent.Amount.Currency} " +
                                         $"for order #{order.Id} has been processed. This amount will be " +
                                         $"credited back to your original payment method.";

                        if (!string.IsNullOrEmpty(domainEvent.Reason))
                        {
                            message += $"\n\nReason for refund: {domainEvent.Reason}";
                        }

                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderPartiallyRefundedEvent for OrderId: {OrderId}", domainEvent.OrderId);
        }
    }
}
