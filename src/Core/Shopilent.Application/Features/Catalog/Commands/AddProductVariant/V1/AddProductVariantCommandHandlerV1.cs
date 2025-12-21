using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Imaging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.Features.Catalog.Commands.AddProductVariant.V1;

internal sealed class
    AddProductVariantCommandHandlerV1 : ICommandHandler<AddProductVariantCommandV1, AddProductVariantResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IS3StorageService _s3StorageService;
    private readonly IImageService _imageService;
    private readonly ILogger<AddProductVariantCommandHandlerV1> _logger;

    public AddProductVariantCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IS3StorageService s3StorageService,
        IImageService imageService,
        ILogger<AddProductVariantCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _s3StorageService = s3StorageService;
        _imageService = imageService;
        _logger = logger;
    }

    public async Task<Result<AddProductVariantResponseV1>> Handle(AddProductVariantCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get product
            var product = await _unitOfWork.ProductWriter.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null)
            {
                return Result.Failure<AddProductVariantResponseV1>(ProductErrors.NotFound(request.ProductId));
            }

            // Check if SKU already exists
            if (!string.IsNullOrEmpty(request.Sku) &&
                await _unitOfWork.ProductVariantWriter.SkuExistsAsync(request.Sku, null, cancellationToken))
            {
                return Result.Failure<AddProductVariantResponseV1>(ProductVariantErrors.DuplicateSku(request.Sku));
            }

            // Verify attributes exist and are variant attributes
            var attributeValues = new Dictionary<Guid, object>();
            var attributesInfo = new Dictionary<Guid, string>(); // Keep track of attribute names for response

            foreach (var attributeEntry in request.Attributes)
            {
                var attribute =
                    await _unitOfWork.AttributeReader.GetByIdAsync(attributeEntry.AttributeId, cancellationToken);
                if (attribute == null)
                {
                    return Result.Failure<AddProductVariantResponseV1>(
                        AttributeErrors.NotFound(attributeEntry.AttributeId));
                }

                if (!attribute.IsVariant)
                {
                    return Result.Failure<AddProductVariantResponseV1>(
                        AttributeErrors.NotVariantAttribute(attribute.Name));
                }

                attributeValues.Add(attributeEntry.AttributeId, attributeEntry.Value);
                attributesInfo.Add(attributeEntry.AttributeId, attribute.Name);
            }

            // Create money value object - use product's base price if variant price not provided
            Money price;
            if (request.Price.HasValue)
            {
                var moneyResult = Money.Create(request.Price.Value, "USD"); // Currency hardcoded for simplicity
                if (moneyResult.IsFailure)
                {
                    return Result.Failure<AddProductVariantResponseV1>(moneyResult.Error);
                }

                price = moneyResult.Value;
            }
            else
            {
                price = product.BasePrice;
            }

            // Create the product variant using the correct method signature
            var variantResult = ProductVariant.Create(
                request.ProductId,
                request.Sku,
                price,
                request.StockQuantity
            );

            if (variantResult.IsFailure)
            {
                return Result.Failure<AddProductVariantResponseV1>(variantResult.Error);
            }

            var variant = variantResult.Value;

            // Set IsActive if different from default
            if (!request.IsActive)
            {
                variant.Deactivate();
            }

            // Add metadata if provided
            if (request.Metadata != null && request.Metadata.Count > 0)
            {
                foreach (var metadataEntry in request.Metadata)
                {
                    variant.UpdateMetadata(metadataEntry.Key, metadataEntry.Value);
                }
            }

            // Add attribute values
            foreach (var attributeEntry in attributeValues)
            {
                var attribute = await _unitOfWork.AttributeWriter.GetByIdAsync(attributeEntry.Key, cancellationToken);
                var attributeValueResult = variant.AddAttribute(attribute, attributeEntry.Value);
                if (attributeValueResult.IsFailure)
                {
                    return Result.Failure<AddProductVariantResponseV1>(attributeValueResult.Error);
                }
            }

            // Add images if provided
            if (request.Images != null && request.Images.Count > 0)
            {
                var order = 0;
                foreach (var imageDto in request.Images)
                {
                    order++;
                    var image = await _imageService.ProcessProductImage(imageDto.Url);
                    var imageId = Guid.NewGuid().ToString();
                    var imageKey = "products/" + request.ProductId + "/variants/" + variant.Id + "/" + imageId +
                                   ".webp";
                    var thumbnailKey = "products/" + request.ProductId + "/variants/" + variant.Id + "/thumbs/" +
                                       imageId + ".webp";

                    var imageUpload = await _s3StorageService.UploadFileAsync(imageKey,
                        image.MainImage, "image/webp", cancellationToken: cancellationToken);
                    var thumbnailUpload = await _s3StorageService.UploadFileAsync(thumbnailKey,
                        image.Thumbnail, "image/webp", cancellationToken: cancellationToken);

                    // Create ProductImage value object
                    var imageResult = ProductImage.Create(
                        imageKey,
                        thumbnailKey,
                        imageDto.AltText ?? "",
                        imageDto.IsDefault,
                        order);

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

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                variant.SetCreationAuditInfo(_currentUserContext.UserId);
            }

            // Add variant to product
            var addResult = product.AddVariant(variant);
            if (addResult.IsFailure)
            {
                return Result.Failure<AddProductVariantResponseV1>(addResult.Error);
            }

            // Add to repository
            await _unitOfWork.ProductVariantWriter.AddAsync(variant, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Create response
            var attributeDtos = attributeValues.Select(a => new VariantAttributeDto
            {
                AttributeId = a.Key, Name = attributesInfo[a.Key], Value = a.Value
            }).ToList();

            // Map images for response
            var images = variant.Images.Select(img => new ProductImageResponseDto
            {
                Url = img.ImageKey,
                AltText = img.AltText,
                IsDefault = img.IsDefault,
                DisplayOrder = img.DisplayOrder
            }).ToList();

            var response = new AddProductVariantResponseV1
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                Sku = variant.Sku,
                Price = variant.Price?.Amount,
                StockQuantity = variant.StockQuantity,
                IsActive = variant.IsActive,
                Metadata = request.Metadata,
                CreatedAt = variant.CreatedAt,
                Attributes = attributeDtos,
                Images = images
            };

            _logger.LogInformation("Product variant created successfully with ID: {VariantId}", variant.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product variant: {ErrorMessage}", ex.Message);

            return Result.Failure<AddProductVariantResponseV1>(
                Error.Failure(
                    "ProductVariant.CreateFailed",
                    Domain.Common.Errors.ErrorType.Failure.ToString()
                ));
        }
    }
}
