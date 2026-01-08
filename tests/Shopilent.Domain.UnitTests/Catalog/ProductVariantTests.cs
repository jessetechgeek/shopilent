using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;
using Attribute = Shopilent.Domain.Catalog.Attribute;

namespace Shopilent.Domain.Tests.Catalog;

public class ProductVariantTests
{
    private Product CreateTestProduct()
    {
        return Product.Create(
            "Test Product",
            Slug.Create("test-product").Value,
            Money.FromDollars(100).Value).Value;
    }

    private Attribute CreateTestAttribute(string name = "Color", AttributeType type = AttributeType.Color)
    {
        return Attribute.CreateVariant(name, name, type).Value;
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreateProductVariant()
    {
        // Arrange
        var product = CreateTestProduct();
        var sku = "TEST-123";
        var price = Money.FromDollars(150).Value;
        var stockQuantity = 100;

        // Act
        var result = ProductVariant.Create(product.Id, sku, price, stockQuantity);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var variant = result.Value;
        variant.ProductId.Should().Be(product.Id);
        variant.Sku.Should().Be(sku);
        variant.Price.Should().Be(price);
        variant.StockQuantity.Should().Be(stockQuantity);
        variant.IsActive.Should().BeTrue();
        variant.VariantAttributes.Should().BeEmpty();
        variant.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithInvalidProductId_ShouldReturnFailure()
    {
        // Arrange
        var productId = Guid.Empty;
        var sku = "TEST-123";
        var price = Money.FromDollars(150).Value;
        var stockQuantity = 100;

        // Act
        var result = ProductVariant.Create(productId, sku, price, stockQuantity);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public void Create_WithNegativeStockQuantity_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var sku = "TEST-123";
        var price = Money.FromDollars(150).Value;
        var stockQuantity = -10;

        // Act
        var result = ProductVariant.Create(product.Id, sku, price, stockQuantity);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProductVariant.NegativeStockQuantity");
    }

    [Fact]
    public void CreateInactive_ShouldCreateInactiveVariant()
    {
        // Arrange
        var product = CreateTestProduct();
        var sku = "TEST-123";
        var price = Money.FromDollars(150).Value;
        var stockQuantity = 100;

        // Act
        var result = ProductVariant.CreateInactive(product, sku, price, stockQuantity);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var variant = result.Value;
        variant.ProductId.Should().Be(product.Id);
        variant.Sku.Should().Be(sku);
        variant.Price.Should().Be(price);
        variant.StockQuantity.Should().Be(stockQuantity);
        variant.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreateOutOfStock_ShouldCreateVariantWithZeroStock()
    {
        // Arrange
        var product = CreateTestProduct();
        var sku = "TEST-123";
        var price = Money.FromDollars(150).Value;

        // Act
        var result = ProductVariant.CreateOutOfStock(product, "TEST-123", price);


        // Assert
        result.IsSuccess.Should().BeTrue();
        var variant = result.Value;
        variant.ProductId.Should().Be(product.Id);
        variant.Sku.Should().Be(sku);
        variant.Price.Should().Be(price);
        variant.StockQuantity.Should().Be(0);
        variant.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldUpdateSkuAndPrice()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "OLD-123", Money.FromDollars(100).Value, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var newSku = "NEW-456";
        var newPrice = Money.FromDollars(150).Value;

        // Act
        var result = variant.Update(newSku, newPrice);

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.Sku.Should().Be(newSku);
        variant.Price.Should().Be(newPrice);
    }

    [Fact]
    public void SetStockQuantity_WithValidQuantity_ShouldUpdateStock()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;
        variant.StockQuantity.Should().Be(10);

        var newQuantity = 50;

        // Act
        var result = variant.SetStockQuantity(newQuantity);

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.StockQuantity.Should().Be(newQuantity);
    }

    [Fact]
    public void SetStockQuantity_WithNegativeQuantity_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;
        var negativeQuantity = -5;

        // Act
        var result = variant.SetStockQuantity(negativeQuantity);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProductVariant.NegativeStockQuantity");
    }

    [Fact]
    public void AddStock_WithPositiveQuantity_ShouldIncreaseStock()
    {
        // Arrange
        var product = CreateTestProduct();
        var initialStock = 10;
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value, initialStock);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var additionalStock = 15;
        var expectedStock = initialStock + additionalStock;

        // Act
        var result = variant.AddStock(additionalStock);

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.StockQuantity.Should().Be(expectedStock);
    }

    [Fact]
    public void AddStock_WithZeroOrNegativeQuantity_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        // Act & Assert - Zero
        var zeroResult = variant.AddStock(0);
        zeroResult.IsFailure.Should().BeTrue();
        zeroResult.Error.Code.Should().Be("ProductVariant.NegativeStockQuantity");

