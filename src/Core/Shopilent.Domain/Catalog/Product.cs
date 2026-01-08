using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Events;
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
        _variants = new List<ProductVariant>();
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

    private readonly List<ProductVariant> _variants = new();
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

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

    public Result AddCategory(Category category)
    {
        if (category == null)
            return Result.Failure(CategoryErrors.NotFound(Guid.Empty));

        if (_categories.Exists(pc => pc.CategoryId == category.Id))
            return Result.Success(); // Already added

        var productCategory = ProductCategory.Create(this, category);
        _categories.Add(productCategory);
        AddDomainEvent(new ProductCategoryAddedEvent(Id, category.Id));
        return Result.Success();
    }

    public Result RemoveCategory(Category category)
    {
        if (category == null)
            return Result.Failure(CategoryErrors.NotFound(Guid.Empty));

        var productCategory = _categories.Find(pc => pc.CategoryId == category.Id);
        if (productCategory == null)
            return Result.Failure(CategoryErrors.NotFound(category.Id));

        _categories.Remove(productCategory);
        AddDomainEvent(new ProductCategoryRemovedEvent(Id, category.Id));
        return Result.Success();
    }

    public Result AddAttribute(Attribute attribute, object value)
    {
        if (attribute == null)
            return Result.Failure(AttributeErrors.NotFound(Guid.Empty));

        if (_attributes.Exists(pa => pa.AttributeId == attribute.Id))
            return Result.Success(); // Already added

        var productAttribute = ProductAttribute.Create(this, attribute, value);
        _attributes.Add(productAttribute);
        return Result.Success();
    }

    public Result RemoveAttribute(Attribute attribute)
    {
        if (attribute == null)
            return Result.Failure(Error.Validation(message: "Attribute cannot be null"));

        var productAttribute = Attributes.FirstOrDefault(pa => pa.AttributeId == attribute.Id);
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

    public Result AddVariant(ProductVariant variant)
    {
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(Guid.Empty));

        // Check if the SKU is already used
        if (!string.IsNullOrEmpty(variant.Sku) && _variants.Exists(v => v.Sku == variant.Sku))
            return Result.Failure(ProductVariantErrors.DuplicateSku(variant.Sku));

        _variants.Add(variant);
        AddDomainEvent(new ProductVariantAddedEvent(Id, variant.Id));
        return Result.Success();
    }

    public Result UpdateMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(ProductErrors.InvalidMetadataKey);

        Metadata[key] = value;
        return Result.Success();
    }

    // Methods for operating on variants
    public Result UpdateVariantStock(Guid variantId, int newQuantity)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.SetStockQuantity(newQuantity, this);
    }

    public Result AddVariantStock(Guid variantId, int quantity)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.AddStock(quantity, this);
    }

    public Result RemoveVariantStock(Guid variantId, int quantity)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.RemoveStock(quantity, this);
    }

    public Result ActivateVariant(Guid variantId)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.Activate(this);
    }

    public Result DeactivateVariant(Guid variantId)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.Deactivate(this);
    }

    public Result AddVariantAttribute(Guid variantId, Attribute attribute, object value)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.AddAttribute(attribute, value, this);
    }

    public Result UpdateVariantMetadata(Guid variantId, string key, object value)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.UpdateMetadata(key, value, this);
    }

    public Result UpdateVariant(Guid variantId, string sku, Money price)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure(ProductVariantErrors.NotFound(variantId));

        return variant.Update(sku, price, this);
    }

    // Method to get a variant by ID
    public Result<ProductVariant> GetVariant(Guid variantId)
    {
        var variant = _variants.Find(v => v.Id == variantId);
        if (variant == null)
            return Result.Failure<ProductVariant>(ProductVariantErrors.NotFound(variantId));

        return Result.Success(variant);
    }

    public void RaiseVariantEvent(DomainEvent domainEvent)
    {
        AddDomainEvent(domainEvent);
    }

    public Result Delete()
    {
        if (!_variants.Any())
        {
            AddDomainEvent(new ProductDeletedEvent(Id));
            return Result.Success();
        }
        else
        {
            foreach (var variant in _variants)
            {
                AddDomainEvent(new ProductVariantDeletedEvent(Id, variant.Id));
            }

            AddDomainEvent(new ProductDeletedEvent(Id));
            return Result.Success();
        }
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
