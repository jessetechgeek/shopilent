using FluentAssertions;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Domain.Tests.Sales;

public class CartTests
{
    private User CreateTestUser()
    {
        var emailResult = Email.Create("test@example.com");
        var fullNameResult = FullName.Create("Test", "User");
        var userResult = User.Create(
            emailResult.Value,
            "hashed_password",
            fullNameResult.Value);

        userResult.IsSuccess.Should().BeTrue();
        return userResult.Value;
    }

    private Product CreateTestProduct(string name = "Test Product", decimal price = 100M)
    {
        var slugResult = Slug.Create(name.ToLower().Replace(" ", "-"));
        slugResult.IsSuccess.Should().BeTrue();

        var priceResult = Money.FromDollars(price);
        priceResult.IsSuccess.Should().BeTrue();

        var productResult = Product.Create(
            name,
            slugResult.Value,
            priceResult.Value);

        productResult.IsSuccess.Should().BeTrue();
        return productResult.Value;
    }

    [Fact]
    public void Create_WithoutUser_ShouldCreateEmptyCart()
    {
        // Act
        var cartResult = Cart.Create();

        // Assert
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        cart.UserId.Should().BeNull();
        cart.Items.Should().BeEmpty();
        cart.Metadata.Should().BeEmpty();
        cart.DomainEvents.Should().ContainSingle(e => e is CartCreatedEvent);
    }

    [Fact]
    public void Create_WithUser_ShouldCreateEmptyCartForUser()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cartResult = Cart.Create(user.Id);

