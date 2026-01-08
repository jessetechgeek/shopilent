using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Specifications;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Catalog.Specifications;

public class ActiveProductSpecificationTests
{
    [Fact]
    public void IsSatisfiedBy_WithActiveProduct_ShouldReturnTrue()
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
        
        var specification = new ActiveProductSpecification();

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithInactiveProduct_ShouldReturnFalse()
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
        
        var specification = new ActiveProductSpecification();

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }
}