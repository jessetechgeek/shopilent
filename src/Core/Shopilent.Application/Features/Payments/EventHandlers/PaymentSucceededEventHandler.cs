using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Payments.EventHandlers;

internal sealed class
    PaymentSucceededEventHandler : INotificationHandler<DomainEventNotification<PaymentSucceededEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly IPaymentReadRepository _paymentReadRepository;
    private readonly ILogger<PaymentSucceededEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;

    public PaymentSucceededEventHandler(
        IUnitOfWork unitOfWork,
        IOrderWriteRepository orderWriteRepository,
        IPaymentReadRepository paymentReadRepository,
        ILogger<PaymentSucceededEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService)
    {
        _unitOfWork = unitOfWork;
        _orderWriteRepository = orderWriteRepository;
        _paymentReadRepository = paymentReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
    }

    public async Task Handle(DomainEventNotification<PaymentSucceededEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Payment succeeded. PaymentId: {PaymentId}, OrderId: {OrderId}",
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

                // Get the order to update its status
                var order = await _orderWriteRepository.GetByIdAsync(domainEvent.OrderId, cancellationToken);

                if (order != null)
                {
                    // Mark the order as paid
                    var result = order.MarkAsPaid();
                    if (result.IsSuccess)
                    {
                        // Update the order
                        await _orderWriteRepository.UpdateAsync(order, cancellationToken);

                        // Save changes to persist the updates
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to mark order as paid. OrderId: {OrderId}, Error: {Error}",
                            domainEvent.OrderId,
                            result.Error?.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing PaymentSucceededEvent for PaymentId: {PaymentId}, OrderId: {OrderId}",
                domainEvent.PaymentId,
                domainEvent.OrderId);
        }
    }
}
