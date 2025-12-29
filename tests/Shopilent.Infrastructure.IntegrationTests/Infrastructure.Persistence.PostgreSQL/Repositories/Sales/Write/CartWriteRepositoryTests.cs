using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Sales.Write;

[Collection("IntegrationTests")]
public class CartWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IProductWriteRepository _productWriteRepository = null!;
    private ICategoryWriteRepository _categoryWriteRepository = null!;
    private ICartWriteRepository _cartWriteRepository = null!;
    private ICartReadRepository _cartReadRepository = null!;

    public CartWriteRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _productWriteRepository = GetService<IProductWriteRepository>();
        _categoryWriteRepository = GetService<ICategoryWriteRepository>();
        _cartWriteRepository = GetService<ICartWriteRepository>();
        _cartReadRepository = GetService<ICartReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidCart_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithMetadata("source", "web")
            .WithMetadata("sessionId", Guid.NewGuid().ToString())
            .Build();

        // Act
        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _cartReadRepository.GetByIdAsync(cart.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.UserId.Should().Be(user.Id);
        result.Metadata.Should().ContainKey("source");
        result.Metadata["source"].ToString().Should().Be("web");
        result.CreatedAt.Should().BeCloseTo(cart.CreatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task AddAsync_AnonymousCart_ShouldPersistWithoutUser()
    {
        // Arrange
        await ResetDatabaseAsync();

        var cart = new CartBuilder()
            .AsAnonymousCart()
            .WithMetadata("device", "mobile")
            .Build();

        // Act
        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _cartReadRepository.GetByIdAsync(cart.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.UserId.Should().BeNull();
        result.Metadata.Should().ContainKey("device");
        result.Metadata["device"].ToString().Should().Be("mobile");
    }

    [Fact]
    public async Task AddAsync_CartWithItems_ShouldPersistCartAndItems()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product1 = new ProductBuilder().WithCategory(category).Build();
        var product2 = new ProductBuilder().WithCategory(category).Build();
        await _productWriteRepository.AddAsync(product1);
        await _productWriteRepository.AddAsync(product2);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithItem(product1, 2)
            .WithItem(product2, 1)
            .Build();

        // Act
        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _cartReadRepository.GetByIdAsync(cart.Id);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(3); // 2 + 1
        result.Items.Should().OnlyContain(item => item.Quantity > 0);
    }

    [Fact]
    public async Task UpdateAsync_ExistingCart_ShouldModifyCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithMetadata("version", "1")
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(cart).State = EntityState.Detached;

        // Act - Load fresh entity and update
        var existingCart = await _cartWriteRepository.GetByIdAsync(cart.Id);
        existingCart!.UpdateMetadata("version", "2");
        existingCart.UpdateMetadata("updated", true);

        await _cartWriteRepository.UpdateAsync(existingCart);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedCart = await _cartReadRepository.GetByIdAsync(cart.Id);
        updatedCart.Should().NotBeNull();
        updatedCart!.Metadata["version"].ToString().Should().Be("2");
        updatedCart.Metadata["updated"].ToString().Should().Be("True");
        updatedCart.UpdatedAt.Should().BeAfter(cart.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_AddItemToExistingCart_ShouldUpdateItems()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product1 = new ProductBuilder().WithCategory(category).Build();
        var product2 = new ProductBuilder().WithCategory(category).Build();
        await _productWriteRepository.AddAsync(product1);
        await _productWriteRepository.AddAsync(product2);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithItem(product1, 1)
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(cart).State = EntityState.Detached;

        // Act - Load fresh entity and add item
        var existingCart = await _cartWriteRepository.GetByIdAsync(cart.Id);
        existingCart!.AddItem(product2, 2);

        await _cartWriteRepository.UpdateAsync(existingCart);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedCart = await _cartReadRepository.GetByIdAsync(cart.Id);
        updatedCart.Should().NotBeNull();
        updatedCart!.Items.Should().HaveCount(2);
        updatedCart.TotalItems.Should().Be(3); // 1 + 2
    }

    [Fact]
    public async Task DeleteAsync_ExistingCart_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder().WithUser(user).Build();
        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Act
        await _cartWriteRepository.DeleteAsync(cart);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _cartReadRepository.GetByIdAsync(cart.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCart_ShouldReturnCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder().WithUser(user).Build();
        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _cartWriteRepository.GetByIdAsync(cart.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentCart_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _cartWriteRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ExistingUserCart_ShouldReturnCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder().WithUser(user).Build();
        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _cartWriteRepository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_NonExistentUser_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _cartWriteRepository.GetByUserIdAsync(nonExistentUserId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCartByItemIdAsync_ExistingCartItem_ShouldReturnCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product = new ProductBuilder().WithCategory(category).Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithItem(product, 1)
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        var cartItem = cart.Items.First();

        // Act
        var result = await _cartWriteRepository.GetCartByItemIdAsync(cartItem.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.Items.Should().Contain(item => item.Id == cartItem.Id);
    }

    [Fact]
    public async Task GetCartByItemIdAsync_NonExistentCartItem_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentCartItemId = Guid.NewGuid();

        // Act
        var result = await _cartWriteRepository.GetCartByItemIdAsync(nonExistentCartItemId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentUpdate_ShouldThrowConcurrencyException()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithMetadata("concurrent", "test")
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Act - Simulate concurrent access with two service scopes
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cartWriteRepository1 = scope1.ServiceProvider.GetRequiredService<ICartWriteRepository>();
        var cartWriteRepository2 = scope2.ServiceProvider.GetRequiredService<ICartWriteRepository>();

        // Both contexts load the same entity (same Version initially)
        var cart1 = await cartWriteRepository1.GetByIdAsync(cart.Id);
        var cart2 = await cartWriteRepository2.GetByIdAsync(cart.Id);

        // Both try to modify the cart
        cart1!.UpdateMetadata("version", "1");
        cart2!.UpdateMetadata("version", "2");

        // First update should succeed (Version incremented)
        await cartWriteRepository1.UpdateAsync(cart1);
        await unitOfWork1.CommitAsync();

        // Second update should fail with concurrency exception (stale Version)
        await cartWriteRepository2.UpdateAsync(cart2);
        var action = () => unitOfWork2.CommitAsync();

        // Assert
        await action.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task UpdateAsync_ClearCartItems_ShouldRemoveAllItems()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product1 = new ProductBuilder().WithCategory(category).Build();
        var product2 = new ProductBuilder().WithCategory(category).Build();
        await _productWriteRepository.AddAsync(product1);
        await _productWriteRepository.AddAsync(product2);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithItem(product1, 2)
            .WithItem(product2, 1)
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(cart).State = EntityState.Detached;

        // Act - Load fresh entity and clear
        var existingCart = await _cartWriteRepository.GetByIdAsync(cart.Id);
        existingCart!.Clear();

        await _cartWriteRepository.UpdateAsync(existingCart);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedCart = await _cartReadRepository.GetByIdAsync(cart.Id);
        updatedCart.Should().NotBeNull();
        updatedCart!.Items.Should().BeEmpty();
        updatedCart.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_AssignToUser_ShouldUpdateUserId()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var anonymousCart = new CartBuilder()
            .AsAnonymousCart()
            .WithMetadata("device", "mobile")
            .Build();

        await _cartWriteRepository.AddAsync(anonymousCart);
        await _unitOfWork.CommitAsync();


        // Act - Assign cart to user (both entities are already tracked)
        anonymousCart.AssignToUser(user);

        await _cartWriteRepository.UpdateAsync(anonymousCart);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedCart = await _cartReadRepository.GetByIdAsync(anonymousCart.Id);
        updatedCart.Should().NotBeNull();
        updatedCart!.UserId.Should().Be(user.Id);
        updatedCart.Metadata["device"].ToString().Should().Be("mobile"); // Should preserve existing metadata
    }

    [Fact]
    public async Task UpdateAsync_UpdateItemQuantity_ShouldModifyItemQuantity()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product = new ProductBuilder().WithCategory(category).Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithItem(product, 1)
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        var cartItem = cart.Items.First();

        // Detach to simulate real-world scenario
        DbContext.Entry(cart).State = EntityState.Detached;

        // Act - Load fresh entity and update item quantity
        var existingCart = await _cartWriteRepository.GetByIdAsync(cart.Id);
        existingCart!.UpdateItemQuantity(cartItem.Id, 5);

        await _cartWriteRepository.UpdateAsync(existingCart);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedCart = await _cartReadRepository.GetByIdAsync(cart.Id);
        updatedCart.Should().NotBeNull();
        updatedCart!.Items.Should().HaveCount(1);
        updatedCart.TotalItems.Should().Be(5);
        var updatedItem = updatedCart.Items.First();
        updatedItem.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_RemoveItem_ShouldRemoveItemFromCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product1 = new ProductBuilder().WithCategory(category).Build();
        var product2 = new ProductBuilder().WithCategory(category).Build();
        await _productWriteRepository.AddAsync(product1);
        await _productWriteRepository.AddAsync(product2);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithItem(product1, 2)
            .WithItem(product2, 1)
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        var itemToRemove = cart.Items.First();

        // Detach to simulate real-world scenario
        DbContext.Entry(cart).State = EntityState.Detached;

        // Act - Load fresh entity and remove item
        var existingCart = await _cartWriteRepository.GetByIdAsync(cart.Id);
        existingCart!.RemoveItem(itemToRemove.Id);

        await _cartWriteRepository.UpdateAsync(existingCart);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedCart = await _cartReadRepository.GetByIdAsync(cart.Id);
        updatedCart.Should().NotBeNull();
        updatedCart!.Items.Should().HaveCount(1);
        updatedCart.Items.Should().NotContain(item => item.Id == itemToRemove.Id);
    }
}
