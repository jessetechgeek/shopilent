using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Administration.Commands.RebuildSearchIndex.V1;

internal sealed class
    RebuildSearchIndexCommandHandlerV1 : ICommandHandler<RebuildSearchIndexCommandV1, RebuildSearchIndexResponseV1>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ISearchService _searchService;
    private readonly ILogger<RebuildSearchIndexCommandHandlerV1> _logger;

    public RebuildSearchIndexCommandHandlerV1(
        IProductReadRepository productReadRepository,
        ISearchService searchService,
        ILogger<RebuildSearchIndexCommandHandlerV1> logger)
    {
        _productReadRepository = productReadRepository;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<Result<RebuildSearchIndexResponseV1>> Handle(
        RebuildSearchIndexCommandV1 request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new RebuildSearchIndexResponseV1 { CompletedAt = DateTime.UtcNow };

        try
        {
            _logger.LogInformation(
                "Starting search index rebuild - Initialize: {Initialize}, Index: {Index}, Force: {Force}",
                request.InitializeIndexes, request.IndexProducts, request.ForceReindex);

            if (request.InitializeIndexes)
            {
                _logger.LogInformation("Initializing search indexes...");
                await _searchService.InitializeIndexesAsync(cancellationToken);
                response.IndexesInitialized = true;
                _logger.LogInformation("Search indexes initialized successfully");
            }

            if (request.IndexProducts)
            {
                _logger.LogInformation("Starting product indexing...");

                var productDtos = await _productReadRepository.ListAllAsync(cancellationToken);
                var productDtoList = productDtos.ToList();
                var indexedIds = new HashSet<Guid>();

                if (!productDtoList.Any())
                {
                    _logger.LogInformation("No products found to index");
                    response.ProductsIndexed = 0;
                }
                else
                {
                    var searchDocuments = new List<ProductSearchDocument>();
                    foreach (var productDto in productDtoList)
                    {
                        var product = await _productReadRepository.GetDetailByIdAsync(productDto.Id, cancellationToken);
                        if (product != null)
                        {
                            searchDocuments.Add(ProductSearchDocument.FromProductDto(product));
                        }
                    }

                    var indexResult = await _searchService.IndexProductsAsync(searchDocuments, cancellationToken);

                    if (indexResult.IsFailure)
                    {
                        _logger.LogError("Failed to index products: {Error}", indexResult.Error.Message);
                        stopwatch.Stop();

                        response.IsSuccess = false;
                        response.Message =
                            $"Search index rebuild partially completed. Index initialization: {(response.IndexesInitialized ? "Success" : "Skipped")}. Product indexing failed: {indexResult.Error.Message}";
                        response.Duration = stopwatch.Elapsed;

                        return Result.Failure<RebuildSearchIndexResponseV1>(indexResult.Error);
                    }

                    response.ProductsIndexed = searchDocuments.Count;
                    indexedIds = searchDocuments.Select(d => d.Id).ToHashSet();
                    _logger.LogInformation("Successfully indexed {Count} products", searchDocuments.Count);
                }

                _logger.LogInformation("Checking for orphaned products in search index...");

                var allMeilisearchIdsResult = await _searchService.GetAllProductIdsAsync(cancellationToken);
                if (allMeilisearchIdsResult.IsSuccess)
                {
                    var orphanedIds = allMeilisearchIdsResult.Value.Where(id => !indexedIds.Contains(id)).ToList();

                    if (orphanedIds.Any())
                    {
                        _logger.LogInformation("Found {Count} orphaned products to delete", orphanedIds.Count);
                        var deleteResult =
                            await _searchService.DeleteProductsByIdsAsync(orphanedIds, cancellationToken);

                        if (deleteResult.IsSuccess)
                        {
                            response.ProductsDeleted = orphanedIds.Count;
                            _logger.LogInformation("Successfully deleted {Count} orphaned products", orphanedIds.Count);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to delete orphaned products: {Error}",
                                deleteResult.Error.Message);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No orphaned products found");
                        response.ProductsDeleted = 0;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to fetch product IDs from search index for cleanup: {Error}",
                        allMeilisearchIdsResult.Error.Message);
                }
            }

            stopwatch.Stop();

            var messageParts = new List<string>();
            if (request.InitializeIndexes)
                messageParts.Add("indexes initialized");
            if (request.IndexProducts)
                messageParts.Add($"{response.ProductsIndexed} products indexed");
            if (response.ProductsDeleted > 0)
                messageParts.Add($"{response.ProductsDeleted} orphaned products deleted");

            response.IsSuccess = true;
            response.Message = $"Search index rebuild completed successfully: {string.Join(", ", messageParts)}";
            response.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Search index rebuild completed successfully in {Duration}ms - Indexes: {Indexes}, Products: {Products}, Deleted: {Deleted}",
                stopwatch.ElapsedMilliseconds, response.IndexesInitialized, response.ProductsIndexed,
                response.ProductsDeleted);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during search index rebuild");

            response.IsSuccess = false;
            response.Message = $"Search index rebuild failed: {ex.Message}";
            response.Duration = stopwatch.Elapsed;

            return Result.Failure<RebuildSearchIndexResponseV1>(
                Error.Failure("Search.RebuildFailed", response.Message));
        }
    }
}
