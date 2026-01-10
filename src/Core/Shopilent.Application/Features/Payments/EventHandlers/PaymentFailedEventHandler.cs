using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Events;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Payments.EventHandlers;

internal sealed class PaymentFailedEventHandler : INotificationHandler<DomainEventNotification<PaymentFailedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderWriteRepository _orderWriteRepository;
    private readonly IPaymentReadRepository _paymentReadRepository;
    private readonly ILogger<PaymentFailedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public PaymentFailedEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        IOrderWriteRepository orderWriteRepository,
        IPaymentReadRepository paymentReadRepository,
        ILogger<PaymentFailedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _userReadRepository = userReadRepository;
        _orderWriteRepository = orderWriteRepository;
        _paymentReadRepository = paymentReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<PaymentFailedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Payment failed. PaymentId: {PaymentId}, OrderId: {OrderId}, Error: {ErrorMessage}",
            domainEvent.PaymentId,
            domainEvent.OrderId,
            domainEvent.ErrorMessage);

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
                var order = await _orderWriteRepository.GetByIdAsync(domainEvent.OrderId, cancellationToken);

                if (order != null)
                {
                    // Update the order's payment status
                    var result = order.UpdatePaymentStatus(PaymentStatus.Failed);
                    if (result.IsSuccess)
                    {
                        // Update the order
                        await _orderWriteRepository.UpdateAsync(order, cancellationToken);

                        // Save changes to persist the updates
                        await _unitOfWork.CommitAsync(cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update order payment status. OrderId: {OrderId}, Error: {Error}",
                            domainEvent.OrderId,
                            result.Error?.Message);
                    }

                    // If order has user, send payment failure notification
                    if (order.UserId.HasValue)
                    {
                        var user = await _userReadRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                        if (user != null)
                        {
                            string subject = $"Payment Failed for Order #{order.Id}";
                            string message = $"We were unable to process your payment for order #{order.Id}. " +
                                             $"Please update your payment information or try a different payment method.";

                            if (!string.IsNullOrEmpty(domainEvent.ErrorMessage))
                            {
                                // Sanitize error message for customer-facing communication
                                var sanitizedError = SanitizeErrorMessage(domainEvent.ErrorMessage);
                                if (!string.IsNullOrEmpty(sanitizedError))
                                {
                                    message += $"\n\nReason: {sanitizedError}";
                                }
                            }

                            await _emailService.SendEmailAsync(user.Email, subject, message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentFailedEvent for PaymentId: {PaymentId}, OrderId: {OrderId}",
                domainEvent.PaymentId,
                domainEvent.OrderId);
        }
    }

    private string SanitizeErrorMessage(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return null;

        // List of common payment error messages that are safe to show to customers
        var safeErrors = new Dictionary<string, string>
        {
            { "card_declined", "Your card was declined." },
            { "expired_card", "Your card has expired." },
            { "insufficient_funds", "Insufficient funds in your account." },
            { "incorrect_cvc", "The security code is incorrect." },
            { "processing_error", "There was an error processing your payment." },
            { "invalid_card", "The card information is invalid." }
        };

        // Check if error message contains any of the safe error keys
        foreach (var safeError in safeErrors)
        {
            if (errorMessage.Contains(safeError.Key, StringComparison.OrdinalIgnoreCase))
            {
                return safeError.Value;
            }
        }

        // Default generic message
        return "There was an issue with your payment method.";
    }
}
