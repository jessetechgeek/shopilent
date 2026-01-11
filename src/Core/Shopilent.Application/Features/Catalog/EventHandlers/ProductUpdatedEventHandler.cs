using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class ProductUpdatedEventHandler : INotificationHandler<DomainEventNotification<ProductUpdatedEvent>>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductUpdatedEventHandler> _logger;

    public ProductUpdatedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductUpdatedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductUpdatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Product updated with ID: {ProductId}", domainEvent.ProductId);

        // Invalidate specific cache by ID
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

        // Also invalidate any collections that might contain this product
        await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);
    }
}
