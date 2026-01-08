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
using Shopilent.Domain.Common.ValueObjects;
using Attribute = Shopilent.Domain.Catalog.Attribute;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateProduct.V1;

internal sealed class UpdateProductCommandHandlerV1 : ICommandHandler<UpdateProductCommandV1, UpdateProductResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductWriteRepository _productWriteRepository;
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly IAttributeWriteRepository _attributeWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IS3StorageService _s3StorageService;
    private readonly IImageService _imageService;
    private readonly ILogger<UpdateProductCommandHandlerV1> _logger;

    public UpdateProductCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductWriteRepository productWriteRepository,
        ICategoryWriteRepository categoryWriteRepository,
        IAttributeWriteRepository attributeWriteRepository,
        ICurrentUserContext currentUserContext,
        IS3StorageService s3StorageService,
        IImageService imageService,
        ILogger<UpdateProductCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productWriteRepository = productWriteRepository;
        _categoryWriteRepository = categoryWriteRepository;
        _attributeWriteRepository = attributeWriteRepository;
        _currentUserContext = currentUserContext;
        _s3StorageService = s3StorageService;
        _imageService = imageService;
        _logger = logger;
    }

    public async Task<Result<UpdateProductResponseV1>> Handle(UpdateProductCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get product by ID
            var product = await _productWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (product == null)
            {
                return Result.Failure<UpdateProductResponseV1>(ProductErrors.NotFound(request.Id));
            }

            // Check if slug already exists (but exclude current product)
            if (product.Slug.Value != request.Slug)
            {
                var slugExists =
                    await _productWriteRepository.SlugExistsAsync(request.Slug, request.Id, cancellationToken);
                if (slugExists)
                {
                    return Result.Failure<UpdateProductResponseV1>(ProductErrors.DuplicateSlug(request.Slug));
                }
            }

            // Check if SKU exists (but exclude current product)
            if (!string.IsNullOrWhiteSpace(request.Sku) && request.Sku != product.Sku)
            {
                var skuExists =
                    await _productWriteRepository.SkuExistsAsync(request.Sku, request.Id, cancellationToken);
                if (skuExists)
                {
                    return Result.Failure<UpdateProductResponseV1>(ProductErrors.DuplicateSku(request.Sku));
                }
            }

            // Create slug value object
            var slugResult = Slug.Create(request.Slug);
            if (slugResult.IsFailure)
            {
                return Result.Failure<UpdateProductResponseV1>(slugResult.Error);
            }

            // Create money value object for base price
            var priceResult = Money.Create(request.BasePrice, product.BasePrice.Currency); // Preserve existing currency
            if (priceResult.IsFailure)
            {
                return Result.Failure<UpdateProductResponseV1>(priceResult.Error);
            }

            // Update product
            var updateResult = product.Update(
                request.Name,
                slugResult.Value,
                priceResult.Value,
                request.Description,
                request.Sku);

            if (updateResult.IsFailure)
            {
                return Result.Failure<UpdateProductResponseV1>(updateResult.Error);
            }

            // Update active status if specified
            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value && !product.IsActive)
                {
                    product.Activate();
                }
                else if (!request.IsActive.Value && product.IsActive)
                {
                    product.Deactivate();
                }
            }

            // Update categories if provided
            if (request.CategoryIds != null)
            {
                // Get current category IDs
                var currentCategoryIds = product.Categories.Select(pc => pc.CategoryId).ToList();

                // Categories to remove (in current but not in request)
                var categoriesToRemove = currentCategoryIds
                    .Where(categoryId => !request.CategoryIds.Contains(categoryId))
                    .ToList();

                foreach (var categoryId in categoriesToRemove)
                {
                    // Load the category to remove
                    var categoryToRemove = await _categoryWriteRepository.GetByIdAsync(categoryId, cancellationToken);
                    if (categoryToRemove != null)
                    {
                        product.RemoveCategory(categoryToRemove);
                    }
                }

                // Categories to add (in request but not in current)
                var categoriesToAdd = request.CategoryIds.Except(currentCategoryIds).ToList();
                foreach (var categoryId in categoriesToAdd)
                {
                    var category = await _categoryWriteRepository.GetByIdAsync(categoryId, cancellationToken);
                    if (category != null)
                    {
                        product.AddCategory(category);
                    }
                    else
                    {
                        _logger.LogWarning("Category with ID {CategoryId} not found while updating product",
                            categoryId);
                    }
                }
            }

            // Update attributes if provided
            if (request.Attributes != null)
            {
                // Clear existing attributes and add all the new/updated ones
                // This is simpler than trying to update individual attributes
                // First, load all needed attribute entities
                var attributeEntities = new Dictionary<Guid, Attribute>();
                foreach (var attributeDto in request.Attributes)
                {
                    if (!attributeEntities.ContainsKey(attributeDto.AttributeId))
                    {
                        var attribute =
                            await _attributeWriteRepository.GetByIdAsync(attributeDto.AttributeId, cancellationToken);
                        if (attribute != null)
                        {
                            attributeEntities[attributeDto.AttributeId] = attribute;
                        }
                        else
                        {
                            _logger.LogWarning("Attribute with ID {AttributeId} not found while updating product",
                                attributeDto.AttributeId);
                        }
                    }
                }

                // Now clear all existing product attributes and re-add them
                // This is typically done through a domain method on the Product entity
                var clearResult = product.ClearAttributes();
                if (clearResult.IsFailure)
                {
                    _logger.LogWarning("Failed to clear existing attributes: {Error}", clearResult.Error.Message);
                }

                // Add all attributes from the request
                foreach (var attributeDto in request.Attributes)
                {
                    if (attributeEntities.TryGetValue(attributeDto.AttributeId, out var attribute))
                    {
                        var addResult = product.AddAttribute(attribute, attributeDto.Value);
                        if (addResult.IsFailure)
                        {
                            _logger.LogWarning("Failed to add attribute {AttributeId} to product: {Error}",
                                attributeDto.AttributeId, addResult.Error.Message);
                        }
                    }
                }
            }

            // Handle images - Remove all existing if requested
            if (request.RemoveExistingImages == true)
            {
                // Remove existing images (ideally the domain model should have a method for this)
                // Since we don't have direct access to modify _images collection in Product entity,
                // we might need to remove them one by one
                var existingImages = product.Images.ToList();
                foreach (var image in existingImages)
                {
                    var removeResult = product.RemoveImage(image);
                    if (removeResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to remove image: {Error}", removeResult.Error.Message);
                    }

                    // Optionally, delete the image files from S3 storage
                    try
                    {
                        await _s3StorageService.DeleteFileAsync(image.ImageKey, cancellationToken);
                        await _s3StorageService.DeleteFileAsync(image.ThumbnailKey, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete image from storage: {ImageKey}", image.ImageKey);
                    }
                }
            }
            // Remove selected images if specified
            else if (request.ImagesToRemove != null && request.ImagesToRemove.Any())
            {
                var existingImages = product.Images.ToList();
                foreach (var image in existingImages)
                {
                    // Check if this image's key is in the list of images to remove
                    if (request.ImagesToRemove.Contains(image.ImageKey))
                    {
                        var removeResult = product.RemoveImage(image);
                        if (removeResult.IsFailure)
                        {
                            _logger.LogWarning("Failed to remove selected image {ImageKey}: {Error}",
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
                                _logger.LogWarning(ex, "Failed to delete image from storage: {ImageKey}",
                                    image.ImageKey);
                            }
                        }
                    }
                }
            }

            // Add new images if provided
            if (request.Images != null && request.Images.Count > 0)
            {
                var order = product.Images.Any() ? product.Images.Max(i => i.DisplayOrder) + 1 : 0;
                foreach (var imageDto in request.Images)
                {
                    var image = await _imageService.ProcessProductImage(imageDto.Url);
                    var imageId = Guid.NewGuid().ToString();
                    var imageKey = "products/" + product.Id + "/" + imageId + ".webp";
                    var thumbnailKey = "products/" + product.Id + "/thumbs/" + imageId + ".webp";
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
                        _logger.LogWarning("Failed to create product image: {Error}",
                            imageResult.Error.Message);
                        continue;
                    }

                    // Add the image to the product
                    var addImageResult = product.AddImage(imageResult.Value);
                    if (addImageResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to add image to product: {Error}",
                            addImageResult.Error.Message);
                    }
                }
            }

            if (request.ImageOrders != null && request.ImageOrders.Any())
            {
                var existingImages = product.Images.ToList();
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
                    var reorderResult = product.ReorderImages(imageReorderList);
                    if (reorderResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to reorder images: {Error}", reorderResult.Error.Message);
                    }
                }

                // Set new default image if specified
                if (newDefaultImage != null)
                {
                    var setDefaultResult = product.SetDefaultImage(newDefaultImage);
                    if (setDefaultResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to set default image: {Error}", setDefaultResult.Error.Message);
                    }
                }
            }


            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                product.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            // Get updated category IDs
            var categoryIds = product.Categories.Select(pc => pc.CategoryId).ToList();

            // Get attribute details
            var attributes = product.Attributes.Select(pa => new ProductAttributeResponseDto
            {
                AttributeId = pa.AttributeId,
                AttributeName = pa.AttributeId.ToString(), // Ideally fetch the name from somewhere
                Values = pa.Values
            }).ToList();

            // Map images for response
            var images = product.Images.Select(img => new ProductImageResponseDto
            {
                Url = img.ImageKey,
                AltText = img.AltText,
                IsDefault = img.IsDefault,
                DisplayOrder = img.DisplayOrder
            }).ToList();

            // Create response
            var response = new UpdateProductResponseV1
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                BasePrice = product.BasePrice.Amount,
                Currency = product.BasePrice.Currency,
                Slug = product.Slug.Value,
                Sku = product.Sku,
                IsActive = product.IsActive,
                CategoryIds = categoryIds,
                Attributes = attributes,
                Images = images,
                UpdatedAt = product.UpdatedAt
            };

            _logger.LogInformation("Product updated successfully with ID: {ProductId}", product.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product with ID {ProductId}: {ErrorMessage}", request.Id, ex.Message);

            return Result.Failure<UpdateProductResponseV1>(
                Error.Failure(
                    "Product.UpdateFailed",
                    "Failed to update product"
                ));
        }
    }
}
