using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.ClearCart.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class ClearCartCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public ClearCartCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockCartWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<ClearCartCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<ClearCartCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<ClearCartCommandV1>, ClearCartCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task ClearCart_WithValidCartId_ReturnsSuccessfulResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new ClearCartCommandV1
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

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ClearCart_WithNonExistentCartId_ReturnsErrorResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new ClearCartCommandV1
        {
            CartId = cartId
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls - cart not found
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
    public async Task ClearCart_WithoutCartIdForAuthenticatedUser_ClearsUserCart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        var command = new ClearCartCommandV1
        {
            // No cart ID provided
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(cart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the correct repository method was called
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ClearCart_WithoutCartIdForAnonymousUser_ReturnsErrorResult()
    {
        // Arrange
        var command = new ClearCartCommandV1
        {
            // No cart ID provided and no authenticated user
        };

        // Don't set authenticated user

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CartErrors.CartNotFound(Guid.Empty).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ClearCart_AccessingOtherUserCart_ReturnsErrorResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var cartOwnerId = Guid.NewGuid(); // Different user

        var command = new ClearCartCommandV1
        {
            CartId = cartId
        };

        var cartOwner = new UserBuilder().WithId(cartOwnerId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(cartOwner).Build();

        // Setup current user (different from cart owner)
        Fixture.SetAuthenticatedUser(currentUserId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

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
    public async Task ClearCart_WithNoUserCartFound_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new ClearCartCommandV1
        {
            // No cart ID provided
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls - no cart found for user
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync((Cart)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CartErrors.CartNotFound(Guid.Empty).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }
}
