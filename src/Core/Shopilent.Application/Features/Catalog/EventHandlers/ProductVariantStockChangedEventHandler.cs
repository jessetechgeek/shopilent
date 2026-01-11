using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantStockChangedEventHandler : INotificationHandler<
    DomainEventNotification<ProductVariantStockChangedEvent>>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductVariantStockChangedEventHandler> _logger;

    public ProductVariantStockChangedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductVariantStockChangedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductVariantStockChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation(
            "Product variant stock changed. ProductId: {ProductId}, VariantId: {VariantId}, Old Quantity: {OldQuantity}, New Quantity: {NewQuantity}",
            domainEvent.ProductId,
            domainEvent.VariantId,
            domainEvent.OldQuantity,
            domainEvent.NewQuantity);

        // Invalidate product cache since stock levels affect availability
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
        await _cacheService.RemoveByPatternAsync($"variant-{domainEvent.VariantId}", cancellationToken);
        await _cacheService.RemoveByPatternAsync($"product-variants-{domainEvent.ProductId}", cancellationToken);

        // Invalidate product listings that might show stock status
        await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);

        // Check for low stock conditions
        bool isLowStock = domainEvent.NewQuantity > 0 && domainEvent.NewQuantity <= 5;
        bool isOutOfStock = domainEvent.NewQuantity == 0;
        bool isBackInStock = domainEvent.OldQuantity == 0 && domainEvent.NewQuantity > 0;
    }
}
