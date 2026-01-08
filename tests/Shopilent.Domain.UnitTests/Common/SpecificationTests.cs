using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Specifications;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Common;

public class SpecificationTests
{
    [Fact]
    public void AndSpecification_WithBothSatisfied_ShouldReturnTrue()
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

        var activeSpec = new ActiveProductSpecification();
        var priceSpec = new ProductPriceRangeSpecification(500, 1000);

        var andSpec = activeSpec.And(priceSpec);

        // Act
        var result = andSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AndSpecification_WithOneSatisfied_ShouldReturnFalse()
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

        var activeSpec = new ActiveProductSpecification();
        var priceSpec = new ProductPriceRangeSpecification(100, 500);

        var andSpec = activeSpec.And(priceSpec);

        // Act
        var result = andSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void OrSpecification_WithBothSatisfied_ShouldReturnTrue()
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

        var activeSpec = new ActiveProductSpecification();
        var priceSpec = new ProductPriceRangeSpecification(500, 1000);

        var orSpec = activeSpec.Or(priceSpec);

        // Act
        var result = orSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OrSpecification_WithOneSatisfied_ShouldReturnTrue()
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

        var activeSpec = new ActiveProductSpecification();
        var priceSpec = new ProductPriceRangeSpecification(100, 500);

        var orSpec = activeSpec.Or(priceSpec);

        // Act
        var result = orSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OrSpecification_WithNeitherSatisfied_ShouldReturnFalse()
    {
        // Arrange
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var priceResult = Money.FromDollars(999);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;

        var productResult = Product.CreateInactive("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var activeSpec = new ActiveProductSpecification();
        var priceSpec = new ProductPriceRangeSpecification(100, 500);

        var orSpec = activeSpec.Or(priceSpec);

        // Act
        var result = orSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void NotSpecification_WithSatisfied_ShouldReturnFalse()
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

        var activeSpec = new ActiveProductSpecification();
        var notSpec = activeSpec.Not();

        // Act
        var result = notSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void NotSpecification_WithNotSatisfied_ShouldReturnTrue()
    {
        // Arrange
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var priceResult = Money.FromDollars(999);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;

        var productResult = Product.CreateInactive("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var activeSpec = new ActiveProductSpecification();
        var notSpec = activeSpec.Not();

        // Act
        var result = notSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ComplexSpecification_ShouldEvaluateCorrectly()
    {
        // Arrange - (Active OR ExpensivePrice) AND NOT InStockProduct
        var slugResult = Slug.Create("iphone");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var priceResult = Money.FromDollars(999);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;

        var productResult = Product.Create("iPhone", slug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var variantResult = ProductVariant.CreateOutOfStock(product, "IP-BLK-128", price);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var addVariantResult = product.AddVariant(variant);
        addVariantResult.IsSuccess.Should().BeTrue();

        var activeSpec = new ActiveProductSpecification();
        var priceSpec = new ProductPriceRangeSpecification(1500, 2000);
        var stockSpec = new InStockProductSpecification();

        var complexSpec = activeSpec.Or(priceSpec).And(stockSpec.Not());

        // Act
        var result = complexSpec.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue(); // Active but not in stock, should match
    }
}