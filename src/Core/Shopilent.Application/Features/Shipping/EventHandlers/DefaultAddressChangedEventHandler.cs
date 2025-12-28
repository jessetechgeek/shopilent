using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Events;
using Shopilent.Domain.Shipping.Repositories.Read;

namespace Shopilent.Application.Features.Shipping.EventHandlers;

internal sealed class
    DefaultAddressChangedEventHandler : INotificationHandler<DomainEventNotification<DefaultAddressChangedEvent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAddressReadRepository _addressReadRepository;
    private readonly ILogger<DefaultAddressChangedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;
    private readonly IEmailService _emailService;

    public DefaultAddressChangedEventHandler(
        IUnitOfWork unitOfWork,
        IAddressReadRepository addressReadRepository,
        ILogger<DefaultAddressChangedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
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
            "Default address changed. AddressId: {AddressId}, UserId: {UserId}, AddressType: {AddressType}",
            domainEvent.AddressId,
            domainEvent.UserId,
            domainEvent.AddressType);

        try
        {
            // Clear default address caches
            await _cacheService.RemoveByPatternAsync($"default-address-{domainEvent.AddressType}-{domainEvent.UserId}",
                cancellationToken);

            // If the address type is 'Both', clear both shipping and billing default address caches
            if (domainEvent.AddressType == AddressType.Both)
            {
                await _cacheService.RemoveByPatternAsync($"default-address-{AddressType.Shipping}-{domainEvent.UserId}",
                    cancellationToken);
                await _cacheService.RemoveByPatternAsync($"default-address-{AddressType.Billing}-{domainEvent.UserId}",
                    cancellationToken);
            }

            // Clear all user addresses cache as the default flag has changed
            await _cacheService.RemoveByPatternAsync($"user-addresses-{domainEvent.UserId}",
                cancellationToken);

            // Get user details
            var user = await _unitOfWork.UserReader.GetByIdAsync(domainEvent.UserId, cancellationToken);

            if (user != null)
            {
                // Get the address details
                var address = await _addressReadRepository.GetByIdAsync(domainEvent.AddressId, cancellationToken);

                if (address != null)
                {
                    // Determine the address type description for the email
                    string addressTypeDescription = domainEvent.AddressType switch
                    {
                        AddressType.Shipping => "shipping",
                        AddressType.Billing => "billing",
                        AddressType.Both => "shipping and billing",
                        _ => "default"
                    };

                    // Send notification email
                    string subject = $"Default {addressTypeDescription} Address Updated";
                    string message = $"Your default {addressTypeDescription} address has been updated. " +
                                     $"The new address will be used for future orders.";

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
