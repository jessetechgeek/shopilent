using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantCreatedEventHandler : INotificationHandler<DomainEventNotification<ProductVariantCreatedEvent>>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductVariantCreatedEventHandler> _logger;

    public ProductVariantCreatedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductVariantCreatedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductVariantCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Product variant created. ProductId: {ProductId}, VariantId: {VariantId}",
            domainEvent.ProductId, domainEvent.VariantId);

        // Invalidate product cache since variants are part of product detail
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

        // Invalidate any variant-specific caches if they exist
        await _cacheService.RemoveByPatternAsync($"variant-{domainEvent.VariantId}", cancellationToken);
        await _cacheService.RemoveByPatternAsync($"product-variants-{domainEvent.ProductId}", cancellationToken);
    }
}
