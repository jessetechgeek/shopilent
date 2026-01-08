using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Testing.Builders;

public class ProductBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Product";
    private string _slug = "test-product";
    private decimal _basePrice = 99.99m;
    private string _currency = "USD";
    private string _sku = null;
    private string _description = "Test product description";
    private bool _isActive = true;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;
    private readonly Dictionary<string, object> _metadata = new();

    public ProductBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }
    
    public ProductBuilder WithName(string name)
    {
        _name = name;
        return this;
    }
    
    public ProductBuilder WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }
    
    public ProductBuilder WithPrice(decimal price, string currency = "USD")
    {
        _basePrice = price;
        _currency = currency;
        return this;
    }
    
    public ProductBuilder WithSku(string sku)
    {
        _sku = sku;
        return this;
    }
    
    public ProductBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }
    
    public ProductBuilder IsInactive()
    {
        _isActive = false;
        return this;
    }
    
    public ProductBuilder CreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }
    
    public ProductBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }
    
    public Product Build()
    {
        var slugResult = Slug.Create(_slug);
        if (slugResult.IsFailure)
            throw new InvalidOperationException($"Invalid slug: {_slug}");
            
        var moneyResult = Money.Create(_basePrice, _currency);
        if (moneyResult.IsFailure)
            throw new InvalidOperationException($"Invalid price: {_basePrice} {_currency}");
            
        var productResult = string.IsNullOrEmpty(_description) 
            ? Product.Create(_name, slugResult.Value, moneyResult.Value, _sku)
            : Product.CreateWithDescription(_name, slugResult.Value, moneyResult.Value, _description, _sku);
            
        if (productResult.IsFailure)
            throw new InvalidOperationException($"Failed to create product: {productResult.Error.Message}");
            
        var product = productResult.Value;
        
        // Use reflection to set private properties
        SetPrivatePropertyValue(product, "Id", _id);
        SetPrivatePropertyValue(product, "CreatedAt", _createdAt);
        SetPrivatePropertyValue(product, "UpdatedAt", _updatedAt);
        
        // Set metadata
        foreach (var metadata in _metadata)
        {
            product.Metadata[metadata.Key] = metadata.Value;
        }
        
        // Set inactive if needed
        if (!_isActive)
        {
            product.Deactivate();
        }
        
        return product;
    }
    
    private static void SetPrivatePropertyValue<T>(object obj, string propertyName, T value)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName);
        if (propertyInfo != null)
        {
            propertyInfo.SetValue(obj, value, null);
        }
        else
        {
            var fieldInfo = obj.GetType().GetField(propertyName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
                
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
            }
            else
            {
                throw new InvalidOperationException($"Property or field {propertyName} not found on type {obj.GetType().Name}");
            }
        }
    }
}