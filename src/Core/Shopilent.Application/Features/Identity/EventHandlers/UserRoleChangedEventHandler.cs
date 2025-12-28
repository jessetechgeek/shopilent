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

internal sealed  class UserRoleChangedEventHandler : INotificationHandler<DomainEventNotification<UserRoleChangedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<UserRoleChangedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public UserRoleChangedEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        ILogger<UserRoleChangedEventHandler> logger,
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

    public async Task Handle(DomainEventNotification<UserRoleChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("User role changed. UserId: {UserId}, NewRole: {NewRole}",
            domainEvent.UserId,
            domainEvent.NewRole);

        try
        {
            // Clear user caches
            await _cacheService.RemoveAsync($"user-{domainEvent.UserId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("users-*", cancellationToken);

            // Get user details
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Send role change notification
                string subject = "Your Account Role Has Changed";
                string message = $"Hi {user.FirstName},\n\n" +
                                 $"Your account role on Shopilent has been updated to: {domainEvent.NewRole}\n\n";

                // Add role-specific information
                switch (domainEvent.NewRole)
                {
                    case Domain.Identity.Enums.UserRole.Admin:
                        message +=
                            "As an Administrator, you now have full access to all system features, including user management, " +
                            "content moderation, and system configuration.\n\n";
                        break;
                    case Domain.Identity.Enums.UserRole.Manager:
                        message += "As a Manager, you now have access to order management, product inventory, " +
                                   "and customer service features.\n\n";
                        break;
                    case Domain.Identity.Enums.UserRole.Customer:
                        message += "As a Customer, you can browse products, make purchases, " +
                                   "and manage your account information.\n\n";
                        break;
                }

                message += "If you have any questions about your new role, please contact our support team.\n\n" +
                           "The Shopilent Team";

                await _emailService.SendEmailAsync(user.Email, subject, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserRoleChangedEvent for UserId: {UserId}", domainEvent.UserId);
        }
    }
}
