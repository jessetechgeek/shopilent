using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Specifications;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Catalog.Specifications;

public class ProductInCategorySpecificationTests
{
    [Fact]
    public void IsSatisfiedBy_WithProductInCategory_ShouldReturnTrue()
    {
        // Arrange
        var categorySlugResult = Slug.Create("electronics");
        categorySlugResult.IsSuccess.Should().BeTrue();
        var categorySlug = categorySlugResult.Value;

        var categoryResult = Category.Create("Electronics", categorySlug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;

        var productSlugResult = Slug.Create("iphone");
        productSlugResult.IsSuccess.Should().BeTrue();
        var productSlug = productSlugResult.Value;

        var priceResult = Money.FromDollars(999);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;

        var productResult = Product.Create("iPhone", productSlug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var addCategoryResult = product.AddCategory(category.Id);
        addCategoryResult.IsSuccess.Should().BeTrue();

        var specification = new ProductInCategorySpecification(category.Id);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithProductNotInCategory_ShouldReturnFalse()
    {
        // Arrange
        var categorySlugResult = Slug.Create("electronics");
        categorySlugResult.IsSuccess.Should().BeTrue();
        var categorySlug = categorySlugResult.Value;

        var categoryResult = Category.Create("Electronics", categorySlug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;

        var otherCategorySlugResult = Slug.Create("phones");
        otherCategorySlugResult.IsSuccess.Should().BeTrue();
        var otherCategorySlug = otherCategorySlugResult.Value;

        var otherCategoryResult = Category.Create("Phones", otherCategorySlug);
        otherCategoryResult.IsSuccess.Should().BeTrue();
        var otherCategory = otherCategoryResult.Value;

        var productSlugResult = Slug.Create("iphone");
        productSlugResult.IsSuccess.Should().BeTrue();
        var productSlug = productSlugResult.Value;

        var priceResult = Money.FromDollars(999);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;

        var productResult = Product.Create("iPhone", productSlug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var addCategoryResult = product.AddCategory(otherCategory.Id);
        addCategoryResult.IsSuccess.Should().BeTrue();

        var specification = new ProductInCategorySpecification(category.Id);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithProductInMultipleCategories_ShouldReturnTrue()
    {
        // Arrange
        var category1SlugResult = Slug.Create("electronics");
        category1SlugResult.IsSuccess.Should().BeTrue();
        var category1Slug = category1SlugResult.Value;

        var category1Result = Category.Create("Electronics", category1Slug);
        category1Result.IsSuccess.Should().BeTrue();
        var category1 = category1Result.Value;

        var category2SlugResult = Slug.Create("phones");
        category2SlugResult.IsSuccess.Should().BeTrue();
        var category2Slug = category2SlugResult.Value;

        var category2Result = Category.Create("Phones", category2Slug);
        category2Result.IsSuccess.Should().BeTrue();
        var category2 = category2Result.Value;

        var productSlugResult = Slug.Create("iphone");
        productSlugResult.IsSuccess.Should().BeTrue();
        var productSlug = productSlugResult.Value;

        var priceResult = Money.FromDollars(999);
        priceResult.IsSuccess.Should().BeTrue();
        var price = priceResult.Value;

        var productResult = Product.Create("iPhone", productSlug, price);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        product.AddCategory(category1.Id);
        product.AddCategory(category2.Id);

        var specification = new ProductInCategorySpecification(category1.Id);

        // Act
        var result = specification.IsSatisfiedBy(product);

        // Assert
        result.Should().BeTrue();
    }
}
