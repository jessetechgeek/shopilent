using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Identity.Repositories.Read;

namespace Shopilent.Application.Features.Identity.EventHandlers;

internal sealed class UserCreatedEventHandler : INotificationHandler<DomainEventNotification<UserCreatedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<UserCreatedEventHandler> _logger;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IConfiguration _configuration;

    public UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        IConfiguration configuration)
    {
        _logger = logger;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _unitOfWork = unitOfWork;
        _userReadRepository = userReadRepository;
        _configuration = configuration;
    }

    public async Task Handle(DomainEventNotification<UserCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("User created with ID: {UserId}", domainEvent.UserId);

        try
        {
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found when sending welcome email", domainEvent.UserId);
                return;
            }

            var appUrl = _configuration["AppUrl"] ?? "https://localhost:5001";
            var customerName = $"{user.FirstName} {user.LastName}".Trim();

            var emailBody = _emailTemplateService.BuildWelcomeEmailTemplate(customerName, user.Email, appUrl);

            await _emailService.SendEmailAsync(user.Email, "Welcome to Shopilent!", emailBody, isHtml: true);

            _logger.LogInformation("Welcome email sent to user {Email} (ID: {UserId})", user.Email, domainEvent.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to user with ID: {UserId}", domainEvent.UserId);
        }
    }
}
