using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantAddedEventHandler : INotificationHandler<DomainEventNotification<ProductVariantAddedEvent>>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductVariantAddedEventHandler> _logger;

    public ProductVariantAddedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductVariantAddedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
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

            // Get the product to retrieve slug and check variant count (single DB call)
            var product = await _productReadRepository.GetDetailByIdAsync(domainEvent.ProductId, cancellationToken);

            if (product != null)
            {
                // Invalidate slug-based cache
                await _cacheService.RemoveAsync($"product-slug-{product.Slug}", cancellationToken);
                _logger.LogInformation("Invalidated slug-based cache for product slug: {ProductSlug}", product.Slug);

                // If this is the first variant, it might affect product filtering/display in category pages
                if (product.Variants?.Count == 1)
                {
                    await _cacheService.RemoveByPatternAsync("category-products-*", cancellationToken);
                }

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

            // Invalidate any product variants collection cache
            await _cacheService.RemoveByPatternAsync($"product-variants-{domainEvent.ProductId}", cancellationToken);

            // Invalidate product listings that might be affected by the new variant
            await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ProductVariantAddedEvent for ProductId: {ProductId}, VariantId: {VariantId}",
                domainEvent.ProductId, domainEvent.VariantId);
        }
    }
}
