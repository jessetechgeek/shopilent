using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Shipping.Events;
using Shopilent.Domain.Shipping.Repositories.Read;

namespace Shopilent.Application.Features.Shipping.EventHandlers;

internal sealed class
    DefaultAddressChangedEventHandler : INotificationHandler<DomainEventNotification<DefaultAddressChangedEvent>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IAddressReadRepository _addressReadRepository;
    private readonly ILogger<DefaultAddressChangedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public DefaultAddressChangedEventHandler(
        IUserReadRepository userReadRepository,
        IAddressReadRepository addressReadRepository,
        ILogger<DefaultAddressChangedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _userReadRepository = userReadRepository;
        _addressReadRepository = addressReadRepository;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
        _emailService = emailService;
    }

    public async Task Handle(DomainEventNotification<DefaultAddressChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation(
            "Default address changed. AddressId: {AddressId}, UserId: {UserId}",
            domainEvent.AddressId,
            domainEvent.UserId);

        try
        {
            // Clear all default address caches for this user (all types)
            await _cacheService.RemoveByPatternAsync($"default-address-*-{domainEvent.UserId}",
                cancellationToken);

            // Clear all user addresses cache as the default flag has changed
            await _cacheService.RemoveByPatternAsync($"user-addresses-{domainEvent.UserId}",
                cancellationToken);

            // Get user details
            var user = await _userReadRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Get the address details
                var address = await _addressReadRepository.GetByIdAsync(domainEvent.AddressId, cancellationToken);

                if (address != null)
                {
                    // Send notification email
                    string subject = "Default Address Updated";
                    string message = "Your default address has been updated. " +
                                     "This address will be used for future orders.";

                    await _emailService.SendEmailAsync(user.Email, subject, message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing DefaultAddressChangedEvent for AddressId: {AddressId}, UserId: {UserId}",
                domainEvent.AddressId, domainEvent.UserId);
        }
    }
}
