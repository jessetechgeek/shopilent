using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Specifications;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Catalog.Specifications;

public class InStockProductSpecificationTests
{
    [Fact]
    public void IsSatisfiedBy_WithProductWithoutVariants_ShouldReturnTrue()
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

        var specification = new InStockProductSpecification();

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithProductWithInStockVariants_ShouldReturnTrue()
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

        var variantResult = ProductVariant.Create(product.Id, "IP-BLK-64", price, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var addVariantResult = product.AddVariant(variant);
        addVariantResult.IsSuccess.Should().BeTrue();

        var specification = new InStockProductSpecification();

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithProductWithOutOfStockVariants_ShouldReturnFalse()
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

        var variantResult = ProductVariant.CreateOutOfStock(product, "IP-BLK-64", price);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var addVariantResult = product.AddVariant(variant);
        addVariantResult.IsSuccess.Should().BeTrue();

        var specification = new InStockProductSpecification();

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithProductWithMixedStockVariants_ShouldReturnTrue()
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

        var outOfStockVariantResult = ProductVariant.CreateOutOfStock(product, "IP-BLK-64", price);
        outOfStockVariantResult.IsSuccess.Should().BeTrue();
        var outOfStockVariant = outOfStockVariantResult.Value;

        var inStockVariantResult = ProductVariant.Create(product.Id, "IP-WHT-64", price, 5);
        inStockVariantResult.IsSuccess.Should().BeTrue();
        var inStockVariant = inStockVariantResult.Value;

        product.AddVariant(outOfStockVariant);
        product.AddVariant(inStockVariant);

        var specification = new InStockProductSpecification();

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithProductWithInactiveVariants_ShouldReturnFalse()
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

        var variantResult = ProductVariant.Create(product.Id, "IP-BLK-64", price, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var deactivateResult = variant.Deactivate();
        deactivateResult.IsSuccess.Should().BeTrue();

        var addVariantResult = product.AddVariant(variant);
        addVariantResult.IsSuccess.Should().BeTrue();

        var specification = new InStockProductSpecification();

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }
}