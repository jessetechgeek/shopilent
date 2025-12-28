using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Shipping.Queries.GetUserAddresses.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Application.UnitTests.Features.Shipping.Queries.V1;

public class GetUserAddressesQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetUserAddressesQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAddressReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetUserAddressesQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GetUserAddressesQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_WithValidUserId_ReturnsUserAddresses()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserAddressesQueryV1 { UserId = userId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com" };
        var addresses = new List<AddressDto>
        {
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Shipping, true), // Default shipping
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Billing, true), // Default billing
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Shipping, false) // Non-default shipping
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);

        // Verify all addresses belong to the user
        result.Value.Should().AllSatisfy(address => address.UserId.Should().Be(userId));

        // Verify we have the expected address types
        result.Value.Should().ContainSingle(a => a.AddressType == AddressType.Shipping && a.IsDefault);
        result.Value.Should().ContainSingle(a => a.AddressType == AddressType.Billing && a.IsDefault);
        result.Value.Should().ContainSingle(a => a.AddressType == AddressType.Shipping && !a.IsDefault);

        // Verify repository calls
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ReturnsUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserAddressesQueryV1 { UserId = userId };

        // Mock user not found
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync((UserDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.NotFound");

        // Verify user repository was called
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        // Verify address repository was not called
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(It.IsAny<Guid>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithUserWithoutAddresses_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserAddressesQueryV1 { UserId = userId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com" };
        var emptyAddresses = new List<AddressDto>();

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(emptyAddresses);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();

        // Verify repository calls
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithUserHavingOnlyShippingAddresses_ReturnsOnlyShippingAddresses()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserAddressesQueryV1 { UserId = userId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com" };
        var addresses = new List<AddressDto>
        {
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Shipping, true), // Default shipping
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Shipping, false) // Non-default shipping
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(address => address.AddressType.Should().Be(AddressType.Shipping));

        // Verify we have one default and one non-default
        result.Value.Should().ContainSingle(a => a.IsDefault);
        result.Value.Should().ContainSingle(a => !a.IsDefault);

        // Verify repository calls
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithUserHavingOnlyBillingAddresses_ReturnsOnlyBillingAddresses()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserAddressesQueryV1 { UserId = userId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com" };
        var addresses = new List<AddressDto>
        {
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Billing, true), // Default billing
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Billing, false) // Non-default billing
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(address => address.AddressType.Should().Be(AddressType.Billing));

        // Verify we have one default and one non-default
        result.Value.Should().ContainSingle(a => a.IsDefault);
        result.Value.Should().ContainSingle(a => !a.IsDefault);

        // Verify repository calls
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithUserHavingMixedAddressTypes_ReturnsAllAddresses()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserAddressesQueryV1 { UserId = userId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com" };
        var addresses = new List<AddressDto>
        {
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Shipping, true, "123 Main St", "New York"),
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Billing, true, "456 Oak Ave", "Los Angeles"),
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Shipping, false, "789 Pine St", "Chicago"),
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Billing, false, "321 Elm St", "Houston")
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(4);

        // Verify we have both address types
        result.Value.Where(a => a.AddressType == AddressType.Shipping).Should().HaveCount(2);
        result.Value.Where(a => a.AddressType == AddressType.Billing).Should().HaveCount(2);

        // Verify we have default addresses for each type
        result.Value.Should().ContainSingle(a => a.AddressType == AddressType.Shipping && a.IsDefault);
        result.Value.Should().ContainSingle(a => a.AddressType == AddressType.Billing && a.IsDefault);

        // Verify repository calls
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAddressesHavingPhoneNumbers_ReturnsAddressesWithPhones()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserAddressesQueryV1 { UserId = userId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com" };
        var addresses = new List<AddressDto>
        {
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Shipping, true, "123 Main St", "New York",
                "+15551234567"),
            CreateTestAddressDto(Guid.NewGuid(), userId, AddressType.Billing, false, "456 Oak Ave", "Los Angeles",
                null) // No phone
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);

        var shippingAddress = result.Value.First(a => a.AddressType == AddressType.Shipping);
        var billingAddress = result.Value.First(a => a.AddressType == AddressType.Billing);

        shippingAddress.Phone.Should().Be("+15551234567");
        billingAddress.Phone.Should().BeNull();

        // Verify repository calls
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    private static AddressDto CreateTestAddressDto(
        Guid addressId,
        Guid userId,
        AddressType addressType,
        bool isDefault = false,
        string addressLine1 = "123 Main Street",
        string city = "New York",
        string phone = "+15551234567")
    {
        return new AddressDto
        {
            Id = addressId,
            UserId = userId,
            AddressLine1 = addressLine1,
            AddressLine2 = "Apt 4B",
            City = city,
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            Phone = phone,
            AddressType = addressType,
            IsDefault = isDefault
        };
    }
}
