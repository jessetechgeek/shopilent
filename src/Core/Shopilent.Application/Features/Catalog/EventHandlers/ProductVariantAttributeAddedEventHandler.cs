using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantAttributeAddedEventHandler : INotificationHandler<
    DomainEventNotification<ProductVariantAttributeAddedEvent>>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICacheService _cacheService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductVariantAttributeAddedEventHandler> _logger;

    public ProductVariantAttributeAddedEventHandler(
        IProductReadRepository productReadRepository,
        ICacheService cacheService,
        ISearchService searchService,
        ILogger<ProductVariantAttributeAddedEventHandler> logger)
    {
        _productReadRepository = productReadRepository;
        _cacheService = cacheService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductVariantAttributeAddedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation(
            "Product variant attribute added. ProductId: {ProductId}, VariantId: {VariantId}, AttributeId: {AttributeId}",
            domainEvent.ProductId,
            domainEvent.VariantId,
            domainEvent.AttributeId);

        try
        {
            // Invalidate caches
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

            await _cacheService.RemoveAsync($"variant-{domainEvent.VariantId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync($"product-variants-{domainEvent.ProductId}", cancellationToken);
            await _cacheService.RemoveAsync($"attribute-{domainEvent.AttributeId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("products-*", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ProductVariantAttributeAddedEvent for ProductId: {ProductId}, VariantId: {VariantId}, AttributeId: {AttributeId}",
                domainEvent.ProductId,
                domainEvent.VariantId,
                domainEvent.AttributeId);
        }
    }
}