        // Act & Assert - Negative
        var negativeResult = variant.AddStock(-5);
        negativeResult.IsFailure.Should().BeTrue();
        negativeResult.Error.Code.Should().Be("ProductVariant.NegativeStockQuantity");
    }

    [Fact]
    public void RemoveStock_WithValidQuantity_ShouldDecreaseStock()
    {
        // Arrange
        var product = CreateTestProduct();
        var initialStock = 20;
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value, initialStock);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var quantityToRemove = 5;
        var expectedStock = initialStock - quantityToRemove;

        // Act
        var result = variant.RemoveStock(quantityToRemove);

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.StockQuantity.Should().Be(expectedStock);
    }

    [Fact]
    public void RemoveStock_WithZeroOrNegativeQuantity_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value, 10);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        // Act & Assert - Zero
        var zeroResult = variant.RemoveStock(0);
        zeroResult.IsFailure.Should().BeTrue();
        zeroResult.Error.Code.Should().Be("ProductVariant.NegativeStockQuantity");

        // Act & Assert - Negative
        var negativeResult = variant.RemoveStock(-5);
        negativeResult.IsFailure.Should().BeTrue();
        negativeResult.Error.Code.Should().Be("ProductVariant.NegativeStockQuantity");
    }

    [Fact]
    public void RemoveStock_WithQuantityExceedingStock_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var initialStock = 10;
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value, initialStock);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var quantityToRemove = initialStock + 5;

        // Act
        var result = variant.RemoveStock(quantityToRemove);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProductVariant.InsufficientStock");
        variant.StockQuantity.Should().Be(initialStock); // Stock should remain unchanged
    }

    [Fact]
    public void Activate_WhenInactive_ShouldActivateVariant()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.CreateInactive(product, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;
        variant.IsActive.Should().BeFalse();

        // Act
        var result = variant.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateVariant()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;
        variant.IsActive.Should().BeTrue();

        // Act
        var result = variant.Deactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AddAttribute_ShouldAddAttributeToVariant()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var attribute = CreateTestAttribute();
        var attributeValue = "Blue";

        // Act
        var result = variant.AddAttribute(attribute, attributeValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.VariantAttributes.Should().HaveCount(1);
        variant.VariantAttributes.First().AttributeId.Should().Be(attribute.Id);
    }

    [Fact]
    public void AddAttribute_WithNonVariantAttribute_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var attributeResult = Attribute.Create("Weight", "Weight", AttributeType.Number);
        attributeResult.IsSuccess.Should().BeTrue();
        var attribute = attributeResult.Value; // Not a variant attribute

        var attributeValue = 500;

        // Act
        var result = variant.AddAttribute(attribute, attributeValue);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProductVariant.NonVariantAttribute");
    }

    [Fact]
    public void AddAttribute_WithNullAttribute_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        Attribute attribute = null;
        var attributeValue = "Blue";

        // Act
        var result = variant.AddAttribute(attribute, attributeValue);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Attribute.NotFound");
    }

    [Fact]
    public void UpdateMetadata_ShouldAddOrUpdateMetadata()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var key = "dimension";
        var value = "10x15x5 cm";

        // Act
        var result = variant.UpdateMetadata(key, value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        variant.Metadata.Should().ContainKey(key);
        variant.Metadata[key].Should().Be(value);
    }

    [Fact]
    public void UpdateMetadata_WithEmptyKey_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var emptyKey = string.Empty;
        var value = "test";

        // Act
        var result = variant.UpdateMetadata(emptyKey, value);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProductVariant.InvalidMetadataKey");
    }

    [Fact]
    public void HasAttribute_WithExistingAttributeId_ShouldReturnTrue()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var attribute = CreateTestAttribute();
        var attributeValue = "Blue";

        var addResult = variant.AddAttribute(attribute, attributeValue);
        addResult.IsSuccess.Should().BeTrue();

        // Act
        var result = variant.HasAttribute(attribute.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasAttribute_WithNonExistingAttributeId_ShouldReturnFalse()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var nonExistingAttributeId = Guid.NewGuid();

        // Act
        var result = variant.HasAttribute(nonExistingAttributeId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetAttributeValue_WithExistingAttribute_ShouldReturnValue()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var attribute = CreateTestAttribute();
        var attributeValue = "Blue";

        var addResult = variant.AddAttribute(attribute, attributeValue);
        addResult.IsSuccess.Should().BeTrue();

        // Act
        var result = variant.GetAttributeValue(attribute.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(attributeValue);
    }

    [Fact]
    public void GetAttributeValue_WithNonExistingAttribute_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var nonExistingAttributeId = Guid.NewGuid();

        // Act
        var result = variant.GetAttributeValue(nonExistingAttributeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Attribute.NotFound");
    }

    [Fact]
    public void UpdateAttributeValue_WithExistingAttribute_ShouldUpdateValue()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var attribute = CreateTestAttribute();
        var initialValue = "Blue";
        var newValue = "Red";

        var addResult = variant.AddAttribute(attribute, initialValue);
        addResult.IsSuccess.Should().BeTrue();

        // Act
        var updateResult = variant.UpdateAttributeValue(attribute.Id, newValue);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();

        var getValueResult = variant.GetAttributeValue(attribute.Id);
        getValueResult.IsSuccess.Should().BeTrue();
        getValueResult.Value.Should().Be(newValue);
    }

    [Fact]
    public void UpdateAttributeValue_WithNonExistingAttribute_ShouldReturnFailure()
    {
        // Arrange
        var product = CreateTestProduct();
        var variantResult = ProductVariant.Create(product.Id, "TEST-123", Money.FromDollars(100).Value);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var nonExistingAttributeId = Guid.NewGuid();
        var newValue = "Red";

        // Act
        var result = variant.UpdateAttributeValue(nonExistingAttributeId, newValue);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Attribute.NotFound");
    }
}
