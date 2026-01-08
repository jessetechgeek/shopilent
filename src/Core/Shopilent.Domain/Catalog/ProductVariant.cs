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

    private ProductVariant(Product product, string sku = null, Money price = null, int stockQuantity = 0)
    {
        ProductId = product.Id;
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

        // Create via the private constructor and reflection
        var variant = new ProductVariant();
        variant.Id = Guid.NewGuid();
        variant.ProductId = productId;
        variant.Sku = sku;
        variant.Price = price;
        variant.StockQuantity = stockQuantity;
        variant.IsActive = true;
        variant.Metadata = new Dictionary<string, object>();
        // Note: _variantAttributes is initialized in the default constructor

        variant.AddDomainEvent(new ProductVariantCreatedEvent(productId, variant.Id));
        return Result.Success(variant);
    }

    // Original factory method for Product aggregate use
    internal static ProductVariant Create(Product product, string sku = null, Money price = null, int stockQuantity = 0)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        if (stockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative", nameof(stockQuantity));

        if (price != null && price.Amount < 0)
            throw new ArgumentException("Price cannot be negative", nameof(price));

        var variant = new ProductVariant(product, sku, price, stockQuantity);
        variant.AddDomainEvent(new ProductVariantCreatedEvent(product.Id, variant.Id));
        return variant;
    }

    // This method defines the domain invariants and validation
    public static Result<ProductVariant> Create(Result<Product> productResult, string sku = null, Money price = null,
        int stockQuantity = 0)
    {
        if (productResult.IsFailure)
            return Result.Failure<ProductVariant>(productResult.Error);

        if (stockQuantity < 0)
            return Result.Failure<ProductVariant>(ProductVariantErrors.NegativeStockQuantity);

        if (price != null && price.Amount < 0)
            return Result.Failure<ProductVariant>(ProductVariantErrors.NegativePrice);

        var variant = new ProductVariant(productResult.Value, sku, price, stockQuantity);
        variant.AddDomainEvent(new ProductVariantCreatedEvent(productResult.Value.Id, variant.Id));
        return Result.Success(variant);
    }

    public static Result<ProductVariant> CreateInactive(Product product, string sku = null, Money price = null,
        int stockQuantity = 0)
    {
        if (product == null)
            return Result.Failure<ProductVariant>(ProductErrors.NotFound(Guid.Empty));

        var variant = Create(product, sku, price, stockQuantity);
        variant.IsActive = false;
        variant.AddDomainEvent(new ProductVariantStatusChangedEvent(product.Id, variant.Id, false));
        return Result.Success(variant);
    }

    public static Result<ProductVariant> CreateOutOfStock(Product product, string sku = null, Money price = null)
    {
        var variant = Create(product, sku, price, 0);
        variant.AddDomainEvent(new ProductVariantStockChangedEvent(product.Id, variant.Id, 0, 0));
        return Result.Success(variant);
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

    // Backward compatibility methods - now they just call the self-contained methods
    internal Result Update(string sku, Money price, Product product)
    {
        return Update(sku, price);
    }

    internal Result SetStockQuantity(int quantity, Product product)
    {
        return SetStockQuantity(quantity);
    }

    internal Result AddStock(int quantity, Product product)
    {
        return AddStock(quantity);
    }

    internal Result RemoveStock(int quantity, Product product)
    {
        return RemoveStock(quantity);
    }

    internal Result Activate(Product product)
    {
        return Activate();
    }

    internal Result Deactivate(Product product)
    {
        return Deactivate();
    }

    public Result AddAttribute(Attribute attribute, object value, Product product = null)
    {
        if (attribute == null)
            return Result.Failure(AttributeErrors.NotFound(Guid.Empty));

        if (!attribute.IsVariant)
            return Result.Failure(ProductVariantErrors.NonVariantAttribute(attribute.Name));

        if (_variantAttributes.Exists(va => va.AttributeId == attribute.Id))
            return Result.Success(); // Already exists

        try
        {
            var variantAttribute = VariantAttribute.Create(this, attribute, value);
            _variantAttributes.Add(variantAttribute);

            AddDomainEvent(new ProductVariantAttributeAddedEvent(ProductId, Id, attribute.Id));
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

    public Result UpdateMetadata(string key, object value, Product product = null)
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
    public Result UpdateAttributeValue(Guid attributeId, object value, Product product = null)
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
