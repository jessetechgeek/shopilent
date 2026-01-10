using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Domain.Tests.Catalog.Events;

public class CategoryEventTests
{
    [Fact]
    public void Category_WhenCreated_ShouldRaiseCategoryCreatedEvent()
    {
        // Arrange & Act
        var slugResult = Slug.Create("electronics");
        slugResult.IsSuccess.Should().BeTrue();
        
        var result = Category.Create("Electronics", slugResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var category = result.Value;
        var domainEvent = category.DomainEvents.Should().ContainSingle(e => e is CategoryCreatedEvent).Subject;
        var createdEvent = (CategoryCreatedEvent)domainEvent;
        createdEvent.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public void Category_WhenUpdated_ShouldRaiseCategoryUpdatedEvent()
    {
        // Arrange
        var slugResult = Slug.Create("electronics");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var categoryResult = Category.Create("Electronics", slug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;
        
        category.ClearDomainEvents(); // Clear the creation event

        var newSlugResult = Slug.Create("updated-electronics");
        newSlugResult.IsSuccess.Should().BeTrue();
        var newSlug = newSlugResult.Value;

        // Act
        var updateResult = category.Update("Updated Electronics", newSlug, "Updated description");
        updateResult.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = category.DomainEvents.Should().ContainSingle(e => e is CategoryUpdatedEvent).Subject;
        var updatedEvent = (CategoryUpdatedEvent)domainEvent;
        updatedEvent.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public void Category_WhenActivated_ShouldRaiseCategoryStatusChangedEvent()
    {
        // Arrange
        var slugResult = Slug.Create("electronics");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var categoryResult = Category.CreateInactive("Electronics", slug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;
        
        category.ClearDomainEvents(); // Clear the creation event

        // Act
        var result = category.Activate();
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = category.DomainEvents.Should().ContainSingle(e => e is CategoryStatusChangedEvent).Subject;
        var statusEvent = (CategoryStatusChangedEvent)domainEvent;
        statusEvent.CategoryId.Should().Be(category.Id);
        statusEvent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Category_WhenDeactivated_ShouldRaiseCategoryStatusChangedEvent()
    {
        // Arrange
        var slugResult = Slug.Create("electronics");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;
        
        var categoryResult = Category.Create("Electronics", slug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;
        
        category.ClearDomainEvents(); // Clear the creation event

        // Act
        var result = category.Deactivate();
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = category.DomainEvents.Should().ContainSingle(e => e is CategoryStatusChangedEvent).Subject;
        var statusEvent = (CategoryStatusChangedEvent)domainEvent;
        statusEvent.CategoryId.Should().Be(category.Id);
        statusEvent.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Category_WhenParentIsSet_ShouldRaiseCategoryHierarchyChangedEvent()
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
        
        child.ClearDomainEvents(); // Clear the creation event

        // Act
        var result = child.SetParent(parent.Id);
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = child.DomainEvents.Should().ContainSingle(e => e is CategoryHierarchyChangedEvent).Subject;
        var hierarchyEvent = (CategoryHierarchyChangedEvent)domainEvent;
        hierarchyEvent.CategoryId.Should().Be(child.Id);
    }
}