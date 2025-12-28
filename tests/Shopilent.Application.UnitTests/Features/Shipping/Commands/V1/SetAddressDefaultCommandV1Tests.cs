using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Shipping.Commands.SetAddressDefault.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Application.UnitTests.Features.Shipping.Commands.V1;

public class SetAddressDefaultCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public SetAddressDefaultCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAddressWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockAddressReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<SetAddressDefaultCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<SetAddressDefaultCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<SetAddressDefaultCommandV1>, SetAddressDefaultCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task SetAddressDefault_WithValidRequest_ReturnsUpdatedAddress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var otherAddressId = Guid.NewGuid();

        var command = new SetAddressDefaultCommandV1
        {
            AddressId = addressId
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, false);
        var otherAddress = CreateTestAddress(otherAddressId, userId, AddressType.Shipping, true); // Currently default
        var addresses = new List<Address> { address, otherAddress };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping, true);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.AddressType.Should().Be(AddressType.Shipping);

        // Verify update was called for both addresses (unset old default, set new default)
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Exactly(2));

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SetAddressDefault_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var addressId = Guid.NewGuid();

        var command = new SetAddressDefaultCommandV1
        {
            AddressId = addressId
        };

        // Don't set authenticated user

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Address.Unauthorized");

        // Verify update was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetAddressDefault_WithNonExistentAddress_ReturnsAddressNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new SetAddressDefaultCommandV1
        {
            AddressId = addressId
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock address not found
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync((Address)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("Address.NotFound");

        // Verify update was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetAddressDefault_WithAddressBelongingToOtherUser_ReturnsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new SetAddressDefaultCommandV1
        {
            AddressId = addressId
        };

        var address = CreateTestAddress(addressId, otherUserId, AddressType.Shipping, false); // Address belongs to other user

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Address.NotOwned");

        // Verify update was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetAddressDefault_WithAlreadyDefaultAddress_ReturnsAddressWithoutChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new SetAddressDefaultCommandV1
        {
            AddressId = addressId
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, true); // Already default
        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping, true);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.IsDefault.Should().BeTrue();

        // Verify GetByUserIdAsync was not called since address was already default
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.GetByUserIdAsync(It.IsAny<Guid>(), CancellationToken),
            Times.Never);

        // Verify update was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetAddressDefault_WithShippingAddress_UnsetsOtherShippingDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var otherShippingAddressId = Guid.NewGuid();
        var billingAddressId = Guid.NewGuid();

        var command = new SetAddressDefaultCommandV1
        {
            AddressId = addressId
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, false);
        var otherShippingAddress = CreateTestAddress(otherShippingAddressId, userId, AddressType.Shipping, true); // Currently default shipping
        var billingAddress = CreateTestAddress(billingAddressId, userId, AddressType.Billing, true); // Should remain default billing
        var addresses = new List<Address> { address, otherShippingAddress, billingAddress };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Shipping, true);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.AddressType.Should().Be(AddressType.Shipping);

        // Verify update was called twice: once to unset old default, once to set new default
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Exactly(2));

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SetAddressDefault_WithBillingAddress_UnsetsOtherBillingDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var otherBillingAddressId = Guid.NewGuid();
        var shippingAddressId = Guid.NewGuid();

        var command = new SetAddressDefaultCommandV1
        {
            AddressId = addressId
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Billing, false);
        var otherBillingAddress = CreateTestAddress(otherBillingAddressId, userId, AddressType.Billing, true); // Currently default billing
        var shippingAddress = CreateTestAddress(shippingAddressId, userId, AddressType.Shipping, true); // Should remain default shipping
        var addresses = new List<Address> { address, otherBillingAddress, shippingAddress };

        var addressDto = CreateTestAddressDto(addressId, userId, AddressType.Billing, true);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(addresses);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(addressDto);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.AddressType.Should().Be(AddressType.Billing);

        // Verify update was called twice: once to unset old default, once to set new default
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Exactly(2));

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    private static Address CreateTestAddress(Guid addressId, Guid userId, AddressType addressType, bool isDefault)
    {
        var builder = new AddressBuilder()
            .WithId(addressId)
            .WithUserId(userId)
            .WithStreetAddress("123 Main Street")
            .WithLocation("New York", "NY", "10001", "US")
            .WithAddressType(addressType);

        if (isDefault)
            builder = builder.IsDefault();

        return builder.Build();
    }

    private static AddressDto CreateTestAddressDto(Guid addressId, Guid userId, AddressType addressType, bool isDefault)
    {
        return new AddressDto
        {
            Id = addressId,
            UserId = userId,
            AddressLine1 = "123 Main Street",
            AddressLine2 = null,
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            Phone = null,
            AddressType = addressType,
            IsDefault = isDefault
        };
    }
}
