using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Domain.Catalog;

public class Product : AggregateRoot
{
    private Product()
    {
        // Required by EF Core
    }

    private Product(string name, Slug slug, Money basePrice, string sku = null)
    {
        Name = name;
        Slug = slug;
        BasePrice = basePrice;
        Sku = sku;
        IsActive = true;
        Metadata = new Dictionary<string, object>();

        _categories = new List<ProductCategory>();
        _attributes = new List<ProductAttribute>();
    }

    public static Result<Product> Create(string name, Slug slug, Money basePrice, string sku = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Product>(ProductErrors.NameRequired);

        if (slug == null || string.IsNullOrWhiteSpace(slug.Value))
            return Result.Failure<Product>(ProductErrors.SlugRequired);

        if (basePrice == null)
            return Result.Failure<Product>(ProductErrors.NegativePrice);

        var product = new Product(name, slug, basePrice, sku);
        product.AddDomainEvent(new ProductCreatedEvent(product.Id));
        return Result.Success(product);
    }

    public static Result<Product> CreateWithDescription(string name, Slug slug, Money basePrice, string description,
        string sku = null)
    {
        var result = Create(name, slug, basePrice, sku);
        if (result.IsFailure)
            return result;

        var product = result.Value;
        product.Description = description;
        return Result.Success(product);
    }

    public static Result<Product> CreateInactive(string name, Slug slug, Money basePrice, string sku = null)
    {
        var result = Create(name, slug, basePrice, sku);
        if (result.IsFailure)
            return result;

        var product = result.Value;
        product.IsActive = false;
        return Result.Success(product);
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public Money BasePrice { get; private set; }
    public string Sku { get; private set; }
    public Slug Slug { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new Dictionary<string, object>();
    public bool IsActive { get; private set; }

    private readonly List<ProductCategory> _categories = new();
    public IReadOnlyCollection<ProductCategory> Categories => _categories.AsReadOnly();

    private readonly List<ProductAttribute> _attributes = new();
    public IReadOnlyCollection<ProductAttribute> Attributes => _attributes.AsReadOnly();

    private readonly List<ProductImage> _images = new();
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    public Result Update(string name, Slug slug, Money basePrice, string description = null, string sku = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(ProductErrors.NameRequired);

        if (slug == null || string.IsNullOrWhiteSpace(slug.Value))
            return Result.Failure(ProductErrors.SlugRequired);

        if (basePrice == null)
            return Result.Failure(ProductErrors.NegativePrice);

        Name = name;
        Slug = slug;
        BasePrice = basePrice;
        Description = description;
        Sku = sku;

        AddDomainEvent(new ProductUpdatedEvent(Id));
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Success();

        IsActive = true;
        AddDomainEvent(new ProductStatusChangedEvent(Id, true));
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        AddDomainEvent(new ProductStatusChangedEvent(Id, false));
        return Result.Success();
    }

    public Result AddCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            return Result.Failure(CategoryErrors.NotFound(Guid.Empty));

        if (_categories.Exists(pc => pc.CategoryId == categoryId))
            return Result.Success(); // Already added

        var productCategory = ProductCategory.Create(Id, categoryId);
        _categories.Add(productCategory);
        AddDomainEvent(new ProductCategoryAddedEvent(Id, categoryId));
        return Result.Success();
    }

    public Result RemoveCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            return Result.Failure(CategoryErrors.NotFound(Guid.Empty));

        var productCategory = _categories.Find(pc => pc.CategoryId == categoryId);
        if (productCategory == null)
            return Result.Failure(CategoryErrors.NotFound(categoryId));

        _categories.Remove(productCategory);
        AddDomainEvent(new ProductCategoryRemovedEvent(Id, categoryId));
        return Result.Success();
    }

    public Result AddAttribute(Guid attributeId, object value)
    {
        if (attributeId == Guid.Empty)
            return Result.Failure(AttributeErrors.NotFound(Guid.Empty));

        if (_attributes.Exists(pa => pa.AttributeId == attributeId))
            return Result.Success(); // Already added

        var productAttribute = ProductAttribute.Create(Id, attributeId, value);
        _attributes.Add(productAttribute);
        return Result.Success();
    }

    public Result RemoveAttribute(Guid attributeId)
    {
        if (attributeId == Guid.Empty)
            return Result.Failure(AttributeErrors.NotFound(Guid.Empty));

        var productAttribute = Attributes.FirstOrDefault(pa => pa.AttributeId == attributeId);
        if (productAttribute == null)
            return Result.Failure(Error.Validation(message: "Attribute is not associated with this product"));

        // Remove the attribute
        _attributes.Remove(productAttribute);
        return Result.Success();
    }

    public Result UpdateAttributeValue(Guid attributeId, object value)
    {
        var productAttribute = Attributes.FirstOrDefault(pa => pa.AttributeId == attributeId);
        if (productAttribute == null)
            return Result.Failure(Error.Validation(message: "Attribute is not associated with this product"));

        // Update the attribute value
        productAttribute.UpdateValue(value);

        return Result.Success();
    }

    public Result ClearAttributes()
    {
        _attributes.Clear();
        return Result.Success();
    }

    public Result UpdateMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(ProductErrors.InvalidMetadataKey);

        Metadata[key] = value;
        return Result.Success();
    }

    public Result Delete()
    {
        AddDomainEvent(new ProductDeletedEvent(Id));
        return Result.Success();
    }

    public Result AddImage(ProductImage image)
    {
        // If this is the first image or it's marked as default and no other default exists
        if (_images.Count == 0 || (image.IsDefault && !_images.Any(i => i.IsDefault)))
        {
            _images.Add(image);
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

        return Result.Success();
    }
}
