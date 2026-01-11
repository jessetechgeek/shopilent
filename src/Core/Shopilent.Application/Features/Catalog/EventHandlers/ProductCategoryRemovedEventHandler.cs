using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductCategoryRemovedEventHandler : INotificationHandler<DomainEventNotification<ProductCategoryRemovedEvent>>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductCategoryRemovedEventHandler> _logger;

    public ProductCategoryRemovedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductCategoryRemovedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductCategoryRemovedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation("Product category removed. ProductId: {ProductId}, CategoryId: {CategoryId}",
            domainEvent.ProductId,
            domainEvent.CategoryId);

        try
        {
            // Invalidate product cache
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

            // Invalidate category product lists
            await _cacheService.RemoveByPatternAsync($"category-products-{domainEvent.CategoryId}", cancellationToken);

            // Invalidate product collections that might include this product
            await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ProductCategoryRemovedEvent for ProductId: {ProductId}, CategoryId: {CategoryId}",
                domainEvent.ProductId, domainEvent.CategoryId);
        }
    }
}
