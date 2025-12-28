using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Payments.Repositories.Read;

namespace Shopilent.Application.Features.Payments.EventHandlers;

internal sealed class
    PaymentMethodCreatedEventHandler : INotificationHandler<DomainEventNotification<PaymentMethodCreatedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IPaymentMethodReadRepository _paymentMethodReadRepository;
    private readonly ILogger<PaymentMethodCreatedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public PaymentMethodCreatedEventHandler(
        IUserReadRepository userReadRepository,
        IPaymentMethodReadRepository paymentMethodReadRepository,
        ILogger<PaymentMethodCreatedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _userReadRepository = userReadRepository;
        _paymentMethodReadRepository = paymentMethodReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<PaymentMethodCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Payment method created. PaymentMethodId: {PaymentMethodId}, UserId: {UserId}",
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
                // Get payment method details
                var paymentMethod =
                    await _paymentMethodReadRepository.GetByIdAsync(domainEvent.PaymentMethodId, cancellationToken);

                if (paymentMethod != null)
                {
                    // Notify the user about the added payment method
                    string subject = "New Payment Method Added";
                    string message =
                        $"A new payment method ({paymentMethod.DisplayName}) has been added to your account. " +
                        "If you did not perform this action, please contact our support team immediately.";

                    await _emailService.SendEmailAsync(user.Email, subject, message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing PaymentMethodCreatedEvent for PaymentMethodId: {PaymentMethodId}, UserId: {UserId}",
                domainEvent.PaymentMethodId, domainEvent.UserId);
        }
    }
}
