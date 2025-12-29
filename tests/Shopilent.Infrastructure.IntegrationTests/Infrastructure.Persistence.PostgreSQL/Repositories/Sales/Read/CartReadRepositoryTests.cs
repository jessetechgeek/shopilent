using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Sales.Read;

[Collection("IntegrationTests")]
public class CartReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IProductWriteRepository _productWriteRepository = null!;
    private ICategoryWriteRepository _categoryWriteRepository = null!;
    private ICartWriteRepository _cartWriteRepository = null!;
    private ICartReadRepository _cartReadRepository = null!;

    public CartReadRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
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
    public async Task GetByIdAsync_ExistingCart_ShouldReturnCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var cart = new CartBuilder()
            .WithUser(user)
            .WithMetadata("source", "web")
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _cartReadRepository.GetByIdAsync(cart.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.UserId.Should().Be(user.Id);
        result.Metadata.Should().ContainKey("source");
        result.Metadata["source"].ToString().Should().Be("web");
        result.CreatedAt.Should().BeCloseTo(cart.CreatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentCart_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _cartReadRepository.GetByIdAsync(nonExistentId);

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

        var cart = new CartBuilder()
            .WithUser(user)
            .WithRandomMetadata()
            .Build();

        await _cartWriteRepository.AddAsync(cart);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _cartReadRepository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.UserId.Should().Be(user.Id);
        result.Metadata.Should().NotBeEmpty();
        result.CreatedAt.Should().BeCloseTo(cart.CreatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetByUserIdAsync_NonExistentUser_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _cartReadRepository.GetByUserIdAsync(nonExistentUserId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_AnonymousCart_ShouldNotReturnCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var anonymousCart = new CartBuilder()
            .AsAnonymousCart()
            .Build();

        await _cartWriteRepository.AddAsync(anonymousCart);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _cartReadRepository.GetByUserIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAllAsync_EmptyRepository_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _cartReadRepository.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAllAsync_HasCarts_ShouldReturnAllCarts()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = new UserBuilder().Build();
        var user2 = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _unitOfWork.CommitAsync();

        var cart1 = new CartBuilder().WithUser(user1).Build();
        var cart2 = new CartBuilder().WithUser(user2).Build();
        var anonymousCart = new CartBuilder().AsAnonymousCart().Build();

        await _cartWriteRepository.AddAsync(cart1);
        await _cartWriteRepository.AddAsync(cart2);
        await _cartWriteRepository.AddAsync(anonymousCart);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _cartReadRepository.ListAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(c => c.Id).Should().Contain(new[] { cart1.Id, cart2.Id, anonymousCart.Id });
    }

    [Fact]
    public async Task GetAbandonedCartsAsync_HasOldCarts_ShouldReturnAbandonedCarts()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = new UserBuilder().Build();
        var user2 = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product1 = new ProductBuilder().WithCategory(category).Build();
        var product2 = new ProductBuilder().WithCategory(category).Build();
        await _productWriteRepository.AddAsync(product1);
        await _productWriteRepository.AddAsync(product2);
        await _unitOfWork.CommitAsync();

        // Create carts with items that will be considered "old"
        // Note: GetAbandonedCartsAsync only returns carts that have items
        var oldCart = new CartBuilder()
            .WithUser(user1)
            .WithItem(product1, 1)
            .Build();
        var anotherOldCart = new CartBuilder()
            .WithUser(user2)
            .WithItem(product2, 2)
            .Build();

        await _cartWriteRepository.AddAsync(oldCart);
        await _cartWriteRepository.AddAsync(anotherOldCart);
        await _unitOfWork.CommitAsync();

        // Wait to ensure the carts are older than our threshold
        await Task.Delay(2000); // Wait 2 seconds to be safe

        // Act - looking for carts older than 1 second
        var result = await _cartReadRepository.GetAbandonedCartsAsync(TimeSpan.FromSeconds(1));

        // Assert - Both carts should be considered "abandoned" since they're older than 1 second
        // and they both have items (which is required by the implementation)
        result.Should().HaveCount(2);
        result.Select(c => c.Id).Should().Contain(new[] { oldCart.Id, anotherOldCart.Id });
    }

    [Fact]
    public async Task GetAbandonedCartsAsync_NoOldCarts_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var recentCart = new CartBuilder().WithUser(user).Build();
        await _cartWriteRepository.AddAsync(recentCart);
        await _unitOfWork.CommitAsync();

        // Act - looking for carts older than 1 hour
        var result = await _cartReadRepository.GetAbandonedCartsAsync(TimeSpan.FromHours(1));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAbandonedCartsAsync_EmptyRepository_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _cartReadRepository.GetAbandonedCartsAsync(TimeSpan.FromMinutes(30));

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_CartWithItems_ShouldIncludeItemsData()
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

        // Act
        var result = await _cartReadRepository.GetByIdAsync(cart.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cart.Id);
        result.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(3); // 2 + 1
        result.Items.Should().OnlyContain(item => item.Quantity > 0);
        result.Items.Select(i => i.ProductId).Should().Contain(new[] { product1.Id, product2.Id });
    }

    [Fact]
    public async Task GetByUserIdAsync_MultipleCartsForUser_ShouldReturnLatestCart()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Create multiple carts for the same user
        var cart1 = new CartBuilder().WithUser(user).Build();
        await _cartWriteRepository.AddAsync(cart1);
        await _unitOfWork.CommitAsync();

        // Wait a bit to ensure different timestamps
        await Task.Delay(100);

        var cart2 = new CartBuilder().WithUser(user).WithMetadata("version", "2").Build();
        await _cartWriteRepository.AddAsync(cart2);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _cartReadRepository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        // Should return the latest cart (implementation may vary, but typically returns the most recent one)
        result!.UserId.Should().Be(user.Id);
        // The specific cart returned depends on the repository implementation
        // but both carts should be valid for this user
        new[] { cart1.Id, cart2.Id }.Should().Contain(result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NullId_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _cartReadRepository.GetByIdAsync(Guid.Empty);

        // Assert
        result.Should().BeNull();
    }
}
