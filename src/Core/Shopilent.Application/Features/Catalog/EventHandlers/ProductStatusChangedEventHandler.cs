using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductStatusChangedEventHandler : INotificationHandler<DomainEventNotification<ProductStatusChangedEvent>>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductStatusChangedEventHandler> _logger;

    public ProductStatusChangedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductStatusChangedEventHandler> logger
    )
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductStatusChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Product status changed for ID: {ProductId}. New status: {IsActive}",
            domainEvent.ProductId,
            domainEvent.IsActive ? "Active" : "Inactive");

        // Invalidate specific product cache
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

        // Also invalidate any collection caches
        await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);
    }
}
