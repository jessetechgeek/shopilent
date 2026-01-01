using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Application.Abstractions.Search;

using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Search.Queries.UniversalSearch.V1;

internal sealed class UniversalSearchQueryHandlerV1 : IQueryHandler<UniversalSearchQueryV1, SearchResponse<ProductSearchResponseDto>>
{
    private readonly ISearchService _searchService;
    private readonly ILogger<UniversalSearchQueryHandlerV1> _logger;
    private readonly IS3StorageService _s3StorageService;

    public UniversalSearchQueryHandlerV1(
        ISearchService searchService,
        ILogger<UniversalSearchQueryHandlerV1> logger,
        IS3StorageService s3StorageService)
    {
        _searchService = searchService;
        _logger = logger;
        _s3StorageService = s3StorageService;
    }

    public async Task<Result<SearchResponse<ProductSearchResponseDto>>> Handle(
        UniversalSearchQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Executing universal search with query: {Query}", request.Query);

            var searchRequest = new SearchRequest
            {
                Query = request.Query,
                CategorySlugs = request.CategorySlugs,
                AttributeFilters = request.AttributeFilters,
                PriceMin = request.PriceMin,
                PriceMax = request.PriceMax,
                InStockOnly = request.InStockOnly,
                ActiveOnly = request.ActiveOnly,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending
            };

            var result = await _searchService.SearchProductsAsync(searchRequest, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Universal search failed: {Error}", result.Error.Message);
                return Result.Failure<SearchResponse<ProductSearchResponseDto>>(result.Error);
            }

            // Transform image keys to presigned URLs
            var transformedItems = new List<ProductSearchResponseDto>();

            foreach (var product in result.Value.Items)
            {
                var transformedProduct = await TransformProductImagesAsync(product, cancellationToken);
                transformedItems.Add(transformedProduct);
            }

            var transformedResponse = new SearchResponse<ProductSearchResponseDto>
            {
                Items = transformedItems.ToArray(),
                TotalCount = result.Value.TotalCount,
                PageNumber = result.Value.PageNumber,
                PageSize = result.Value.PageSize,
                TotalPages = result.Value.TotalPages,
                HasNextPage = result.Value.HasNextPage,
                HasPreviousPage = result.Value.HasPreviousPage,
                Facets = result.Value.Facets,
                Query = result.Value.Query
            };

            _logger.LogDebug("Universal search completed successfully. Found {Count} results",
                transformedResponse.TotalCount);

            return Result.Success(transformedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during universal search");
            return Result.Failure<SearchResponse<ProductSearchResponseDto>>(
                Domain.Common.Errors.Error.Failure("Search.UnexpectedError", "An unexpected error occurred during search"));
        }
    }

    private async Task<ProductSearchResponseDto> TransformProductImagesAsync(
        ProductSearchResultDto product,
        CancellationToken cancellationToken)
    {
        // Transform product images
        var transformedImages = new List<ProductSearchImageResponseDto>();
        foreach (var image in product.Images)
        {
            var transformedImage = await TransformImageAsync(image, cancellationToken);
            transformedImages.Add(transformedImage);
        }

        // Transform variant images
        var transformedVariants = new List<ProductSearchVariantResponseDto>();
        foreach (var variant in product.Variants)
        {
            var transformedVariantImages = new List<ProductSearchImageResponseDto>();
            foreach (var image in variant.Images)
            {
                var transformedImage = await TransformImageAsync(image, cancellationToken);
                transformedVariantImages.Add(transformedImage);
            }

            transformedVariants.Add(new ProductSearchVariantResponseDto
            {
                Id = variant.Id,
                SKU = variant.SKU,
                Price = variant.Price,
                Stock = variant.Stock,
                IsActive = variant.IsActive,
                Attributes = variant.Attributes,
                Images = transformedVariantImages.ToArray()
            });
        }

        return new ProductSearchResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            SKU = product.SKU,
            Slug = product.Slug,
            BasePrice = product.BasePrice,
            IsActive = product.IsActive,
            Status = product.Status,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            HasStock = product.HasStock,
            TotalStock = product.TotalStock,
            Categories = product.Categories,
            Attributes = product.Attributes,
            Variants = transformedVariants.ToArray(),
            Images = transformedImages.ToArray()
        };
    }

    private async Task<ProductSearchImageResponseDto> TransformImageAsync(
        ProductSearchImage image,
        CancellationToken cancellationToken)
    {
        string imageUrl = "";
        string thumbnailUrl = "";

        if (!string.IsNullOrEmpty(image.ImageKey))
        {
            var imageUrlResult = await _s3StorageService.GetPresignedUrlAsync(
                image.ImageKey,
                TimeSpan.FromHours(24),
                cancellationToken);

            if (imageUrlResult.IsSuccess)
                imageUrl = imageUrlResult.Value;
        }

        if (!string.IsNullOrEmpty(image.ThumbnailKey))
        {
            var thumbnailUrlResult = await _s3StorageService.GetPresignedUrlAsync(
                image.ThumbnailKey,
                TimeSpan.FromHours(24),
                cancellationToken);

            if (thumbnailUrlResult.IsSuccess)
                thumbnailUrl = thumbnailUrlResult.Value;
        }

        return new ProductSearchImageResponseDto
        {
            ImageUrl = imageUrl,
            ThumbnailUrl = thumbnailUrl,
            AltText = image.AltText,
            IsDefault = image.IsDefault,
            Order = image.Order
        };
    }
}