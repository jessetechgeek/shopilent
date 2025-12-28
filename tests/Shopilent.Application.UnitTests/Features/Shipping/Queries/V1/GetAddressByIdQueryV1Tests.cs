using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Shipping.Queries.GetAddressById.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Application.UnitTests.Features.Shipping.Queries.V1;

public class GetAddressByIdQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetAddressByIdQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAddressReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetAddressByIdQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<GetAddressByIdQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_WithValidAddressId_ReturnsAddress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository call
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.UserId.Should().Be(userId);
        result.Value.AddressLine1.Should().Be("123 Main Street");
        result.Value.AddressLine2.Should().Be("Apt 4B");
        result.Value.City.Should().Be("New York");
        result.Value.State.Should().Be("NY");
        result.Value.PostalCode.Should().Be("10001");
        result.Value.Country.Should().Be("US");
        result.Value.Phone.Should().Be("+15551234567");
        result.Value.AddressType.Should().Be(AddressType.Shipping);
        result.Value.IsDefault.Should().BeFalse();

        // Verify repository was called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithUnauthenticatedUser_ReturnsNotFound()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping);

        // Don't set authenticated user (CurrentUserContext.UserId will be null)

        // Mock repository call - handler always calls repository first
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("Address.NotFound");

        // Verify repository was called (handler always fetches first, then checks authorization)
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentAddress_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository to return null
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync((AddressDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("Address.NotFound");

        // Verify repository was called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAddressBelongingToOtherUser_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        var addressDto = CreateTestAddressDto(addressId, otherUserId, AddressType.Shipping); // Address belongs to other user

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository call
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("Address.NotFound");

        // Verify repository was called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithShippingAddress_ReturnsShippingAddress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository call
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AddressType.Should().Be(AddressType.Shipping);
        result.Value.Id.Should().Be(addressId);

        // Verify repository was called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithBillingAddress_ReturnsBillingAddress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Billing);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository call
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AddressType.Should().Be(AddressType.Billing);
        result.Value.Id.Should().Be(addressId);

        // Verify repository was called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDefaultAddress_ReturnsDefaultAddress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping, true); // Default address

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository call
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsDefault.Should().BeTrue();
        result.Value.Id.Should().Be(addressId);

        // Verify repository was called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAddressWithoutPhone_ReturnsAddressWithNullPhone()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var query = new GetAddressByIdQueryV1
        {
            AddressId = addressId
        };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping);
        addressDto.Phone = null; // No phone number

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository call
        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Phone.Should().BeNull();
        result.Value.Id.Should().Be(addressId);

        // Verify repository was called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByIdAsync(addressId, CancellationToken),
            Times.Once);
    }

    private static AddressDto CreateTestAddressDto(Guid addressId, Guid userId, AddressType addressType, bool isDefault = false)
    {
        return new AddressDto
        {
            Id = addressId,
            UserId = userId,
            AddressLine1 = "123 Main Street",
            AddressLine2 = "Apt 4B",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            Phone = "+15551234567",
            AddressType = addressType,
            IsDefault = isDefault
        };
    }
}
