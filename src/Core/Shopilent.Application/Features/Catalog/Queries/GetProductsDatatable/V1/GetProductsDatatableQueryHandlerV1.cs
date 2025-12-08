using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetProductsDatatable.V1;

internal sealed class GetProductsDatatableQueryHandlerV1 :
    IQueryHandler<GetProductsDatatableQueryV1, DataTableResult<ProductDatatableDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetProductsDatatableQueryHandlerV1> _logger;
    private readonly IS3StorageService _s3StorageService;
    private const string DefaultBucket = "shopilent";

    public GetProductsDatatableQueryHandlerV1(
        IUnitOfWork unitOfWork,
        ILogger<GetProductsDatatableQueryHandlerV1> logger,
        IS3StorageService s3StorageService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _s3StorageService = s3StorageService;
    }

    public async Task<Result<DataTableResult<ProductDatatableDto>>> Handle(
        GetProductsDatatableQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get detailed datatable results from repository
            var result = await _unitOfWork.ProductReader.GetProductDetailDataTableAsync(
                request.Request,
                cancellationToken);

            // Map the product details to the DTO and generate presigned URLs for images
            var dtoItems = new List<ProductDatatableDto>();

            foreach (var productDetail in result.Data)
            {
                var imagesWithUrls = new List<ProductImageDto>();

                if (productDetail.Images != null && productDetail.Images.Any())
                {
                    foreach (var image in productDetail.Images)
                    {
                        var imageDto = new ProductImageDto
                        {
                            ImageKey = image.ImageKey,
                            ThumbnailKey = image.ThumbnailKey,
                            AltText = image.AltText,
                            IsDefault = image.IsDefault,
                            DisplayOrder = image.DisplayOrder,
                            ImageUrl = string.Empty,
                            ThumbnailUrl = string.Empty
                        };

                        // Generate presigned URLs for image and thumbnail
                        if (!string.IsNullOrEmpty(image.ImageKey))
                        {
                            var imageUrlResult = await _s3StorageService.GetPresignedUrlAsync(
                                DefaultBucket,
                                image.ImageKey,
                                TimeSpan.FromHours(24),
                                cancellationToken);

                            if (imageUrlResult.IsSuccess)
                            {
                                imageDto.ImageUrl = imageUrlResult.Value;
                            }
                        }

                        if (!string.IsNullOrEmpty(image.ThumbnailKey))
                        {
                            var thumbnailUrlResult = await _s3StorageService.GetPresignedUrlAsync(
                                DefaultBucket,
                                image.ThumbnailKey,
                                TimeSpan.FromHours(24),
                                cancellationToken);

                            if (thumbnailUrlResult.IsSuccess)
                            {
                                imageDto.ThumbnailUrl = thumbnailUrlResult.Value;
                            }
                        }

                        imagesWithUrls.Add(imageDto);
                    }
                }

                dtoItems.Add(new ProductDatatableDto
                {
                    Id = productDetail.Id,
                    Name = productDetail.Name,
                    Slug = productDetail.Slug,
                    Description = productDetail.Description,
                    BasePrice = productDetail.BasePrice,
                    Currency = productDetail.Currency,
                    Sku = productDetail.Sku,
                    IsActive = productDetail.IsActive,
                    VariantsCount = productDetail.Variants?.Count ?? 0,
                    TotalStockQuantity = productDetail.Variants?.Sum(v => v.StockQuantity) ?? 0,
                    Categories = productDetail.Categories?.Select(c => c.Name).ToList() ?? new List<string>(),
                    Images = imagesWithUrls,
                    CreatedAt = productDetail.CreatedAt,
                    UpdatedAt = productDetail.UpdatedAt
                });
            }

            // Create new datatable result with mapped DTOs
            var datatableResult = new DataTableResult<ProductDatatableDto>(
                result.Draw,
                result.RecordsTotal,
                result.RecordsFiltered,
                dtoItems);

            _logger.LogInformation("Retrieved {Count} products for datatable", dtoItems.Count);
            return Result.Success(datatableResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products for datatable");

            return Result.Failure<DataTableResult<ProductDatatableDto>>(
                Error.Failure(
                    code: "Products.GetDataTableFailed",
                    message: $"Failed to retrieve products: {ex.Message}"));
        }
    }
}