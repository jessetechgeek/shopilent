using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Identity.Repositories.Read;

namespace Shopilent.Application.Features.Identity.EventHandlers;

internal sealed  class UserEmailChangedEventHandler : INotificationHandler<DomainEventNotification<UserEmailChangedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<UserEmailChangedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public UserEmailChangedEventHandler(
        IUserReadRepository userReadRepository,
        ILogger<UserEmailChangedEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<UserEmailChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("User email changed. UserId: {UserId}, NewEmail: {NewEmail}",
            domainEvent.UserId,
            domainEvent.NewEmail);

        try
        {
            // Clear user caches
            await _cacheService.RemoveAsync($"user-{domainEvent.UserId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("users-*", cancellationToken);

            // Get user details
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Send verification email
                string subject = "Verify Your New Email Address";
                string message = $"Hi {user.FirstName},\n\n" +
                                 $"You've recently changed your email address on Shopilent. Please verify your new email address by clicking the link below:\n\n" +
                                 $"[Verification Link]\n\n" +
                                 $"If you did not request this change, please contact our support team immediately.\n\n" +
                                 $"The Shopilent Team";

                await _emailService.SendEmailAsync(domainEvent.NewEmail, subject, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserEmailChangedEvent for UserId: {UserId}", domainEvent.UserId);
        }
    }
}
