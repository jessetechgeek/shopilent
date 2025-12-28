using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Identity.Repositories.Read;

namespace Shopilent.Application.Features.Identity.EventHandlers;

internal sealed  class UserEmailVerifiedEventHandler : INotificationHandler<DomainEventNotification<UserEmailVerifiedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<UserEmailVerifiedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public UserEmailVerifiedEventHandler(
        IUserReadRepository userReadRepository,
        ILogger<UserEmailVerifiedEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<UserEmailVerifiedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("User email verified. UserId: {UserId}", domainEvent.UserId);

        try
        {
            // Clear user caches
            await _cacheService.RemoveAsync($"user-{domainEvent.UserId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("users-*", cancellationToken);

            // Get user details
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Send welcome email
                string subject = "Welcome to Shopilent - Your Account is Verified!";
                string message = $"Hi {user.FirstName},\n\n" +
                                 $"Thank you for verifying your email address. Your account is now fully activated.\n\n" +
                                 $"You can now enjoy all features of our platform, including browsing products, making purchases, and tracking your orders.\n\n" +
                                 $"If you have any questions or need assistance, please don't hesitate to contact our support team.\n\n" +
                                 $"Happy shopping!\n\n" +
                                 $"The Shopilent Team";

                await _emailService.SendEmailAsync(user.Email, subject, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserEmailVerifiedEvent for UserId: {UserId}", domainEvent.UserId);
        }
    }
}
