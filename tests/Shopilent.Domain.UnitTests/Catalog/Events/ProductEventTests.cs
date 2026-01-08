using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Catalog.Events;

public class ProductEventTests
{
    [Fact]
    public void Product_WhenCreated_ShouldRaiseProductCreatedEvent()
    {
        // Arrange & Act
        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var moneyResult = Money.FromDollars(100);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var productResult = Product.Create("Test Product", slug, money);

        // Assert
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        var domainEvent = product.DomainEvents.Should().ContainSingle(e => e is ProductCreatedEvent).Subject;
        var createdEvent = (ProductCreatedEvent)domainEvent;
        createdEvent.ProductId.Should().Be(product.Id);
    }

    [Fact]
    public void Product_WhenUpdated_ShouldRaiseProductUpdatedEvent()
    {
        // Arrange
        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var moneyResult = Money.FromDollars(100);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var productResult = Product.Create("Test Product", slug, money);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        product.ClearDomainEvents(); // Clear the creation event

        var newSlugResult = Slug.Create("updated-product");
        newSlugResult.IsSuccess.Should().BeTrue();
        var newSlug = newSlugResult.Value;

        var newPriceResult = Money.FromDollars(120);
        newPriceResult.IsSuccess.Should().BeTrue();
        var newPrice = newPriceResult.Value;

        // Act
        var updateResult = product.Update(
            "Updated Product",
            newSlug,
            newPrice,
            "Updated description");
        updateResult.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = product.DomainEvents.Should().ContainSingle(e => e is ProductUpdatedEvent).Subject;
        var updatedEvent = (ProductUpdatedEvent)domainEvent;
        updatedEvent.ProductId.Should().Be(product.Id);
    }

    [Fact]
    public void Product_WhenActivated_ShouldRaiseProductStatusChangedEvent()
    {
        // Arrange
        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var moneyResult = Money.FromDollars(100);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var productResult = Product.CreateInactive("Test Product", slug, money);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        product.ClearDomainEvents(); // Clear the creation event

        // Act
        var result = product.Activate();
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = product.DomainEvents.Should().ContainSingle(e => e is ProductStatusChangedEvent).Subject;
        var statusEvent = (ProductStatusChangedEvent)domainEvent;
        statusEvent.ProductId.Should().Be(product.Id);
        statusEvent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Product_WhenDeactivated_ShouldRaiseProductStatusChangedEvent()
    {
        // Arrange
        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var moneyResult = Money.FromDollars(100);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var productResult = Product.Create("Test Product", slug, money);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        product.ClearDomainEvents(); // Clear the creation event

        // Act
        var result = product.Deactivate();
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = product.DomainEvents.Should().ContainSingle(e => e is ProductStatusChangedEvent).Subject;
        var statusEvent = (ProductStatusChangedEvent)domainEvent;
        statusEvent.ProductId.Should().Be(product.Id);
        statusEvent.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Product_WhenCategoryAdded_ShouldRaiseProductCategoryAddedEvent()
    {
        // Arrange
        var productSlugResult = Slug.Create("test-product");
        productSlugResult.IsSuccess.Should().BeTrue();
        var productSlug = productSlugResult.Value;

        var moneyResult = Money.FromDollars(100);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var productResult = Product.Create("Test Product", productSlug, money);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var categorySlugResult = Slug.Create("test-category");
        categorySlugResult.IsSuccess.Should().BeTrue();
        var categorySlug = categorySlugResult.Value;

        var categoryResult = Category.Create("Test Category", categorySlug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;

        product.ClearDomainEvents(); // Clear the creation event

        // Act
        var result = product.AddCategory(category);
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = product.DomainEvents.Should().ContainSingle(e => e is ProductCategoryAddedEvent).Subject;
        var categoryEvent = (ProductCategoryAddedEvent)domainEvent;
        categoryEvent.ProductId.Should().Be(product.Id);
        categoryEvent.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public void Product_WhenCategoryRemoved_ShouldRaiseProductCategoryRemovedEvent()
    {
        // Arrange
        var productSlugResult = Slug.Create("test-product");
        productSlugResult.IsSuccess.Should().BeTrue();
        var productSlug = productSlugResult.Value;

        var moneyResult = Money.FromDollars(100);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var productResult = Product.Create("Test Product", productSlug, money);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var categorySlugResult = Slug.Create("test-category");
        categorySlugResult.IsSuccess.Should().BeTrue();
        var categorySlug = categorySlugResult.Value;

        var categoryResult = Category.Create("Test Category", categorySlug);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;

        var addCategoryResult = product.AddCategory(category);
        addCategoryResult.IsSuccess.Should().BeTrue();

        product.ClearDomainEvents(); // Clear previous events

        // Act
        var result = product.RemoveCategory(category);
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = product.DomainEvents.Should().ContainSingle(e => e is ProductCategoryRemovedEvent).Subject;
        var categoryEvent = (ProductCategoryRemovedEvent)domainEvent;
        categoryEvent.ProductId.Should().Be(product.Id);
        categoryEvent.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public void Product_WhenVariantAdded_ShouldRaiseProductVariantAddedEvent()
    {
        // Arrange
        var productSlugResult = Slug.Create("test-product");
        productSlugResult.IsSuccess.Should().BeTrue();
        var productSlug = productSlugResult.Value;

        var moneyResult = Money.FromDollars(100);
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var productResult = Product.Create("Test Product", productSlug, money);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var variantPriceResult = Money.FromDollars(120);
        variantPriceResult.IsSuccess.Should().BeTrue();
        var variantPrice = variantPriceResult.Value;

        var variantResult = ProductVariant.Create(product.Id, "V1", variantPrice, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        product.ClearDomainEvents(); // Clear the creation event

        // Act
        var result = product.AddVariant(variant);
        result.IsSuccess.Should().BeTrue();

        // Assert
        var domainEvent = product.DomainEvents.Should().ContainSingle(e => e is ProductVariantAddedEvent).Subject;
        var variantEvent = (ProductVariantAddedEvent)domainEvent;
        variantEvent.ProductId.Should().Be(product.Id);
        variantEvent.VariantId.Should().Be(variant.Id);
    }
}