using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Payments.Repositories.Read;

namespace Shopilent.Application.Features.Payments.EventHandlers;

internal sealed class PaymentCreatedEventHandler : INotificationHandler<DomainEventNotification<PaymentCreatedEvent>>
{
    private readonly IPaymentReadRepository _paymentReadRepository;
    private readonly ILogger<PaymentCreatedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;

    public PaymentCreatedEventHandler(
        IPaymentReadRepository paymentReadRepository,
        ILogger<PaymentCreatedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService)
    {
        _paymentReadRepository = paymentReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
    }

    public async Task Handle(DomainEventNotification<PaymentCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Payment created with ID: {PaymentId}", domainEvent.PaymentId);

        try
        {
            // Get payment details
            var payment = await _paymentReadRepository.GetByIdAsync(domainEvent.PaymentId, cancellationToken);

            if (payment != null)
            {
                // Clear payment caches
                await _cacheService.RemoveByPatternAsync("payment-*", cancellationToken);
                await _cacheService.RemoveByPatternAsync("payments-*", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentCreatedEvent for PaymentId: {PaymentId}",
                domainEvent.PaymentId);
        }
    }
}