        // Assert
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        cart.UserId.Should().Be(user.Id);
        cart.Items.Should().BeEmpty();
        cart.Metadata.Should().BeEmpty();
        cart.DomainEvents.Should().ContainSingle(e => e is CartCreatedEvent);
    }

    [Fact]
    public void CreateWithMetadata_ShouldCreateCartWithMetadata()
    {
        // Arrange
        var user = CreateTestUser();
        var metadata = new Dictionary<string, object>
        {
            { "source", "mobile_app" },
            { "version", "1.0" }
        };

        // Act
        var cartResult = Cart.CreateWithMetadata(user.Id, metadata);

        // Assert
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        cart.UserId.Should().Be(user.Id);
        cart.Items.Should().BeEmpty();
        cart.Metadata.Should().HaveCount(2);
        cart.Metadata["source"].Should().Be("mobile_app");
        cart.Metadata["version"].Should().Be("1.0");
        cart.DomainEvents.Should().ContainSingle(e => e is CartCreatedEvent);
    }

    [Fact]
    public void AssignToUser_ShouldAssignCartToUser()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        cart.UserId.Should().BeNull();

        var user = CreateTestUser();

        // Act
        var assignResult = cart.AssignToUser(user.Id);

        // Assert
        assignResult.IsSuccess.Should().BeTrue();
        cart.UserId.Should().Be(user.Id);
        cart.DomainEvents.Should().ContainSingle(e => e is CartAssignedToUserEvent);
    }

    [Fact]
    public void AssignToUser_WithNullUser_ShouldReturnFailure()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        var userId = Guid.Empty;

        // Act
        var assignResult = cart.AssignToUser(userId);

        // Assert
        assignResult.IsFailure.Should().BeTrue();
        assignResult.Error.Code.Should().Be("Cart.InvalidUserId");
    }

    [Fact]
    public void AddItem_NewProduct_ShouldAddItemToCart()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var product = CreateTestProduct();
        var quantity = 2;

        // Act
        var cartItemResult = cart.AddItem(product.Id, quantity);

        // Assert
        cartItemResult.IsSuccess.Should().BeTrue();
        var cartItem = cartItemResult.Value;
        cart.Items.Should().HaveCount(1);
        cartItem.ProductId.Should().Be(product.Id);
        cartItem.Quantity.Should().Be(quantity);
        cartItem.VariantId.Should().BeNull();
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemAddedEvent);
    }

    [Fact]
    public void AddItem_ExistingProduct_ShouldIncreaseQuantity()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var product = CreateTestProduct();

        // Add first item
        var initialQuantity = 2;
        var cartItemResult = cart.AddItem(product.Id, initialQuantity);
        cartItemResult.IsSuccess.Should().BeTrue();
        var cartItem = cartItemResult.Value;
        cartItem.Quantity.Should().Be(initialQuantity);

        // Add same product again
        var additionalQuantity = 3;

        // Act
        var updatedItemResult = cart.AddItem(product.Id, additionalQuantity);

        // Assert
        updatedItemResult.IsSuccess.Should().BeTrue();
        var updatedItem = updatedItemResult.Value;
        cart.Items.Should().HaveCount(1);
        updatedItem.Id.Should().Be(cartItem.Id);
        updatedItem.Quantity.Should().Be(initialQuantity + additionalQuantity);
    }

    [Fact]
    public void AddItem_WithProductVariant_ShouldAddItemWithVariant()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var product = CreateTestProduct();

        var priceResult = Money.FromDollars(150);
        priceResult.IsSuccess.Should().BeTrue();

        var variantResult = ProductVariant.Create(product.Id, "VAR-123", priceResult.Value, 100);
        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var quantity = 1;

        // Act
        var cartItemResult = cart.AddItem(product.Id, quantity, variant?.Id);

        // Assert
        cartItemResult.IsSuccess.Should().BeTrue();
        var cartItem = cartItemResult.Value;
        cart.Items.Should().HaveCount(1);
        cartItem.ProductId.Should().Be(product.Id);
        cartItem.VariantId.Should().Be(variant.Id);
        cartItem.Quantity.Should().Be(quantity);
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemAddedEvent);
    }

    [Fact]
    public void AddItem_WithNullProduct_ShouldReturnFailure()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var productId = Guid.Empty;
        var quantity = 1;

        // Act
        var cartItemResult = cart.AddItem(productId, quantity);

        // Assert
        cartItemResult.IsFailure.Should().BeTrue();
        cartItemResult.Error.Code.Should().Be("Cart.InvalidProductId");
    }

    [Fact]
    public void AddItem_WithZeroQuantity_ShouldReturnFailure()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var product = CreateTestProduct();
        var quantity = 0;

        // Act
        var cartItemResult = cart.AddItem(product.Id, quantity);

        // Assert
        cartItemResult.IsFailure.Should().BeTrue();
        cartItemResult.Error.Code.Should().Be("Cart.InvalidQuantity");
    }

    [Fact]
    public void UpdateItemQuantity_ShouldUpdateItemQuantity()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var product = CreateTestProduct();
        var initialQuantity = 1;
        var cartItemResult = cart.AddItem(product.Id, initialQuantity);
        cartItemResult.IsSuccess.Should().BeTrue();
        var cartItem = cartItemResult.Value;

        var newQuantity = 5;

        // Act
        var updateResult = cart.UpdateItemQuantity(cartItem.Id, newQuantity);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        cart.Items.Should().HaveCount(1);
        cart.Items.First().Quantity.Should().Be(newQuantity);
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemUpdatedEvent);
    }

    [Fact]
    public void UpdateItemQuantity_WithZeroOrLessQuantity_ShouldRemoveItem()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var product = CreateTestProduct();
        var cartItemResult = cart.AddItem(product.Id, 2);
        cartItemResult.IsSuccess.Should().BeTrue();
        cart.Items.Should().HaveCount(1);

        // Act
        var updateResult = cart.UpdateItemQuantity(cartItemResult.Value.Id, 0);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        cart.Items.Should().BeEmpty();
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemRemovedEvent);
    }

    [Fact]
    public void UpdateItemQuantity_WithInvalidItemId_ShouldReturnFailure()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var invalidItemId = Guid.NewGuid();
        var quantity = 5;

        // Act
        var updateResult = cart.UpdateItemQuantity(invalidItemId, quantity);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Cart.ItemNotFound");
    }

    [Fact]
    public void RemoveItem_ShouldRemoveItemFromCart()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var product = CreateTestProduct();
        var cartItemResult = cart.AddItem(product.Id, 1);
        cartItemResult.IsSuccess.Should().BeTrue();
        cart.Items.Should().HaveCount(1);

        // Act
        var removeResult = cart.RemoveItem(cartItemResult.Value.Id);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        cart.Items.Should().BeEmpty();
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemRemovedEvent);
    }

    [Fact]
    public void Clear_ShouldRemoveAllItemsFromCart()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        cart.AddItem(CreateTestProduct("Product 1").Id, 1);
        cart.AddItem(CreateTestProduct("Product 2").Id, 2);
        cart.AddItem(CreateTestProduct("Product 3").Id, 3);
        cart.Items.Should().HaveCount(3);

        // Act
        var clearResult = cart.Clear();

        // Assert
        clearResult.IsSuccess.Should().BeTrue();
        cart.Items.Should().BeEmpty();
        cart.DomainEvents.Should().ContainSingle(e => e is CartClearedEvent);
    }

    [Fact]
    public void UpdateMetadata_ShouldAddOrUpdateMetadata()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var key = "campaign";
        var value = "summer_sale";

        // Act
        var updateResult = cart.UpdateMetadata(key, value);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        cart.Metadata.Should().HaveCount(1);
        cart.Metadata[key].Should().Be(value);
    }

    [Fact]
    public void UpdateMetadata_WithEmptyKey_ShouldReturnFailure()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;

        var key = string.Empty;
        var value = "test";

        // Act
        var updateResult = cart.UpdateMetadata(key, value);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Cart.InvalidMetadataKey");
    }
}
