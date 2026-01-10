using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.CreateOrderFromCart.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class CreateOrderFromCartCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public CreateOrderFromCartCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockAddressWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockProductWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockProductVariantWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCartWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<CreateOrderFromCartCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateOrderFromCartCommandV1>();
        });

        // Register validator
        services
            .AddTransient<FluentValidation.IValidator<CreateOrderFromCartCommandV1>,
                CreateOrderFromCartCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task CreateOrderFromCart_WithValidData_ReturnsSuccessfulResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shippingAddressId = Guid.NewGuid();
        var billingAddressId = Guid.NewGuid();

        var command = new CreateOrderFromCartCommandV1
        {
            CartId = cartId,
            ShippingAddressId = shippingAddressId,
            BillingAddressId = billingAddressId,
            ShippingMethod = "Standard"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();

        // Add a test item to the cart so it's not empty
        var testProduct = new ProductBuilder().Build();
        var addItemResult = cart.AddItem(testProduct.Id, 1);
        if (addItemResult.IsFailure)
            throw new InvalidOperationException($"Failed to add item to cart: {addItemResult.Error.Message}");

        var shippingAddress = new AddressBuilder()
            .WithId(shippingAddressId)
            .WithUser(user)
            .WithAddressType(AddressType.Shipping)
            .WithStreetAddress("123 Main St")
            .WithLocation("Anytown", "CA", "12345", "US")
            .Build();

        var billingAddress = new AddressBuilder()
            .WithId(billingAddressId)
            .WithUser(user)
            .WithAddressType(AddressType.Billing)
            .WithStreetAddress("123 Main St")
            .WithLocation("Anytown", "CA", "12345", "US")
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock user repository - user exists
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(shippingAddressId, CancellationToken))
            .ReturnsAsync(shippingAddress);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(billingAddressId, CancellationToken))
            .ReturnsAsync(billingAddress);

        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(testProduct.Id, CancellationToken))
            .ReturnsAsync(testProduct);

        // Capture order being added
        Order addedOrder = null;
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Order>(), CancellationToken))
            .Callback<Order, CancellationToken>((o, _) => addedOrder = o)
            .ReturnsAsync((Order o, CancellationToken _) => o);

        // Mock save operations
        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(userId);
        result.Value.ShippingAddressId.Should().Be(shippingAddressId);
        result.Value.BillingAddressId.Should().Be(billingAddressId);
        result.Value.ShippingMethod.Should().Be("Standard");

        // Verify order was created and added
        addedOrder.Should().NotBeNull();
        addedOrder.UserId.Should().Be(userId);

        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Order>(), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithNonExistentCart_ReturnsErrorResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new CreateOrderFromCartCommandV1
        {
            CartId = cartId, ShippingAddressId = Guid.NewGuid(), BillingAddressId = Guid.NewGuid()
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock user repository - user exists
        var user = new UserBuilder().WithId(userId).Build();
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock repository calls - cart not found
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync((Cart)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CartErrors.CartNotFound(cartId).Code);

        // Verify order was not created
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithNonExistentShippingAddress_ReturnsErrorResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shippingAddressId = Guid.NewGuid();
        var billingAddressId = Guid.NewGuid();

        var command = new CreateOrderFromCartCommandV1
        {
            CartId = cartId, ShippingAddressId = shippingAddressId, BillingAddressId = billingAddressId
        };

        var user = new UserBuilder().WithId(userId).Build();
        var cart = new CartBuilder().WithId(cartId).WithUser(user).Build();
        var billingAddressDto = new AddressDto
        {
            Id = billingAddressId,
            UserId = userId,
            AddressLine1 = "123 Main St",
            City = "Anytown",
            State = "CA",
            PostalCode = "12345",
            Country = "US",
            AddressType = AddressType.Billing,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByIdAsync(cartId, CancellationToken))
            .ReturnsAsync(cart);

        // Shipping address not found
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(shippingAddressId, CancellationToken))
            .ReturnsAsync((AddressDto)null);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(billingAddressId, CancellationToken))
            .ReturnsAsync(billingAddressDto);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();

        // Verify order was not created
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCart_AccessingOtherUserCart_ReturnsErrorResult()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var cartOwnerId = Guid.NewGuid(); // Different user

        var command = new CreateOrderFromCartCommandV1
        {
            CartId = cartId, ShippingAddressId = Guid.NewGuid(), BillingAddressId = Guid.NewGuid()
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

        // Verify order was not created
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);
    }
}
