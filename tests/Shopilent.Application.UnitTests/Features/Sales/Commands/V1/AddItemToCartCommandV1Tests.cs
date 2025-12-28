using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.AddItemToCart.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class AddItemToCartCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public AddItemToCartCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCartWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<AddItemToCartCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<AddItemToCartCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<AddItemToCartCommandV1>, AddItemToCartCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task AddItemToCart_WithValidDataAndExistingCart_ReturnsSuccessfulResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AddItemToCartCommandV1 { CartId = cartId, ProductId = productId, Quantity = 2 };

        var user = new UserBuilder().WithId(userId).Build();
        var product = new ProductBuilder().WithId(productId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(product);

        // Capture cart updates
        Cart updatedCart = null;
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Cart>(), CancellationToken))
            .Callback<Cart, CancellationToken>((c, _) => updatedCart = c)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CartId.Should().Be(cartId);
        result.Value.ProductId.Should().Be(productId);
        result.Value.Quantity.Should().Be(2);

        // Verify cart was updated
        updatedCart.Should().NotBeNull();
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task AddItemToCart_WithNonExistentProduct_ReturnsErrorResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AddItemToCartCommandV1 { CartId = cartId, ProductId = productId, Quantity = 1 };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        // Product not found
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync((Product)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.NotFound(productId).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task AddItemToCart_WithNonExistentCart_ReturnsErrorResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AddItemToCartCommandV1 { CartId = cartId, ProductId = productId, Quantity = 1 };

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
    }

    [Fact]
    public async Task AddItemToCart_CreatesNewCartWhenNoneExists_ReturnsSuccessfulResult()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AddItemToCartCommandV1
        {
            // No cart ID - should create new cart
            ProductId = productId, Quantity = 1
        };

        var user = new UserBuilder().WithId(userId).Build();
        var product = new ProductBuilder().WithId(productId).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // No existing cart
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync((Cart)null);

        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(product);

        // Capture new cart being added
        Cart addedCart = null;
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken))
            .Callback<Cart, CancellationToken>((c, _) => addedCart = c)
            .ReturnsAsync((Cart c, CancellationToken _) => c);

        // Mock save changes to return success
        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ProductId.Should().Be(productId);
        result.Value.Quantity.Should().Be(1);

        // Verify new cart was created and added
        addedCart.Should().NotBeNull();
        addedCart.UserId.Should().Be(userId);

        Fixture.MockCartWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Once);

        // Verify save was called twice (once for cart creation, once for item addition)
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Exactly(2));
    }

    [Fact]
    public async Task AddItemToCart_WithVariant_AddsVariantToCart()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AddItemToCartCommandV1
        {
            CartId = cartId, ProductId = productId, VariantId = variantId, Quantity = 1
        };

        var user = new UserBuilder().WithId(userId).Build();
        var product = new ProductBuilder().WithId(productId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();

        // Create a variant using the domain factory method
        var variantResult = ProductVariant.Create(productId, "TEST-VARIANT", null, 10);
        if (variantResult.IsFailure)
            throw new InvalidOperationException($"Failed to create variant: {variantResult.Error.Message}");
        var variant = variantResult.Value;

        // Set the specific ID we want for testing using reflection (similar to other builders)
        SetPrivatePropertyValue(variant, "Id", variantId);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(product);

        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Mock save operations
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Cart>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.VariantId.Should().Be(variantId);
        result.Value.ProductId.Should().Be(productId);
    }

    private static void SetPrivatePropertyValue(object obj, string propertyName, object value)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        propertyInfo?.SetValue(obj, value);
    }
}
