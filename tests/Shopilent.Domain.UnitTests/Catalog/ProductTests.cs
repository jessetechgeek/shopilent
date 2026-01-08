using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;
using Attribute = Shopilent.Domain.Catalog.Attribute;

namespace Shopilent.Domain.Tests.Catalog;

public class ProductTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateProduct()
    {
        // Arrange
        var name = "iPhone 13";
        var slugResult = Slug.Create("iphone-13");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var basePriceResult = Money.FromDollars(999);
        basePriceResult.IsSuccess.Should().BeTrue();
        var basePrice = basePriceResult.Value;

        var sku = "IP13-64GB";

        // Act
        var result = Product.Create(name, slug, basePrice, sku);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var product = result.Value;
        product.Name.Should().Be(name);
        product.Slug.Should().Be(slug);
        product.BasePrice.Should().Be(basePrice);
        product.Sku.Should().Be(sku);
        product.IsActive.Should().BeTrue();
        product.Categories.Should().BeEmpty();
        product.Attributes.Should().BeEmpty();
        product.Variants.Should().BeEmpty();
        product.DomainEvents.Should().Contain(e => e is ProductCreatedEvent);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldReturnFailure()
    {
        // Arrange
        var name = string.Empty;
        var slugResult = Slug.Create("iphone-13");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var basePriceResult = Money.FromDollars(999);
        basePriceResult.IsSuccess.Should().BeTrue();
        var basePrice = basePriceResult.Value;

        // Act
        var result = Product.Create(name, slug, basePrice);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.NameRequired");
    }

    [Fact]
    public void Create_WithEmptySlug_ShouldReturnFailure()
    {
        // Arrange
        var name = "iPhone 13";
        var slugResult = Slug.Create(string.Empty);

        // Act & Assert
        slugResult.IsFailure.Should().BeTrue();
        slugResult.Error.Code.Should().Be("Category.SlugRequired");
    }

    [Fact]
    public void Create_WithNullBasePrice_ShouldReturnFailure()
    {
        // Arrange
        var name = "iPhone 13";
        var slugResult = Slug.Create("iphone-13");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        Money basePrice = null;

        // Act
        var result = Product.Create(name, slug, basePrice);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.NegativePrice");
    }

    [Fact]
    public void CreateWithDescription_ShouldCreateProductWithDescription()
    {
        // Arrange
        var name = "iPhone 13";
        var slugResult = Slug.Create("iphone-13");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var basePriceResult = Money.FromDollars(999);
        basePriceResult.IsSuccess.Should().BeTrue();
        var basePrice = basePriceResult.Value;

        var description = "Latest iPhone with A15 Bionic chip";

        // Act
        var result = Product.CreateWithDescription(name, slug, basePrice, description);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var product = result.Value;
        product.Name.Should().Be(name);
        product.Slug.Should().Be(slug);
        product.BasePrice.Should().Be(basePrice);
        product.Description.Should().Be(description);
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateInactive_ShouldCreateInactiveProduct()
    {
        // Arrange
        var name = "iPhone 13";
        var slugResult = Slug.Create("iphone-13");
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var basePriceResult = Money.FromDollars(999);
        basePriceResult.IsSuccess.Should().BeTrue();
        var basePrice = basePriceResult.Value;

        // Act
        var result = Product.CreateInactive(name, slug, basePrice);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var product = result.Value;
        product.Name.Should().Be(name);
        product.Slug.Should().Be(slug);
        product.BasePrice.Should().Be(basePrice);
        product.IsActive.Should().BeFalse();
        product.DomainEvents.Should().Contain(e => e is ProductCreatedEvent);
    }

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateProduct()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value,
            "IP13-64GB");
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var newName = "iPhone 13 Pro";
        var newSlugResult = Slug.Create("iphone-13-pro");
        newSlugResult.IsSuccess.Should().BeTrue();
        var newSlug = newSlugResult.Value;

        var newPriceResult = Money.FromDollars(1099);
        newPriceResult.IsSuccess.Should().BeTrue();
        var newPrice = newPriceResult.Value;

        var newDescription = "Pro model with better camera";
        var newSku = "IP13P-128GB";

        // Act
        var result = product.Update(newName, newSlug, newPrice, newDescription, newSku);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be(newName);
        product.Slug.Should().Be(newSlug);
        product.BasePrice.Should().Be(newPrice);
        product.Description.Should().Be(newDescription);
        product.Sku.Should().Be(newSku);
        product.DomainEvents.Should().Contain(e => e is ProductUpdatedEvent);
    }

    [Fact]
    public void Activate_WhenInactive_ShouldActivateProduct()
    {
        // Arrange
        var productResult = Product.CreateInactive(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        product.IsActive.Should().BeFalse();

        // Act
        var result = product.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeTrue();
        product.DomainEvents.Should().Contain(e => e is ProductStatusChangedEvent);
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateProduct()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;
        product.IsActive.Should().BeTrue();

        // Act
        var result = product.Deactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        product.DomainEvents.Should().Contain(e => e is ProductStatusChangedEvent);
    }

    [Fact]
    public void AddCategory_ShouldAddCategoryToProduct()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var categoryResult = Category.Create(
            "Smartphones",
            Slug.Create("smartphones").Value);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;

        // Act
        var result = product.AddCategory(category);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Categories.Should().HaveCount(1);
        product.Categories.First().CategoryId.Should().Be(category.Id);
        product.DomainEvents.Should().Contain(e => e is ProductCategoryAddedEvent);
    }

    [Fact]
    public void AddCategory_WhenAlreadyAdded_ShouldNotAddAgain()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var categoryResult = Category.Create(
            "Smartphones",
            Slug.Create("smartphones").Value);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;

        product.AddCategory(category);

        // Pre-check
        product.Categories.Should().HaveCount(1);

        // Act
        var result = product.AddCategory(category);

        // Assert - still only one category
        result.IsSuccess.Should().BeTrue();
        product.Categories.Should().HaveCount(1);
    }

    [Fact]
    public void AddCategory_WithNullCategory_ShouldReturnFailure()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        // Act
        var result = product.AddCategory(null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Category.NotFound");
    }

    [Fact]
    public void RemoveCategory_ShouldRemoveCategoryFromProduct()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var categoryResult = Category.Create(
            "Smartphones",
            Slug.Create("smartphones").Value);
        categoryResult.IsSuccess.Should().BeTrue();
        var category = categoryResult.Value;

        product.AddCategory(category);
        product.Categories.Should().HaveCount(1);

        // Act
        var result = product.RemoveCategory(category);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Categories.Should().BeEmpty();
        product.DomainEvents.Should().Contain(e => e is ProductCategoryRemovedEvent);
    }

    [Fact]
    public void AddAttribute_ShouldAddAttributeToProduct()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var attributeResult = Attribute.Create("Color", "Color", AttributeType.Color);
        attributeResult.IsSuccess.Should().BeTrue();
        var attribute = attributeResult.Value;

        var attributeValue = "Blue";

        // Act
        var result = product.AddAttribute(attribute, attributeValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Attributes.Should().HaveCount(1);
        product.Attributes.First().AttributeId.Should().Be(attribute.Id);
    }

    [Fact]
    public void AddVariant_ShouldAddVariantToProduct()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var variantResult = ProductVariant.Create(
            product.Id,
            "IP13-128GB",
            Money.FromDollars(1099).Value,
            100);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        // Act
        var result = product.AddVariant(variant);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Variants.Should().HaveCount(1);
        product.Variants.First().Id.Should().Be(variant.Id);
        product.DomainEvents.Should().Contain(e => e is ProductVariantAddedEvent);
    }

    [Fact]
    public void AddVariant_WithDuplicateSku_ShouldReturnFailure()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var sku = "IP13-128GB";
        var variant1Result = ProductVariant.Create(product.Id, sku, Money.FromDollars(1099).Value, 100);
        variant1Result.IsSuccess.Should().BeTrue();
        var variant1 = variant1Result.Value;

        product.AddVariant(variant1);

        var variant2Result = ProductVariant.Create(product.Id, sku, Money.FromDollars(1199).Value, 50);
        variant2Result.IsSuccess.Should().BeTrue();
        var variant2 = variant2Result.Value;

        // Act
        var result = product.AddVariant(variant2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProductVariant.DuplicateSku");
    }

    [Fact]
    public void UpdateMetadata_ShouldUpdateProductMetadata()
    {
        // Arrange
        var productResult = Product.Create(
            "iPhone 13",
            Slug.Create("iphone-13").Value,
            Money.FromDollars(999).Value);
        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var key = "weight";
        var value = "174g";

        // Act
        var result = product.UpdateMetadata(key, value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Metadata.Should().ContainKey(key);
        product.Metadata[key].Should().Be(value);
    }
}
