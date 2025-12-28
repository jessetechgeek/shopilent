using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Imaging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateVariant.V1;

internal sealed class UpdateVariantCommandHandlerV1 : ICommandHandler<UpdateVariantCommandV1, UpdateVariantResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductVariantWriteRepository _productVariantWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IS3StorageService _s3StorageService;
    private readonly IImageService _imageService;
    private readonly ILogger<UpdateVariantCommandHandlerV1> _logger;

    public UpdateVariantCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductVariantWriteRepository productVariantWriteRepository,
        ICurrentUserContext currentUserContext,
        IS3StorageService s3StorageService,
        IImageService imageService,
        ILogger<UpdateVariantCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productVariantWriteRepository = productVariantWriteRepository;
        _currentUserContext = currentUserContext;
        _s3StorageService = s3StorageService;
        _imageService = imageService;
        _logger = logger;
    }

    public async Task<Result<UpdateVariantResponseV1>> Handle(UpdateVariantCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get variant by ID
            var variant = await _productVariantWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (variant == null)
            {
                return Result.Failure<UpdateVariantResponseV1>(
                    Error.NotFound(
                        code: "ProductVariant.NotFound",
                        message: $"Product variant with ID {request.Id} not found"));
            }

            // Check if SKU is unique if provided and different from current
            if (!string.IsNullOrEmpty(request.Sku) && request.Sku != variant.Sku)
            {
                var skuExists =
                    await _productVariantWriteRepository.SkuExistsAsync(request.Sku, request.Id, cancellationToken);
                if (skuExists)
                {
                    return Result.Failure<UpdateVariantResponseV1>(
                        Error.Conflict(
                            code: "ProductVariant.DuplicateSku",
                            message: $"A product variant with SKU '{request.Sku}' already exists"));
                }
            }

            // Update variant properties
            if (!string.IsNullOrEmpty(request.Sku) || request.Price.HasValue)
            {
                // Convert decimal? to Money if price is provided
                Money? price = null;
                if (request.Price.HasValue)
                {
                    var moneyResult = Money.Create(request.Price.Value, variant.Price?.Currency ?? "USD");
                    if (moneyResult.IsFailure)
                    {
                        return Result.Failure<UpdateVariantResponseV1>(moneyResult.Error);
                    }

                    price = moneyResult.Value;
                }
                else if (variant.Price != null)
                {
                    price = variant.Price; // Preserve existing price if not updating
                }

                var updateResult = variant.Update(
                    !string.IsNullOrEmpty(request.Sku) ? request.Sku : variant.Sku,
                    price);

                if (updateResult.IsFailure)
                {
                    return Result.Failure<UpdateVariantResponseV1>(updateResult.Error);
                }
            }

            if (request.StockQuantity.HasValue)
            {
                var stockResult = variant.SetStockQuantity(request.StockQuantity.Value);
                if (stockResult.IsFailure)
                {
                    return Result.Failure<UpdateVariantResponseV1>(stockResult.Error);
                }
            }

            if (request.Metadata != null)
            {
                foreach (var item in request.Metadata)
                {
                    var metadataResult = variant.UpdateMetadata(item.Key, item.Value);
                    if (metadataResult.IsFailure)
                    {
                        return Result.Failure<UpdateVariantResponseV1>(metadataResult.Error);
                    }
                }
            }

            if (request.IsActive.HasValue)
            {
                Result statusResult;
                if (request.IsActive.Value)
                {
                    statusResult = variant.Activate();
                }
                else
                {
                    statusResult = variant.Deactivate();
                }

                if (statusResult.IsFailure)
                {
                    return Result.Failure<UpdateVariantResponseV1>(statusResult.Error);
                }
            }

            // Handle images - Remove all existing if requested
            if (request.RemoveExistingImages == true)
            {
                var existingImages = variant.Images.ToList();
                foreach (var image in existingImages)
                {
                    var removeResult = variant.RemoveImage(image);
                    if (removeResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to remove variant image: {Error}", removeResult.Error.Message);
                    }

                    // Delete the image files from S3 storage
                    try
                    {
                        await _s3StorageService.DeleteFileAsync(image.ImageKey, cancellationToken);
                        await _s3StorageService.DeleteFileAsync(image.ThumbnailKey, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete variant image from storage: {ImageKey}",
                            image.ImageKey);
                    }
                }
            }
            // Remove selected images if specified
            else if (request.ImagesToRemove != null && request.ImagesToRemove.Any())
            {
                var existingImages = variant.Images.ToList();
                foreach (var image in existingImages)
                {
                    // Check if this image's key is in the list of images to remove
                    if (request.ImagesToRemove.Contains(image.ImageKey))
                    {
                        var removeResult = variant.RemoveImage(image);
                        if (removeResult.IsFailure)
                        {
                            _logger.LogWarning("Failed to remove selected variant image {ImageKey}: {Error}",
                                image.ImageKey, removeResult.Error.Message);
                        }
                        else
                        {
                            // Delete image files from S3 storage
                            try
                            {
                                await _s3StorageService.DeleteFileAsync(image.ImageKey, cancellationToken);
                                await _s3StorageService.DeleteFileAsync(image.ThumbnailKey,
                                    cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete variant image from storage: {ImageKey}",
                                    image.ImageKey);
                            }
                        }
                    }
                }
            }

            // Add new images if provided
            if (request.Images != null && request.Images.Count > 0)
            {
                var order = variant.Images.Any() ? variant.Images.Max(i => i.DisplayOrder) + 1 : 0;
                foreach (var imageDto in request.Images)
                {
                    var image = await _imageService.ProcessProductImage(imageDto.Url);
                    var imageId = Guid.NewGuid().ToString();
                    var imageKey = "variants/" + variant.Id + "/" + imageId + ".webp";
                    var thumbnailKey = "variants/" + variant.Id + "/thumbs/" + imageId + ".webp";

                    var imageUpload = await _s3StorageService.UploadFileAsync(imageKey,
                        image.MainImage, "image/webp", cancellationToken: cancellationToken);
                    var thumbnailUpload = await _s3StorageService.UploadFileAsync(thumbnailKey,
                        image.Thumbnail, "image/webp", cancellationToken: cancellationToken);

                    // Create ProductImage value object
                    var imageResult = ProductImage.Create(
                        imageKey,
                        thumbnailKey,
                        imageDto.AltText,
                        imageDto.IsDefault,
                        order++);

                    if (imageResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to create variant image: {Error}",
                            imageResult.Error.Message);
                        continue;
                    }

                    // Add the image to the variant
                    var addImageResult = variant.AddImage(imageResult.Value);
                    if (addImageResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to add image to variant: {Error}",
                            addImageResult.Error.Message);
                    }
                }
            }

            // Handle image ordering
            if (request.ImageOrders != null && request.ImageOrders.Any())
            {
                var existingImages = variant.Images.ToList();
                var imageOrderMap = request.ImageOrders.ToDictionary(io => io.ImageKey, io => io);

                // Create a list of tuples for reordering
                var imageReorderList = new List<(ProductImage Image, int Order)>();

                // Track if we need to update default image
                ProductImage newDefaultImage = null;

                foreach (var existingImage in existingImages)
                {
                    if (imageOrderMap.TryGetValue(existingImage.ImageKey, out var orderInfo))
                    {
                        imageReorderList.Add((existingImage, orderInfo.DisplayOrder));

                        // Check if this image should be the new default
                        if (orderInfo.IsDefault == true)
                        {
                            newDefaultImage = existingImage;
                        }
                    }
                    else
                    {
                        // If image is not in the order list, keep its current order
                        imageReorderList.Add((existingImage, existingImage.DisplayOrder));
                    }
                }

                // Apply the reordering
                if (imageReorderList.Any())
                {
                    var reorderResult = variant.ReorderImages(imageReorderList);
                    if (reorderResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to reorder variant images: {Error}", reorderResult.Error.Message);
                    }
                }

                // Set new default image if specified
                if (newDefaultImage != null)
                {
                    var setDefaultResult = variant.SetDefaultImage(newDefaultImage);
                    if (setDefaultResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to set default variant image: {Error}",
                            setDefaultResult.Error.Message);
                    }
                }
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                variant.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Map images for response
            var images = variant.Images.Select(img => new VariantImageResponseDto
            {
                Url = img.ImageKey,
                AltText = img.AltText,
                IsDefault = img.IsDefault,
                DisplayOrder = img.DisplayOrder
            }).ToList();

            // Prepare response
            var response = new UpdateVariantResponseV1
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                Sku = variant.Sku,
                Price = variant.Price?.Amount ?? 0,
                Currency = variant.Price?.Currency,
                StockQuantity = variant.StockQuantity,
                IsActive = variant.IsActive,
                Metadata = variant.Metadata,
                Images = images,
                UpdatedAt = variant.UpdatedAt
            };

            _logger.LogInformation("Product variant updated successfully with ID: {VariantId}", variant.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product variant with ID {VariantId}: {ErrorMessage}", request.Id,
                ex.Message);

            return Result.Failure<UpdateVariantResponseV1>(
                Error.Failure(
                    code: "ProductVariant.UpdateFailed",
                    message: $"Failed to update product variant: {ex.Message}"));
        }
    }
}
