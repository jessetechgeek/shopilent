using Bogus;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class ProductBuilder
{
    private string _name;
    private string _description;
    private Money _price;
    private Slug _slug;
    private bool _isActive;
    private bool _slugExplicitlySet;
    private readonly List<int> _categoryIds;
    private readonly List<ProductImage> _images;

    public ProductBuilder()
    {
        var faker = new Faker();
        _name = faker.Commerce.ProductName();
        _description = faker.Lorem.Paragraph();
        _price = Money.Create(Math.Round(faker.Random.Decimal(10, 1000), 2), "USD").Value;
        _slug = Slug.Create(_name.ToLower().Replace(" ", "-")).Value;
        _isActive = true;
        _slugExplicitlySet = false;
        _categoryIds = new List<int>();
        _images = new List<ProductImage>();
    }

    public ProductBuilder WithName(string name)
    {
        _name = name;
        if (!_slugExplicitlySet)
        {
            _slug = Slug.Create(_name.ToLower().Replace(" ", "-")).Value;
        }
        return this;
    }

    public ProductBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ProductBuilder WithPrice(decimal price, string currency = "USD")
    {
        _price = Money.Create(Math.Round(price, 2), currency).Value;
        return this;
    }

    public ProductBuilder WithSlug(string slug)
    {
        _slug = Slug.Create(slug).Value;
        _slugExplicitlySet = true;
        return this;
    }

    public ProductBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public ProductBuilder AsActive()
    {
        _isActive = true;
        return this;
    }

    public ProductBuilder InCategory(int categoryId)
    {
        _categoryIds.Add(categoryId);
        return this;
    }

    public ProductBuilder InCategories(params int[] categoryIds)
    {
        _categoryIds.AddRange(categoryIds);
        return this;
    }

    public ProductBuilder WithCategory(Category category)
    {
        // Note: The Domain Product doesn't actually manage category relationships directly
        // Category relationships are managed via ProductCategory entity in the database
        // This method is kept for fluent API compatibility but doesn't affect the product creation
        return this;
    }

    public ProductBuilder WithImage(string imageKey, string thumbnailKey, string? altText = null, bool isDefault = false, int displayOrder = 1)
    {
        var image = ProductImage.Create(imageKey, thumbnailKey, altText, isDefault, displayOrder).Value;
        _images.Add(image);
        return this;
    }

    public Product Build()
    {
        var product = Product.CreateWithDescription(_name, _slug, _price, _description).Value;
        
        if (!_isActive)
        {
            product.Deactivate();
        }

        return product;
    }

    public static ProductBuilder Random()
    {
        return new ProductBuilder();
    }

    public static List<Product> CreateMany(int count)
    {
        var products = new List<Product>();
        
        for (int i = 0; i < count; i++)
        {
            var faker = new Faker();
            var uniqueName = $"{faker.Commerce.ProductName()} {i + 1}";
            products.Add(Random().WithName(uniqueName).Build());
        }
        
        return products;
    }
}