using MediatR;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.Repositories.Read;

namespace Shopilent.Application.Features.Catalog.EventHandlers;

internal sealed class
    ProductCreatedSearchEventHandler : INotificationHandler<DomainEventNotification<ProductCreatedEvent>>
{
    private readonly IProductReadRepository _productReader;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductCreatedSearchEventHandler> _logger;

    public ProductCreatedSearchEventHandler(
        IProductReadRepository productReader,
        ISearchService searchService,
        ILogger<ProductCreatedSearchEventHandler> logger)
    {
        _productReader = productReader;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        try
        {
            var productDto = await _productReader.GetDetailByIdAsync(domainEvent.ProductId, cancellationToken);
            if (productDto is null)
            {
                _logger.LogWarning("Product {ProductId} not found for search indexing", domainEvent.ProductId);
                return;
            }

            var searchDocument = ProductSearchDocument.FromProductDto(productDto);
            var result = await _searchService.IndexProductAsync(searchDocument, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to index product {ProductId} in search: {Error}",
                    domainEvent.ProductId, result.Error.Message);
            }
            else
            {
                _logger.LogDebug("Successfully indexed product {ProductId} in search", domainEvent.ProductId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing product {ProductId} in search", domainEvent.ProductId);
        }
    }
}
