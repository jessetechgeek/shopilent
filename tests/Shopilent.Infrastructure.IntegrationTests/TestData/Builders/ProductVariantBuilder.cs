using Bogus;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class ProductVariantBuilder
{
    private Guid _productId;
    private string _sku;
    private Money _price;
    private int _stockQuantity;
    private bool _isActive;
    private readonly Dictionary<string, object> _metadata;

    public ProductVariantBuilder()
    {
        var faker = new Faker();
        _productId = Guid.NewGuid(); // Will be overridden when building with product
        _sku = faker.Random.AlphaNumeric(8).ToUpper();
        _price = Money.Create(Math.Round(faker.Random.Decimal(10, 500), 2), "USD").Value;
        _stockQuantity = faker.Random.Int(0, 100);
        _isActive = true;
        _metadata = new Dictionary<string, object>();
    }

    public ProductVariantBuilder WithProductId(Guid productId)
    {
        _productId = productId;
        return this;
    }

    public ProductVariantBuilder WithSku(string sku)
    {
        _sku = sku;
        return this;
    }

    public ProductVariantBuilder WithPrice(decimal price, string currency = "USD")
    {
        _price = Money.Create(Math.Round(price, 2), currency).Value;
        return this;
    }

    public ProductVariantBuilder WithoutPrice()
    {
        _price = null;
        return this;
    }

    public ProductVariantBuilder WithStock(int stockQuantity)
    {
        _stockQuantity = stockQuantity;
        return this;
    }

    public ProductVariantBuilder OutOfStock()
    {
        _stockQuantity = 0;
        return this;
    }

    public ProductVariantBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public ProductVariantBuilder AsActive()
    {
        _isActive = true;
        return this;
    }

    public ProductVariantBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public ProductVariant Build()
    {
        var variant = ProductVariant.Create(_productId, _sku, _price, _stockQuantity).Value;
        
        if (!_isActive)
        {
            variant.Deactivate();
        }

        foreach (var metadata in _metadata)
        {
            variant.UpdateMetadata(metadata.Key, metadata.Value);
        }

        return variant;
    }

    public ProductVariant BuildForProduct(Product product)
    {
        _productId = product.Id;
        return Build();
    }

    public static ProductVariantBuilder Random()
    {
        return new ProductVariantBuilder();
    }

    public static ProductVariantBuilder WithUniqueSku(string skuPrefix = "VAR")
    {
        return new ProductVariantBuilder()
            .WithSku($"{skuPrefix}-{DateTime.Now.Ticks}");
    }

    public static ProductVariantBuilder InStock(int stock = 10)
    {
        return new ProductVariantBuilder().WithStock(stock);
    }

    public static ProductVariantBuilder OutOfStockVariant()
    {
        return new ProductVariantBuilder().OutOfStock();
    }

    public static ProductVariantBuilder InactiveVariant()
    {
        return new ProductVariantBuilder().AsInactive();
    }

    public static List<ProductVariant> CreateManyForProduct(Product product, int count)
    {
        var variants = new List<ProductVariant>();
        
        for (int i = 0; i < count; i++)
        {
            var variant = Random()
                .WithSku($"VAR-{i}-{DateTime.Now.Ticks}")
                .BuildForProduct(product);
            variants.Add(variant);
        }
        
        return variants;
    }

    public static List<ProductVariant> CreateMany(int count)
    {
        var variants = new List<ProductVariant>();
        
        for (int i = 0; i < count; i++)
        {
            variants.Add(Random().Build());
        }
        
        return variants;
    }
}