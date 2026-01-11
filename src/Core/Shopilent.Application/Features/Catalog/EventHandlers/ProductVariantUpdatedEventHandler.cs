using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantUpdatedEventHandler : INotificationHandler<DomainEventNotification<ProductVariantUpdatedEvent>>
{
    private readonly ILogger<ProductVariantUpdatedEventHandler> _logger;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly IProductReadRepository _productReadRepository;

    public ProductVariantUpdatedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductVariantUpdatedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductVariantUpdatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Product variant updated. ProductId: {ProductId}, VariantId: {VariantId}",
            domainEvent.ProductId,
            domainEvent.VariantId);

        try
        {
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

            // Invalidate variant-specific caches
            await _cacheService.RemoveAsync($"variant-{domainEvent.VariantId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync($"product-variants-{domainEvent.ProductId}", cancellationToken);

            // Also invalidate product listings that might include this product's variants
            await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ProductVariantUpdatedEvent for ProductId: {ProductId}, VariantId: {VariantId}",
                domainEvent.ProductId, domainEvent.VariantId);
        }
    }
}
