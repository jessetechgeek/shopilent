using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.CreateCart.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Sales;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class CreateCartCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public CreateCartCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<CreateCartCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<CreateCartCommandV1>, CreateCartCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_AnonymousUser_CreatesAnonymousCartSuccessfully()
    {
        // Arrange
        var command = new CreateCartCommandV1();

        // No authenticated user
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        // Setup cart creation
        Cart createdCart = null;
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken))
            .Callback<Cart, CancellationToken>((c, _) => createdCart = c)
            .ReturnsAsync((Cart c, CancellationToken _) => c);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().BeNull(); // Anonymous cart
        result.Value.ItemCount.Should().Be(0);
        createdCart.Should().NotBeNull();

        // Verify cart was added
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AuthenticatedUserWithoutExistingCart_CreatesNewCartSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateCartCommandV1();

        var user = new UserBuilder().WithId(userId).Build();

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

        // Setup cart creation
        Cart createdCart = null;
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken))
            .Callback<Cart, CancellationToken>((c, _) => createdCart = c)
            .ReturnsAsync((Cart c, CancellationToken _) => c);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(userId);
        result.Value.ItemCount.Should().Be(0);
        createdCart.Should().NotBeNull();
        createdCart.UserId.Should().Be(userId);

        // Verify cart was added
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AuthenticatedUserWithExistingCart_ReturnsExistingCart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingCartId = Guid.NewGuid();
        var command = new CreateCartCommandV1();

        var user = new UserBuilder().WithId(userId).Build();
        var existingCart = new CartBuilder().WithId(existingCartId).WithUser(user).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Existing cart found
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingCart);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(existingCartId);
        result.Value.UserId.Should().Be(userId);

        // Verify no new cart was added
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsUserNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateCartCommandV1();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // User not found
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync((User)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.NotFound(userId).Code);

        // Verify no cart was added
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithMetadata_CreatesCartWithMetadataSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            ["source"] = "mobile_app",
            ["campaign"] = "summer_sale"
        };

        var command = new CreateCartCommandV1
        {
            Metadata = metadata
        };

        var user = new UserBuilder().WithId(userId).Build();

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

        // Setup cart creation
        Cart createdCart = null;
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken))
            .Callback<Cart, CancellationToken>((c, _) => createdCart = c)
            .ReturnsAsync((Cart c, CancellationToken _) => c);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(userId);
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata.Count.Should().Be(2);
        result.Value.Metadata["source"].Should().Be("mobile_app");
        result.Value.Metadata["campaign"].Should().Be("summer_sale");

        // Verify cart was added
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AnonymousUserWithMetadata_CreatesAnonymousCartWithMetadataSuccessfully()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["session"] = "guest_session_123",
            ["referrer"] = "google_ads"
        };

        var command = new CreateCartCommandV1
        {
            Metadata = metadata
        };

        // No authenticated user
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        // Setup cart creation
        Cart createdCart = null;
        Fixture.MockCartWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken))
            .Callback<Cart, CancellationToken>((c, _) => createdCart = c)
            .ReturnsAsync((Cart c, CancellationToken _) => c);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().BeNull(); // Anonymous cart
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata.Count.Should().Be(2);
        result.Value.Metadata["session"].Should().Be("guest_session_123");
        result.Value.Metadata["referrer"].Should().Be("google_ads");

        // Verify cart was added
        Fixture.MockCartWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Cart>(), CancellationToken),
            Times.Once);
    }
}
