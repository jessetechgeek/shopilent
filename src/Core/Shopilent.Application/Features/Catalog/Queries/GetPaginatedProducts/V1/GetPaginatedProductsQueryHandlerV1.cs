using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetPaginatedProducts.V1;

internal sealed class GetPaginatedProductsQueryHandlerV1 :
    IQueryHandler<GetPaginatedProductsQueryV1, SearchResponse<ProductSearchResponseDto>>
{
    private readonly ISearchService _searchService;
    private readonly ILogger<GetPaginatedProductsQueryHandlerV1> _logger;
    private readonly IS3StorageService _s3StorageService;

    public GetPaginatedProductsQueryHandlerV1(
        ISearchService searchService,
        ILogger<GetPaginatedProductsQueryHandlerV1> logger,
        IS3StorageService s3StorageService)
    {
        _searchService = searchService;
        _logger = logger;
        _s3StorageService = s3StorageService;
    }

    public async Task<Result<SearchResponse<ProductSearchResponseDto>>> Handle(
        GetPaginatedProductsQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var searchRequest = new SearchRequest
            {
                Query = request.SearchQuery ?? "",
                CategorySlugs = request.CategorySlugs,
                AttributeFilters = request.AttributeFilters,
                PriceMin = request.PriceMin,
                PriceMax = request.PriceMax,
                InStockOnly = request.InStockOnly,
                ActiveOnly = request.IsActiveOnly,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                SortBy = MapSortColumn(request.SortColumn),
                SortDescending = request.SortDescending
            };

            var searchResult = await _searchService.SearchProductsAsync(searchRequest, cancellationToken);

            if (searchResult.IsFailure)
            {
                _logger.LogError("Product search failed: {Error}", searchResult.Error.Message);
                return Result.Failure<SearchResponse<ProductSearchResponseDto>>(searchResult.Error);
            }

            // Transform image keys to presigned URLs
            var transformedItems = new List<ProductSearchResponseDto>();

            foreach (var product in searchResult.Value.Items)
            {
                var transformedProduct = await TransformProductImagesAsync(product, cancellationToken);
                transformedItems.Add(transformedProduct);
            }

            var transformedResponse = new SearchResponse<ProductSearchResponseDto>
            {
                Items = transformedItems.ToArray(),
                TotalCount = searchResult.Value.TotalCount,
                PageNumber = searchResult.Value.PageNumber,
                PageSize = searchResult.Value.PageSize,
                TotalPages = searchResult.Value.TotalPages,
                HasNextPage = searchResult.Value.HasNextPage,
                HasPreviousPage = searchResult.Value.HasPreviousPage,
                Facets = searchResult.Value.Facets,
                Query = searchResult.Value.Query
            };

            _logger.LogInformation(
                "Retrieved products via search: Page {PageNumber}, Size {PageSize}, Total {TotalCount}",
                request.PageNumber, request.PageSize, transformedResponse.TotalCount);

            return Result.Success(transformedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated products");

            return Result.Failure<SearchResponse<ProductSearchResponseDto>>(
                Error.Failure(
                    code: "Products.GetPaginatedFailed",
                    message: $"Failed to retrieve paginated products: {ex.Message}"));
        }
    }


    private static string MapSortColumn(string userFriendlyColumn)
    {
        return userFriendlyColumn switch
        {
            "Name" => "name",
            "BasePrice" => "price",
            "CreatedAt" => "created",
            "UpdatedAt" => "updated",
            _ => "name"
        };
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
