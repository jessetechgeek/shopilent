using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Payments.Events;

namespace Shopilent.Application.Features.Payments.EventHandlers;

internal sealed class
    PaymentMethodRemovedEventHandler : INotificationHandler<DomainEventNotification<PaymentMethodRemovedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<PaymentMethodRemovedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public PaymentMethodRemovedEventHandler(
        IUserReadRepository userReadRepository,
        ILogger<PaymentMethodRemovedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _userReadRepository = userReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<PaymentMethodRemovedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Payment method removed. PaymentMethodId: {PaymentMethodId}, UserId: {UserId}",
            domainEvent.PaymentMethodId,
            domainEvent.UserId);

        try
        {
            // Clear payment method caches
            await _cacheService.RemoveAsync($"payment-method-{domainEvent.PaymentMethodId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("payment-methods-*", cancellationToken);
            await _cacheService.RemoveByPatternAsync($"payment-methods-user-{domainEvent.UserId}", cancellationToken);

            // Get user details
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Notify the user about the removed payment method
                string subject = "Payment Method Removed";
                string message = "Your payment method has been successfully removed from your account. " +
                                 "If you did not perform this action, please contact our support team immediately.";

                await _emailService.SendEmailAsync(user.Email, subject, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing PaymentMethodRemovedEvent for PaymentMethodId: {PaymentMethodId}, UserId: {UserId}",
                domainEvent.PaymentMethodId, domainEvent.UserId);
        }
    }
}
