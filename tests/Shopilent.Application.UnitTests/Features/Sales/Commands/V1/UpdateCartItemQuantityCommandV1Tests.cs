using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.UpdateCartItemQuantity.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class UpdateCartItemQuantityCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public UpdateCartItemQuantityCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockCartWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateCartItemQuantityCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateCartItemQuantityCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateCartItemQuantityCommandV1>, UpdateCartItemQuantityCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequestWithAuthenticatedUser_UpdatesQuantitySuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cartItemId = Guid.NewGuid();
        var newQuantity = 5;

        var command = new UpdateCartItemQuantityCommandV1
        {
            CartItemId = cartItemId,
            Quantity = newQuantity
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();
        var product = new ProductBuilder().Build();

        // Add an item to the cart so we can update its quantity
        var addItemResult = cart.AddItem(product.Id, 1);
        addItemResult.IsSuccess.Should().BeTrue("Failed to add item to cart for test setup");
        var cartItem = addItemResult.Value;

        // Use reflection to set the item ID to match what we're trying to update
        SetPrivatePropertyValue(cartItem, "Id", cartItemId);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetCartByItemIdAsync(cartItemId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CartItemId.Should().Be(cartItemId);
        result.Value.Quantity.Should().Be(newQuantity);
        result.Value.UpdatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestWithAnonymousUser_UpdatesQuantitySuccessfully()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var cartItemId = Guid.NewGuid();
        var newQuantity = 3;

        var command = new UpdateCartItemQuantityCommandV1
        {
            CartItemId = cartItemId,
            Quantity = newQuantity
        };

        var cart = new CartBuilder().WithId(cartId).Build(); // Anonymous cart
        var product = new ProductBuilder().Build();

        // Add an item to the cart so we can update its quantity
        var addItemResult = cart.AddItem(product.Id, 1);
        addItemResult.IsSuccess.Should().BeTrue("Failed to add item to cart for test setup");
        var cartItem = addItemResult.Value;

        // Use reflection to set the item ID to match what we're trying to update
        SetPrivatePropertyValue(cartItem, "Id", cartItemId);

        // No authenticated user
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetCartByItemIdAsync(cartItemId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CartItemId.Should().Be(cartItemId);
        result.Value.Quantity.Should().Be(newQuantity);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ItemNotInAnyCart_ReturnsCartNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartItemId = Guid.NewGuid();

        var command = new UpdateCartItemQuantityCommandV1
        {
            CartItemId = cartItemId,
            Quantity = 2
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // No cart found for this item
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetCartByItemIdAsync(cartItemId, CancellationToken))
            .ReturnsAsync((Cart)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CartErrors.CartNotFound(Guid.Empty).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CartBelongsToDifferentUser_ReturnsCartNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cartItemId = Guid.NewGuid();

        var command = new UpdateCartItemQuantityCommandV1
        {
            CartItemId = cartItemId,
            Quantity = 4
        };

        var otherUser = new UserBuilder().WithId(otherUserId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(otherUser).Build();

        // Setup authenticated user (different from cart owner)
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetCartByItemIdAsync(cartItemId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CartErrors.CartNotFound(Guid.Empty).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ZeroQuantity_UpdatesQuantitySuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cartItemId = Guid.NewGuid();
        var zeroQuantity = 0;

        var command = new UpdateCartItemQuantityCommandV1
        {
            CartItemId = cartItemId,
            Quantity = zeroQuantity
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();
        var product = new ProductBuilder().Build();

        // Add an item to the cart so we can update its quantity
        var addItemResult = cart.AddItem(product.Id, 1);
        addItemResult.IsSuccess.Should().BeTrue("Failed to add item to cart for test setup");
        var cartItem = addItemResult.Value;

        // Use reflection to set the item ID to match what we're trying to update
        SetPrivatePropertyValue(cartItem, "Id", cartItemId);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetCartByItemIdAsync(cartItemId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CartItemId.Should().Be(cartItemId);
        result.Value.Quantity.Should().Be(zeroQuantity);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AuthenticatedUserAccessingAnonymousCart_ReturnsCartNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cartItemId = Guid.NewGuid();
        var newQuantity = 7;

        var command = new UpdateCartItemQuantityCommandV1
        {
            CartItemId = cartItemId,
            Quantity = newQuantity
        };

        var cart = new CartBuilder().WithId(cartId).Build(); // Anonymous cart (no user)
        var product = new ProductBuilder().Build();

        // Add an item to the cart so we can update its quantity
        var addItemResult = cart.AddItem(product.Id, 1);
        addItemResult.IsSuccess.Should().BeTrue("Failed to add item to cart for test setup");
        var cartItem = addItemResult.Value;

        // Use reflection to set the item ID to match what we're trying to update
        SetPrivatePropertyValue(cartItem, "Id", cartItemId);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetCartByItemIdAsync(cartItemId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CartErrors.CartNotFound(Guid.Empty).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    private static void SetPrivatePropertyValue<T>(object obj, string propertyName, T value)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        propertyInfo?.SetValue(obj, value);
    }
}
