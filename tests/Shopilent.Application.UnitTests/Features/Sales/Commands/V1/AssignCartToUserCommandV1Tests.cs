using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.AssignCartToUser.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class AssignCartToUserCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public AssignCartToUserCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCartWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<AssignCartToUserCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<AssignCartToUserCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<AssignCartToUserCommandV1>, AssignCartToUserCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_AssignsCartToUserSuccessfully()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AssignCartToUserCommandV1
        {
            CartId = cartId
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).Build(); // Cart without user

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // No existing cart for user
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync((Cart)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ReturnsUnauthorizedError()
    {
        // Arrange
        var cartId = Guid.NewGuid();

        var command = new AssignCartToUserCommandV1
        {
            CartId = cartId
        };

        // No authenticated user
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.IsAuthenticated).Returns(false);
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.UserNotAuthenticated");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonExistentCart_ReturnsCartNotFoundError()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AssignCartToUserCommandV1
        {
            CartId = cartId
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Cart not found
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync((Cart)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CartErrors.CartNotFound(cartId).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CartAlreadyAssignedToCurrentUser_ReturnsSuccess()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AssignCartToUserCommandV1
        {
            CartId = cartId
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify save was not called (no changes needed)
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CartAlreadyAssignedToDifferentUser_ReturnsValidationError()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var command = new AssignCartToUserCommandV1
        {
            CartId = cartId
        };

        var otherUser = new UserBuilder().WithId(otherUserId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(otherUser).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.AlreadyAssigned");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsUserNotFoundError()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AssignCartToUserCommandV1
        {
            CartId = cartId
        };

        var cart = new CartBuilder().WithId(cartId).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        // User not found
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync((User)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.NotFound(userId).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserAlreadyHasCart_ReturnsValidationError()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingCartId = Guid.NewGuid();

        var command = new AssignCartToUserCommandV1
        {
            CartId = cartId
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).Build();
        var existingCart = new CartBuilder().WithId(existingCartId).WithUser(user).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // User already has a cart
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingCart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.UserAlreadyHasCart");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }
}
