using FluentAssertions;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Sales.Events;

public class CartEventTests
{
    private User CreateTestUser()
    {
        var emailResult = Email.Create("test@example.com");
        emailResult.IsSuccess.Should().BeTrue();
        
        var fullNameResult = FullName.Create("Test", "User");
        fullNameResult.IsSuccess.Should().BeTrue();
        
        var userResult = User.Create(
            emailResult.Value,
            "hashed_password",
            fullNameResult.Value);
            
        userResult.IsSuccess.Should().BeTrue();
        return userResult.Value;
    }

    private Product CreateTestProduct()
    {
        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();
        
        var priceResult = Money.FromDollars(100);
        priceResult.IsSuccess.Should().BeTrue();
        
        var productResult = Product.Create(
            "Test Product",
            slugResult.Value,
            priceResult.Value);
            
        productResult.IsSuccess.Should().BeTrue();
        return productResult.Value;
    }

    [Fact]
    public void Cart_WhenCreated_ShouldRaiseCartCreatedEvent()
    {
        // Act
        var cartResult = Cart.Create();

        // Assert
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        cart.DomainEvents.Should().ContainSingle(e => e is CartCreatedEvent);
        var domainEvent = cart.DomainEvents.First(e => e is CartCreatedEvent);
        var createdEvent = (CartCreatedEvent)domainEvent;
        createdEvent.CartId.Should().Be(cart.Id);
    }

    [Fact]
    public void Cart_WhenAssignedToUser_ShouldRaiseCartAssignedToUserEvent()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        
        var user = CreateTestUser();
        cart.ClearDomainEvents(); // Clear the creation event

        // Act
        var assignResult = cart.AssignToUser(user);

        // Assert
        assignResult.IsSuccess.Should().BeTrue();
        cart.DomainEvents.Should().ContainSingle(e => e is CartAssignedToUserEvent);
        var domainEvent = cart.DomainEvents.First(e => e is CartAssignedToUserEvent);
        var assignedEvent = (CartAssignedToUserEvent)domainEvent;
        assignedEvent.CartId.Should().Be(cart.Id);
        assignedEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Cart_WhenItemAdded_ShouldRaiseCartItemAddedEvent()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        
        var product = CreateTestProduct();
        cart.ClearDomainEvents(); // Clear the creation event

        // Act
        var cartItemResult = cart.AddItem(product, 2);

        // Assert
        cartItemResult.IsSuccess.Should().BeTrue();
        var cartItem = cartItemResult.Value;
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemAddedEvent);
        var domainEvent = cart.DomainEvents.First(e => e is CartItemAddedEvent);
        var addedEvent = (CartItemAddedEvent)domainEvent;
        addedEvent.CartId.Should().Be(cart.Id);
        addedEvent.ItemId.Should().Be(cartItem.Id);
    }

    [Fact]
    public void Cart_WhenItemUpdated_ShouldRaiseCartItemUpdatedEvent()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        
        var product = CreateTestProduct();
        var cartItemResult = cart.AddItem(product, 1);
        cartItemResult.IsSuccess.Should().BeTrue();
        var cartItem = cartItemResult.Value;
        
        cart.ClearDomainEvents(); // Clear previous events

        // Act
        var updateResult = cart.UpdateItemQuantity(cartItem.Id, 3);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemUpdatedEvent);
        var domainEvent = cart.DomainEvents.First(e => e is CartItemUpdatedEvent);
        var updatedEvent = (CartItemUpdatedEvent)domainEvent;
        updatedEvent.CartId.Should().Be(cart.Id);
        updatedEvent.ItemId.Should().Be(cartItem.Id);
    }

    [Fact]
    public void Cart_WhenItemRemoved_ShouldRaiseCartItemRemovedEvent()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        
        var product = CreateTestProduct();
        var cartItemResult = cart.AddItem(product, 1);
        cartItemResult.IsSuccess.Should().BeTrue();
        var cartItem = cartItemResult.Value;
        
        cart.ClearDomainEvents(); // Clear previous events

        // Act
        var removeResult = cart.RemoveItem(cartItem.Id);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        cart.DomainEvents.Should().ContainSingle(e => e is CartItemRemovedEvent);
        var domainEvent = cart.DomainEvents.First(e => e is CartItemRemovedEvent);
        var removedEvent = (CartItemRemovedEvent)domainEvent;
        removedEvent.CartId.Should().Be(cart.Id);
        removedEvent.ItemId.Should().Be(cartItem.Id);
    }

    [Fact]
    public void Cart_WhenCleared_ShouldRaiseCartClearedEvent()
    {
        // Arrange
        var cartResult = Cart.Create();
        cartResult.IsSuccess.Should().BeTrue();
        var cart = cartResult.Value;
        
        var product1 = CreateTestProduct();
        
        var slugResult = Slug.Create("product-2");
        slugResult.IsSuccess.Should().BeTrue();
        
        var priceResult = Money.FromDollars(200);
        priceResult.IsSuccess.Should().BeTrue();
        
        var product2Result = Product.Create("Product 2", slugResult.Value, priceResult.Value);
        product2Result.IsSuccess.Should().BeTrue();
        var product2 = product2Result.Value;

        cart.AddItem(product1, 1);
        cart.AddItem(product2, 1);

        cart.ClearDomainEvents(); // Clear previous events

        // Act
        var clearResult = cart.Clear();

        // Assert
        clearResult.IsSuccess.Should().BeTrue();
        cart.DomainEvents.Should().ContainSingle(e => e is CartClearedEvent);
        var domainEvent = cart.DomainEvents.First(e => e is CartClearedEvent);
        var clearedEvent = (CartClearedEvent)domainEvent;
        clearedEvent.CartId.Should().Be(cart.Id);
    }
}