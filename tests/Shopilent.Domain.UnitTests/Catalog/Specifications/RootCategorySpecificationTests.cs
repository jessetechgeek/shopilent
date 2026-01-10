using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Specifications;
using Shopilent.Domain.Catalog.ValueObjects;

namespace Shopilent.Domain.Tests.Catalog.Specifications;

public class RootCategorySpecificationTests
{
    [Fact]
    public void IsSatisfiedBy_WithRootCategory_ShouldReturnTrue()
    {
        // Arrange
        var slugResult = Slug.Create("electronics");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var categoryResult = Category.Create("Electronics", slug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;
        
        var specification = new RootCategorySpecification();

        // Act
        var result = specification.IsSatisfiedBy(category);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithChildCategory_ShouldReturnFalse()
    {
        // Arrange
        var parentSlugResult = Slug.Create("electronics");
        parentSlugResult.IsSuccess.Should().BeTrue();
        var parentSlug = parentSlugResult.Value;

        var parentResult = Category.Create("Electronics", parentSlug);
        parentResult.IsSuccess.Should().BeTrue();
        var parent = parentResult.Value;

        var childSlugResult = Slug.Create("phones");
        childSlugResult.IsSuccess.Should().BeTrue();
        var childSlug = childSlugResult.Value;

        var childResult = Category.Create("Phones", childSlug);
        childResult.IsSuccess.Should().BeTrue();
        var child = childResult.Value;

        // Set hierarchy to make it a child category
        child.SetHierarchy(parent.Id, parent.Level + 1, $"{parent.Path}/{childSlug}");

        var specification = new RootCategorySpecification();

        // Act
        var result = specification.IsSatisfiedBy(child);

        // Assert
        result.Should().BeFalse();
    }
}