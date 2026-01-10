using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Domain.Catalog;

public class ProductVariant : AggregateRoot
{
    private ProductVariant()
    {
        // Required by EF Core
        _variantAttributes = new List<VariantAttribute>();
        Metadata = new Dictionary<string, object>();
    }

    private ProductVariant(Guid productId, string sku = null, Money price = null, int stockQuantity = 0)
    {
        ProductId = productId;
        Sku = sku;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
        Metadata = new Dictionary<string, object>();
        _variantAttributes = new List<VariantAttribute>();
    }

    // Static factory method that doesn't rely on an existing Product instance
    public static Result<ProductVariant> Create(Guid productId, string sku = null, Money price = null,
        int stockQuantity = 0)
    {
        if (productId == Guid.Empty)
            return Result.Failure<ProductVariant>(ProductErrors.NotFound(productId));

        if (stockQuantity < 0)
            return Result.Failure<ProductVariant>(ProductVariantErrors.NegativeStockQuantity);

        if (price != null && price.Amount < 0)
            return Result.Failure<ProductVariant>(ProductVariantErrors.NegativePrice);

        var variant = new ProductVariant(productId, sku, price, stockQuantity);
        variant.AddDomainEvent(new ProductVariantCreatedEvent(productId, variant.Id));
        return Result.Success(variant);
    }

    public static Result<ProductVariant> CreateInactive(Guid productId, string sku = null, Money price = null,
        int stockQuantity = 0)
    {
        var result = Create(productId, sku, price, stockQuantity);
        if (result.IsFailure)
            return result;

        var variant = result.Value;
        variant.Deactivate();
        return Result.Success(variant);
    }

    public static Result<ProductVariant> CreateOutOfStock(Guid productId, string sku = null, Money price = null)
    {
        return Create(productId, sku, price, 0);
    }

    public Guid ProductId { get; private set; }
    public string Sku { get; private set; }
    public Money Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

    private readonly List<VariantAttribute> _variantAttributes = new();
    public IReadOnlyCollection<VariantAttribute> VariantAttributes => _variantAttributes.AsReadOnly();

    private readonly List<ProductImage> _images = new();
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    // Self-contained update methods that raise their own domain events
    public Result Update(string sku, Money price)
    {
        if (price != null && price.Amount < 0)
            return Result.Failure(ProductVariantErrors.NegativePrice);

        Sku = sku;
        Price = price;

        AddDomainEvent(new ProductVariantUpdatedEvent(ProductId, Id));
        return Result.Success();
    }

    // Encapsulates the business logic for variant stock management
    public Result SetStockQuantity(int quantity)
    {
        if (quantity < 0)
            return Result.Failure(ProductVariantErrors.NegativeStockQuantity);

        var oldQuantity = StockQuantity;
        StockQuantity = quantity;

        AddDomainEvent(new ProductVariantStockChangedEvent(ProductId, Id, oldQuantity, quantity));
        return Result.Success();
    }

    public Result AddStock(int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(ProductVariantErrors.NegativeStockQuantity);

        var oldQuantity = StockQuantity;
        StockQuantity += quantity;

        AddDomainEvent(new ProductVariantStockChangedEvent(ProductId, Id, oldQuantity, StockQuantity));
        return Result.Success();
    }

    public Result RemoveStock(int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(ProductVariantErrors.NegativeStockQuantity);

        if (StockQuantity < quantity)
            return Result.Failure(ProductVariantErrors.InsufficientStock(quantity, StockQuantity));

        var oldQuantity = StockQuantity;
        StockQuantity -= quantity;

        AddDomainEvent(new ProductVariantStockChangedEvent(ProductId, Id, oldQuantity, StockQuantity));
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Success();

        IsActive = true;
        AddDomainEvent(new ProductVariantStatusChangedEvent(ProductId, Id, true));
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        AddDomainEvent(new ProductVariantStatusChangedEvent(ProductId, Id, false));
        return Result.Success();
    }

