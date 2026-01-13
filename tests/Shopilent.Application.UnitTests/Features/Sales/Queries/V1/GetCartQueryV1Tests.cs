using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Queries.GetCart.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Sales.DTOs;

namespace Shopilent.Application.UnitTests.Features.Sales.Queries.V1;

public class GetCartQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetCartQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockCartReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockS3StorageService.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetCartQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GetCartQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task GetCart_WithValidCartId_ReturnsCart()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetCartQueryV1 { CartId = cartId };

        var cartDto = new CartDto
        {
            Id = cartId,
            UserId = userId,
            TotalItems = 2,
            TotalAmount = 99.99m,
            Items = new List<CartItemDto>(),
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartReadRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cartDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(cartId);
        result.Value.UserId.Should().Be(userId);
        result.Value.TotalItems.Should().Be(2);
        result.Value.TotalAmount.Should().Be(99.99m);

        // Verify repository was called correctly
        Fixture.MockCartReadRepository.Verify(
            repo => repo.GetByIdAsync(cartId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetCart_WithoutCartIdForAuthenticatedUser_ReturnsUserCart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        var query = new GetCartQueryV1
        {
            // No cart ID provided
        };

        var cartDto = new CartDto
        {
            Id = cartId,
            UserId = userId,
            TotalItems = 1,
            TotalAmount = 49.99m,
            Items = new List<CartItemDto>(),
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(cartDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(cartId);
        result.Value.UserId.Should().Be(userId);

        // Verify the correct repository method was called
        Fixture.MockCartReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetCart_WithNonExistentCartId_ReturnsNull()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetCartQueryV1 { CartId = cartId };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls - cart not found
        Fixture.MockCartReadRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync((CartDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();

        // Verify repository was called correctly
        Fixture.MockCartReadRepository.Verify(
            repo => repo.GetByIdAsync(cartId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetCart_AccessingOtherUserCart_ReturnsNull()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var cartOwnerId = Guid.NewGuid(); // Different user

        var query = new GetCartQueryV1 { CartId = cartId };

        var cartDto = new CartDto
        {
            Id = cartId,
            UserId = cartOwnerId, // Cart belongs to different user
            TotalItems = 1,
            TotalAmount = 49.99m,
            Items = new List<CartItemDto>(),
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Setup current user (different from cart owner)
        Fixture.SetAuthenticatedUser(currentUserId);

        // Mock repository calls
        Fixture.MockCartReadRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cartDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull(); // Should return null for security reasons

        // Verify repository was called correctly
        Fixture.MockCartReadRepository.Verify(
            repo => repo.GetByIdAsync(cartId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetCart_ForAnonymousUserWithoutCartId_ReturnsNull()
    {
        // Arrange
        var query = new GetCartQueryV1
        {
            // No cart ID and no authenticated user
        };

        // Don't set authenticated user

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();

        // Verify no repository calls were made
        Fixture.MockCartReadRepository.Verify(
            repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Fixture.MockCartReadRepository.Verify(
            repo => repo.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCart_WithNoUserCartFound_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetCartQueryV1
        {
            // No cart ID provided
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls - no cart found for user
        Fixture.MockCartReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync((CartDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();

        // Verify repository was called correctly
        Fixture.MockCartReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }
}
