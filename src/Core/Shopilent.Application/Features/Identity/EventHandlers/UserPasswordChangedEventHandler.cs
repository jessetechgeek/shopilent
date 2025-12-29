using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;

namespace Shopilent.Application.Features.Identity.EventHandlers;

internal sealed class
    UserPasswordChangedEventHandler : INotificationHandler<DomainEventNotification<UserPasswordChangedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly IRefreshTokenWriteRepository _refreshTokenWriteRepository;
    private readonly IRefreshTokenReadRepository _refreshTokenReadRepository;
    private readonly ILogger<UserPasswordChangedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public UserPasswordChangedEventHandler(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        IRefreshTokenWriteRepository refreshTokenWriteRepository,
        IRefreshTokenReadRepository refreshTokenReadRepository,
        ILogger<UserPasswordChangedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _userReadRepository = userReadRepository;
        _refreshTokenWriteRepository = refreshTokenWriteRepository;
        _refreshTokenReadRepository = refreshTokenReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<UserPasswordChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("User password changed. UserId: {UserId}", domainEvent.UserId);

        try
        {
            // Get user details
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Revoke all active refresh tokens for this user
                var refreshTokens =
                    await _refreshTokenReadRepository.GetActiveTokensAsync(domainEvent.UserId, cancellationToken);
                if (refreshTokens != null && refreshTokens.Count > 0)
                {
                    foreach (var token in refreshTokens)
                    {
                        // Get the token from the write repository to revoke it
                        var refreshToken = await _refreshTokenWriteRepository.GetByIdAsync(token.Id, cancellationToken);
                        if (refreshToken != null)
                        {
                            refreshToken.Revoke("Password changed");
                            await _refreshTokenWriteRepository.UpdateAsync(refreshToken, cancellationToken);
                        }
                    }

                    // Save changes to persist token revocations
                    await _unitOfWork.CommitAsync(cancellationToken);
                }

                // Send notification email
                string subject = "Your Password Has Been Changed";
                string message = $"Hi {user.FirstName},\n\n" +
                                 $"Your password for Shopilent has been successfully changed.\n\n" +
                                 $"If you did not request this change, please contact our support team immediately.\n\n" +
                                 $"The Shopilent Team";

                await _emailService.SendEmailAsync(user.Email, subject, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserPasswordChangedEvent for UserId: {UserId}", domainEvent.UserId);
        }
    }
}
