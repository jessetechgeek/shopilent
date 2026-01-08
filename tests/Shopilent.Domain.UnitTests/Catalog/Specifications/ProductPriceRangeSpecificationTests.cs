using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Specifications;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Catalog.Specifications;

public class ProductPriceRangeSpecificationTests
{
    [Fact]
    public void IsSatisfiedBy_WithPriceInRange_ShouldReturnTrue()
    {
        // Arrange
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var priceResult = Money.FromDollars(999);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;
        
        var productResult = Product.Create("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        
        var specification = new ProductPriceRangeSpecification(500, 1000);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithPriceAtLowerBound_ShouldReturnTrue()
    {
        // Arrange
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var priceResult = Money.FromDollars(500);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;
        
        var productResult = Product.Create("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        
        var specification = new ProductPriceRangeSpecification(500, 1000);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithPriceAtUpperBound_ShouldReturnTrue()
    {
        // Arrange
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var priceResult = Money.FromDollars(1000);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;
        
        var productResult = Product.Create("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        
        var specification = new ProductPriceRangeSpecification(500, 1000);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithPriceBelowRange_ShouldReturnFalse()
    {
        // Arrange
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var priceResult = Money.FromDollars(499);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;
        
        var productResult = Product.Create("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        
        var specification = new ProductPriceRangeSpecification(500, 1000);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithPriceAboveRange_ShouldReturnFalse()
    {
        // Arrange
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var priceResult = Money.FromDollars(1001);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;
        
        var productResult = Product.Create("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        
        var specification = new ProductPriceRangeSpecification(500, 1000);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }
}