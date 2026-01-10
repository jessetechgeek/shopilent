using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Imaging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Application.Features.Catalog.Commands.CreateProduct.V1;

internal sealed class CreateProductCommandHandlerV1 : ICommandHandler<CreateProductCommandV1, CreateProductResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductWriteRepository _productWriteRepository;
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly IAttributeWriteRepository _attributeWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IS3StorageService _s3StorageService;
    private readonly IImageService _imageService;
    private readonly ILogger<CreateProductCommandHandlerV1> _logger;

    public CreateProductCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductWriteRepository productWriteRepository,
        ICategoryWriteRepository categoryWriteRepository,
        IAttributeWriteRepository attributeWriteRepository,
        ICurrentUserContext currentUserContext,
        IS3StorageService s3StorageService,
        IImageService imageService,
        ILogger<CreateProductCommandHandlerV1> logger)
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

    public async Task<Result<CreateProductResponseV1>> Handle(CreateProductCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if slug already exists
            var slugExists = await _productWriteRepository.SlugExistsAsync(request.Slug, null, cancellationToken);
            if (slugExists)
            {
                return Result.Failure<CreateProductResponseV1>(ProductErrors.DuplicateSlug(request.Slug));
            }

            // Check if SKU exists
            if (!string.IsNullOrWhiteSpace(request.Sku))
            {
                var skuExists = await _productWriteRepository.SkuExistsAsync(request.Sku, null, cancellationToken);
                if (skuExists)
                {
                    return Result.Failure<CreateProductResponseV1>(ProductErrors.DuplicateSku(request.Sku));
                }
            }

            // Create slug value object
            var slugResult = Slug.Create(request.Slug);
            if (slugResult.IsFailure)
            {
                return Result.Failure<CreateProductResponseV1>(slugResult.Error);
            }

            // Create money value object for price
            var moneyResult = Money.Create(request.BasePrice, request.Currency);
            if (moneyResult.IsFailure)
            {
                return Result.Failure<CreateProductResponseV1>(moneyResult.Error);
            }

            // Create product
            var productResult = Product.Create(request.Name, slugResult.Value, moneyResult.Value, request.Sku);
            if (productResult.IsFailure)
            {
                return Result.Failure<CreateProductResponseV1>(productResult.Error);
            }

            var product = productResult.Value;

            // Set description if provided
            if (!string.IsNullOrEmpty(request.Description))
            {
                product.Update(product.Name, product.Slug, product.BasePrice, request.Description, product.Sku);
            }

            // Set active status if provided
            if (!request.IsActive)
            {
                product.Deactivate();
            }

            // Add metadata
            if (request.Metadata != null && request.Metadata.Count > 0)
            {
                foreach (var item in request.Metadata)
                {
                    product.UpdateMetadata(item.Key, item.Value);
                }
            }

            // Add categories if provided
            if (request.CategoryIds != null && request.CategoryIds.Count > 0)
            {
                foreach (var categoryId in request.CategoryIds)
                {
                    var category = await _categoryWriteRepository.GetByIdAsync(categoryId, cancellationToken);
                    if (category != null)
                    {
                        product.AddCategory(category.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Category with ID {CategoryId} not found while creating product",
                            categoryId);
                    }
                }
            }

            // Add attributes if provided
            if (request.Attributes != null && request.Attributes.Count > 0)
            {
                foreach (var attributeDto in request.Attributes)
                {
                    var attribute =
                        await _attributeWriteRepository.GetByIdAsync(attributeDto.AttributeId, cancellationToken);
                    if (attribute != null)
                    {
                        // Add the attribute to the product
                        var addAttributeResult = product.AddAttribute(attribute.Id, attributeDto.Value);
                        if (addAttributeResult.IsFailure)
                        {
                            _logger.LogWarning("Failed to add attribute {AttributeId} to product: {Error}",
                                attributeDto.AttributeId, addAttributeResult.Error.Message);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Attribute with ID {AttributeId} not found while creating product",
                            attributeDto.AttributeId);
                    }
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
                        "",
                        false,
                        order);

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

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                product.SetCreationAuditInfo(_currentUserContext.UserId);
            }

            // Add to repository
            await _productWriteRepository.AddAsync(product, cancellationToken);

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            // Create response with category IDs
            var categoryIds = product.Categories.Select(pc => pc.CategoryId).ToList();

            // Add this to the response mapping in the handler
            var attributes = product.Attributes.Select(pa => new ProductAttributeResponseDto
            {
                AttributeId = pa.AttributeId, Values = pa.Values
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
            var response = new CreateProductResponseV1
            {
                Id = product.Id,
                Name = product.Name,
                Slug = product.Slug.Value,
                Description = product.Description,
                BasePrice = product.BasePrice.Amount,
                Currency = product.BasePrice.Currency,
                Sku = product.Sku,
                IsActive = product.IsActive,
                Metadata = product.Metadata,
                CategoryIds = categoryIds,
                Attributes = attributes,
                Images = images,
                CreatedAt = product.CreatedAt
            };

            _logger.LogInformation("Product created successfully with ID: {ProductId}", product.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product: {ErrorMessage}", ex.Message);

            return Result.Failure<CreateProductResponseV1>(
                Error.Failure(
                    "Product.CreateFailed",
                    "Failed to create product"
                ));
        }
    }
}
