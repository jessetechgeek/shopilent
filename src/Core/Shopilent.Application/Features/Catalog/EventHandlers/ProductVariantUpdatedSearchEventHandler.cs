using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductVariantUpdatedSearchEventHandler : INotificationHandler<DomainEventNotification<ProductVariantUpdatedEvent>>
{
    private readonly IProductReadRepository _productReader;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductVariantUpdatedSearchEventHandler> _logger;

    public ProductVariantUpdatedSearchEventHandler(
        IProductReadRepository productReader,
        ISearchService searchService,
        ILogger<ProductVariantUpdatedSearchEventHandler> logger)
    {
        _productReader = productReader;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductVariantUpdatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        try
        {
            var productDto = await _productReader.GetDetailByIdAsync(domainEvent.ProductId, cancellationToken);
            if (productDto is null)
            {
                _logger.LogWarning("Product {ProductId} not found for search re-indexing after variant update",
                    domainEvent.ProductId);
                return;
            }

            var searchDocument = ProductSearchDocument.FromProductDto(productDto);
            var result = await _searchService.IndexProductAsync(searchDocument, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to re-index product {ProductId} in search after variant update: {Error}",
                    domainEvent.ProductId, result.Error.Message);
            }
            else
            {
                _logger.LogDebug(
                    "Successfully re-indexed product {ProductId} in search after variant {VariantId} update",
                    domainEvent.ProductId, domainEvent.VariantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-indexing product {ProductId} in search after variant update",
                domainEvent.ProductId);
        }
    }
}