    public Result AddAttribute(Guid attributeId, object value)
    {
        if (attributeId == Guid.Empty)
            return Result.Failure(AttributeErrors.NotFound(Guid.Empty));

        if (_variantAttributes.Exists(va => va.AttributeId == attributeId))
            return Result.Success(); // Already exists

        try
        {
            var variantAttribute = VariantAttribute.Create(Id, attributeId, value);
            _variantAttributes.Add(variantAttribute);

            AddDomainEvent(new ProductVariantAttributeAddedEvent(ProductId, Id, attributeId));
            return Result.Success();
        }
        catch (ArgumentNullException)
        {
            return Result.Failure(AttributeErrors.NotFound(Guid.Empty));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(new Error(
                code: "ProductVariant.InvalidAttribute",
                message: ex.Message
            ));
        }
    }

    public Result UpdateMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(ProductVariantErrors.InvalidMetadataKey);

        Metadata[key] = value;
        AddDomainEvent(new ProductVariantUpdatedEvent(ProductId, Id));
        return Result.Success();
    }

    // Helper method to check if a variant attribute with specified ID exists
    public bool HasAttribute(Guid attributeId)
    {
        return _variantAttributes.Exists(a => a.AttributeId == attributeId);
    }

    // Get an attribute value
    public Result<object> GetAttributeValue(Guid attributeId)
    {
        var attribute = _variantAttributes.Find(a => a.AttributeId == attributeId);
        if (attribute == null)
            return Result.Failure<object>(AttributeErrors.NotFound(attributeId));

        if (!attribute.Value.ContainsKey("value"))
            return Result.Failure<object>(AttributeErrors.InvalidConfigurationFormat);

        return Result.Success(attribute.Value["value"]);
    }

    // Update an attribute value
    public Result UpdateAttributeValue(Guid attributeId, object value)
    {
        var attribute = _variantAttributes.Find(a => a.AttributeId == attributeId);
        if (attribute == null)
            return Result.Failure(AttributeErrors.NotFound(attributeId));

        var updateResult = attribute.UpdateValue(value);
        if (updateResult.IsFailure)
            return updateResult;

        AddDomainEvent(new ProductVariantAttributeUpdatedEvent(ProductId, Id, attributeId));
        return Result.Success();
    }

    public Result Delete()
    {
        AddDomainEvent(new ProductVariantDeletedEvent(ProductId, Id));
        return Result.Success();
    }

    public Result AddImage(ProductImage image)
    {
        // If this is the first image or it's marked as default and no other default exists
        if (_images.Count == 0 || (image.IsDefault && !_images.Any(i => i.IsDefault)))
        {
            _images.Add(image);
            AddDomainEvent(new ProductVariantUpdatedEvent(ProductId, Id));
            return Result.Success();
        }

        // If this image is marked as default, remove default from other images
        if (image.IsDefault)
        {
            foreach (var existingImage in _images.Where(i => i.IsDefault))
            {
                existingImage.RemoveDefault();
            }
        }

        _images.Add(image);
        AddDomainEvent(new ProductVariantUpdatedEvent(ProductId, Id));
        return Result.Success();
    }

    public Result RemoveImage(ProductImage image)
    {
        if (!_images.Contains(image))
            return Result.Failure(Error.Validation(message: "Image not found"));

        bool wasDefault = image.IsDefault;
        _images.Remove(image);

        // If removed image was default and we have other images, set the first one as default
        if (wasDefault && _images.Any())
        {
            _images.First().SetAsDefault();
        }

        AddDomainEvent(new ProductVariantUpdatedEvent(ProductId, Id));
        return Result.Success();
    }

    public Result SetDefaultImage(ProductImage image)
    {
        if (!_images.Contains(image))
            return Result.Failure(Error.Validation(message: "Image not found"));

        foreach (var existingImage in _images)
        {
            existingImage.RemoveDefault();
        }

        image.SetAsDefault();
        AddDomainEvent(new ProductVariantUpdatedEvent(ProductId, Id));
        return Result.Success();
    }

    public Result ReorderImages(List<(ProductImage Image, int Order)> newOrder)
    {
        foreach (var (image, order) in newOrder)
        {
            if (!_images.Contains(image))
                return Result.Failure(Error.Validation(message: "One or more images not found"));

            image.UpdateDisplayOrder(order);
        }

        AddDomainEvent(new ProductVariantUpdatedEvent(ProductId, Id));
        return Result.Success();
    }
}
