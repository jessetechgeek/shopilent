using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetProduct.V1;

internal sealed class GetProductQueryHandlerV1 : IQueryHandler<GetProductQueryV1, ProductDetailDto>
{
    private readonly IProductReadRepository _productReadRepository;
    private readonly ILogger<GetProductQueryHandlerV1> _logger;
    private readonly IS3StorageService _s3StorageService;

    public GetProductQueryHandlerV1(
        IProductReadRepository productReadRepository,
        ILogger<GetProductQueryHandlerV1> logger,
        IS3StorageService s3StorageService)
    {
        _productReadRepository = productReadRepository;
        _logger = logger;
        _s3StorageService = s3StorageService;
    }

    public async Task<Result<ProductDetailDto>> Handle(GetProductQueryV1 request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productReadRepository.GetDetailByIdAsync(request.Id, cancellationToken);

            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} was not found", request.Id);
                return Result.Failure<ProductDetailDto>(ProductErrors.NotFound(request.Id));
            }

            // Transform product images to presigned URLs
            var transformedProduct = await TransformProductImagesAsync(product, cancellationToken);

            _logger.LogInformation("Retrieved product with ID {ProductId}", request.Id);
            return Result.Success(transformedProduct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product with ID {ProductId}", request.Id);

            return Result.Failure<ProductDetailDto>(
                Error.Failure(
                    code: "Product.GetFailed",
                    message: $"Failed to retrieve product: {ex.Message}"));
        }
    }

    private async Task<ProductDetailDto> TransformProductImagesAsync(
        ProductDetailDto product,
        CancellationToken cancellationToken)
    {
        // Transform product images
        var transformedImages = new List<ProductImageDto>();
        foreach (var image in product.Images)
        {
            var transformedImage = await TransformImageAsync(image, cancellationToken);
            transformedImages.Add(transformedImage);
        }

        // Transform variant images
        var transformedVariants = new List<ProductVariantDto>();
        foreach (var variant in product.Variants)
        {
            var transformedVariantImages = new List<ProductImageDto>();
            foreach (var image in variant.Images)
            {
                var transformedImage = await TransformImageAsync(image, cancellationToken);
                transformedVariantImages.Add(transformedImage);
            }

            transformedVariants.Add(new ProductVariantDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                Sku = variant.Sku,
                Price = variant.Price,
                Currency = variant.Currency,
                StockQuantity = variant.StockQuantity,
                IsActive = variant.IsActive,
                Metadata = variant.Metadata,
                Attributes = variant.Attributes,
                Images = transformedVariantImages,
                CreatedAt = variant.CreatedAt,
                UpdatedAt = variant.UpdatedAt
            });
        }

        return new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            BasePrice = product.BasePrice,
            Currency = product.Currency,
            Sku = product.Sku,
            Slug = product.Slug,
            IsActive = product.IsActive,
            Metadata = product.Metadata,
            Images = transformedImages,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Categories = product.Categories,
            Attributes = product.Attributes,
            Variants = transformedVariants,
            CreatedBy = product.CreatedBy,
            ModifiedBy = product.ModifiedBy,
            LastModified = product.LastModified
        };
    }

    private async Task<ProductImageDto> TransformImageAsync(
        ProductImageDto image,
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

        return new ProductImageDto
        {
            ImageKey = null,
            ThumbnailKey = null,
            ImageUrl = imageUrl,
            ThumbnailUrl = thumbnailUrl,
            AltText = image.AltText,
            IsDefault = image.IsDefault,
            DisplayOrder = image.DisplayOrder
        };
    }
}
