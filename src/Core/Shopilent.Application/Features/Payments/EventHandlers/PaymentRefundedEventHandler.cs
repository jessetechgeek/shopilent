using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Payments.Repositories.Read;

namespace Shopilent.Application.Features.Payments.EventHandlers;

internal sealed  class PaymentRefundedEventHandler : INotificationHandler<DomainEventNotification<PaymentRefundedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentReadRepository _paymentReadRepository;
    private readonly ILogger<PaymentRefundedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;

    public PaymentRefundedEventHandler(
        IUnitOfWork unitOfWork,
        IPaymentReadRepository paymentReadRepository,
        ILogger<PaymentRefundedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService)
    {
        _unitOfWork = unitOfWork;
        _paymentReadRepository = paymentReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
    }

    public async Task Handle(DomainEventNotification<PaymentRefundedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Payment refunded. PaymentId: {PaymentId}, OrderId: {OrderId}",
            domainEvent.PaymentId,
            domainEvent.OrderId);

        try
        {
            // Get payment details
            var payment = await _paymentReadRepository.GetByIdAsync(domainEvent.PaymentId, cancellationToken);

            if (payment != null)
            {
                // Clear payment and order caches
                await _cacheService.RemoveAsync($"payment-{domainEvent.PaymentId}", cancellationToken);
                await _cacheService.RemoveAsync($"order-{domainEvent.OrderId}", cancellationToken);
                await _cacheService.RemoveByPatternAsync("payments-*", cancellationToken);
                await _cacheService.RemoveByPatternAsync("orders-*", cancellationToken);

                // Get the order to update its payment status
                var order = await _unitOfWork.OrderWriter.GetByIdAsync(domainEvent.OrderId, cancellationToken);

                if (order != null)
                {
                    // Update the order's payment status
                    var result = order.UpdatePaymentStatus(PaymentStatus.Refunded);
                    if (result.IsSuccess)
                    {
                        // Update the order
                        await _unitOfWork.OrderWriter.UpdateAsync(order, cancellationToken);

                        // Save changes to persist the updates
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update order payment status. OrderId: {OrderId}, Error: {Error}",
                            domainEvent.OrderId,
                            result.Error?.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentRefundedEvent for PaymentId: {PaymentId}, OrderId: {OrderId}",
                domainEvent.PaymentId,
                domainEvent.OrderId);
        }
    }
}
