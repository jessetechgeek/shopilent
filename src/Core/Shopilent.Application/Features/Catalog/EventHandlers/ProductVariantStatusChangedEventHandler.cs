using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantStatusChangedEventHandler : INotificationHandler<
    DomainEventNotification<ProductVariantStatusChangedEvent>>
{
    private readonly ILogger<ProductVariantStatusChangedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly IProductReadRepository _productReadRepository;

    public ProductVariantStatusChangedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductVariantStatusChangedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductVariantStatusChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation(
            "Product variant status changed. ProductId: {ProductId}, VariantId: {VariantId}, IsActive: {IsActive}",
            domainEvent.ProductId,
            domainEvent.VariantId,
            domainEvent.IsActive);

        // Invalidate product and variant caches
        await _cacheService.RemoveAsync($"product-{domainEvent.ProductId}", cancellationToken);

        // Get product to retrieve slug for slug-based cache invalidation
        var product = await _productReadRepository.GetDetailByIdAsync(domainEvent.ProductId, cancellationToken);
        if (product != null)
        {
            await _cacheService.RemoveAsync($"product-slug-{product.Slug}", cancellationToken);
            _logger.LogInformation("Invalidated slug-based cache for product slug: {ProductSlug}", product.Slug);

            var searchDocument = ProductSearchDocument.FromProductDto(product);
            var result = await _searchService.IndexProductAsync(searchDocument, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to re-index product {ProductId}: {ErrorMessage}",
                    domainEvent.ProductId, result.Error.Message);
            }
            else
            {
                _logger.LogDebug("Successfully re-indexed product {ProductId}", domainEvent.ProductId);
            }
        }

        await _cacheService.RemoveByPatternAsync($"variant-{domainEvent.VariantId}", cancellationToken);
        await _cacheService.RemoveByPatternAsync($"product-variants-{domainEvent.ProductId}", cancellationToken);

        // Invalidate product listings
        await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);
    }
}
