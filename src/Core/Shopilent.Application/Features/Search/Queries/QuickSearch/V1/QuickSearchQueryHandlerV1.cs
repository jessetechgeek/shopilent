using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Search.Queries.QuickSearch.V1;

internal sealed class QuickSearchQueryHandlerV1
    : IQueryHandler<QuickSearchQueryV1, QuickSearchResponseV1>
{
    private readonly ISearchService _searchService;
    private readonly ILogger<QuickSearchQueryHandlerV1> _logger;
    private readonly IS3StorageService _s3StorageService;

    public QuickSearchQueryHandlerV1(
        ISearchService searchService,
        ILogger<QuickSearchQueryHandlerV1> logger,
        IS3StorageService s3StorageService)
    {
        _searchService = searchService;
        _logger = logger;
        _s3StorageService = s3StorageService;
    }

    public async Task<Result<QuickSearchResponseV1>> Handle(
        QuickSearchQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "Executing quick search with query: '{Query}', limit: {Limit}",
                request.Query,
                request.Limit);

            var searchRequest = new SearchRequest
            {
                Query = request.Query,
                ActiveOnly = true,
                InStockOnly = false,
                PageNumber = 1,
                PageSize = request.Limit,
                SortBy = "relevance",
                SortDescending = false,
                CategorySlugs = [],
                AttributeFilters = new Dictionary<string, string[]>(),
                PriceMin = null,
                PriceMax = null
            };

            var searchResult = await _searchService.SearchProductsAsync(
                searchRequest,
                cancellationToken);

            if (searchResult.IsFailure)
            {
                _logger.LogError(
                    "Quick search failed: {Error}",
                    searchResult.Error.Message);
                return Result.Failure<QuickSearchResponseV1>(searchResult.Error);
            }

            var suggestions = new List<ProductSuggestionDto>();

            foreach (var item in searchResult.Value.Items)
            {
                var image = ExtractProductImage(item);
                string imageUrl = "";
                string thumbnailUrl = "";

                if (image != null)
                {
                    if (!string.IsNullOrEmpty(image.ImageKey))
                    {
                        var imageUrlResult = await _s3StorageService.GetPublicUrlAsync(
                            image.ImageKey,
                            cancellationToken);

                        if (imageUrlResult.IsSuccess)
                            imageUrl = imageUrlResult.Value;
                    }

                    if (!string.IsNullOrEmpty(image.ThumbnailKey))
                    {
                        var thumbnailUrlResult = await _s3StorageService.GetPublicUrlAsync(
                            image.ThumbnailKey,
                            cancellationToken);

                        if (thumbnailUrlResult.IsSuccess)
                            thumbnailUrl = thumbnailUrlResult.Value;
                    }
                }

                suggestions.Add(new ProductSuggestionDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Slug = item.Slug,
                    ImageUrl = imageUrl,
                    ThumbnailUrl = thumbnailUrl,
                    BasePrice = item.BasePrice
                });
            }

            var response = new QuickSearchResponseV1
            {
                Suggestions = suggestions.ToArray(),
                TotalCount = searchResult.Value.TotalCount,
                Query = request.Query
            };

            _logger.LogDebug(
                "Quick search completed successfully. Found {Count} results",
                response.TotalCount);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during quick search");
            return Result.Failure<QuickSearchResponseV1>(
                Error.Failure(
                    "Search.QuickSearchFailed",
                    "An unexpected error occurred during quick search"));
        }
    }

    private static ProductSearchImage? ExtractProductImage(ProductSearchResultDto product)
    {
        var defaultImage = product.Images.FirstOrDefault(img => img.IsDefault);
        if (defaultImage != null)
            return defaultImage;

        var firstImage = product.Images
            .OrderBy(img => img.Order)
            .FirstOrDefault();
        if (firstImage != null)
            return firstImage;

        var defaultVariantImage = product.Variants
            .Where(v => v.IsActive)
            .SelectMany(v => v.Images)
            .FirstOrDefault(img => img.IsDefault);
        if (defaultVariantImage != null)
            return defaultVariantImage;

        var firstVariantImage = product.Variants
            .Where(v => v.IsActive)
            .SelectMany(v => v.Images)
            .OrderBy(img => img.Order)
            .FirstOrDefault();
        if (firstVariantImage != null)
            return firstVariantImage;

        return null;
    }
}
