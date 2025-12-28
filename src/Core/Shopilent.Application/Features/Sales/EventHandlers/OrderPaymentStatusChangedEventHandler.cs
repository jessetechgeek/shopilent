using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.Events;

namespace Shopilent.Application.Features.Sales.EventHandlers;

internal sealed class
    OrderPaymentStatusChangedEventHandler : INotificationHandler<
    DomainEventNotification<OrderPaymentStatusChangedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<OrderPaymentStatusChangedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public OrderPaymentStatusChangedEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        ILogger<OrderPaymentStatusChangedEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<OrderPaymentStatusChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation(
            "Order payment status changed. OrderId: {OrderId}, Old Status: {OldStatus}, New Status: {NewStatus}",
            domainEvent.OrderId,
            domainEvent.OldStatus,
            domainEvent.NewStatus);

        try
        {
            // Clear order caches
            await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

            // Get order details
            var order = await _unitOfWork.OrderReader.GetDetailByIdAsync(domainEvent.OrderId, cancellationToken);

            if (order != null && order.UserId.HasValue)
            {
                // Get user information
                var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);

                if (user != null)
                {
                    // Send email based on payment status
                    string subject = $"Order #{order.Id} Payment Update";
                    string message = "";

                    switch (domainEvent.NewStatus)
                    {
                        case PaymentStatus.Succeeded:
                            message =
                                $"Your payment for order #{order.Id} has been successfully processed. Thank you for your purchase!";
                            break;
                        case PaymentStatus.Failed:
                            message =
                                $"We encountered an issue processing your payment for order #{order.Id}. Please update your payment information.";
                            break;
                        case PaymentStatus.Refunded:
                            message = $"Your payment for order #{order.Id} has been refunded.";
                            break;
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderPaymentStatusChangedEvent for OrderId: {OrderId}",
                domainEvent.OrderId);
        }
    }
}
