using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantAddedEventHandler : INotificationHandler<DomainEventNotification<ProductVariantAddedEvent>>
{
    private readonly IProductReadRepository _productReader;
    private readonly ILogger<ProductVariantAddedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly IOutboxService _outboxService;

    public ProductVariantAddedEventHandler(
        IProductReadRepository productReader,
        ILogger<ProductVariantAddedEventHandler> logger,
        ICacheService cacheService,
        IOutboxService outboxService)
    {
        _productReader = productReader;
        _logger = logger;
        _cacheService = cacheService;
        _outboxService = outboxService;
    }

    public async Task Handle(DomainEventNotification<ProductVariantAddedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Product variant added. ProductId: {ProductId}, VariantId: {VariantId}",
            domainEvent.ProductId,
            domainEvent.VariantId);

        try
        {
            // Invalidate product cache since variants are part of product detail
            await _cacheService.RemoveAsync($"product-{domainEvent.ProductId}", cancellationToken);

            // Invalidate any product variants collection cache
            await _cacheService.RemoveByPatternAsync($"product-variants-{domainEvent.ProductId}", cancellationToken);

            // Invalidate product listings that might be affected by the new variant
            await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);

            // Get the product to check if it has other variants
            var product = await _productReader.GetDetailByIdAsync(domainEvent.ProductId, cancellationToken);

            if (product != null)
            {
                // If this is the first variant, it might affect product filtering/display in category pages
                if (product.Variants?.Count == 1)
                {
                    await _cacheService.RemoveByPatternAsync("category-products-*", cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ProductVariantAddedEvent for ProductId: {ProductId}, VariantId: {VariantId}",
                domainEvent.ProductId, domainEvent.VariantId);
        }
    }
}
