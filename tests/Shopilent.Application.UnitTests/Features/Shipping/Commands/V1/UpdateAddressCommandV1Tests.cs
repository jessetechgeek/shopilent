using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Shipping.Commands.UpdateAddress.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Application.UnitTests.Features.Shipping.Commands.V1;

public class UpdateAddressCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public UpdateAddressCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAddressWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateAddressCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateAddressCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateAddressCommandV1>, UpdateAddressCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task UpdateAddress_WithValidRequest_ReturnsUpdatedAddress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "456 Oak Avenue",
            AddressLine2 = "Suite 100",
            City = "Los Angeles",
            State = "CA",
            PostalCode = "90210",
            Country = "US",
            Phone = "+1-555-987-6543",
            AddressType = AddressType.Billing
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, false);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.AddressLine1.Should().Be(command.AddressLine1);
        result.Value.AddressLine2.Should().Be(command.AddressLine2);
        result.Value.City.Should().Be(command.City);
        result.Value.State.Should().Be(command.State);
        result.Value.PostalCode.Should().Be(command.PostalCode);
        result.Value.Country.Should().Be(command.Country);
        result.Value.Phone.Should().Be("+15559876543"); // PhoneNumber normalizes format
        result.Value.AddressType.Should().Be(command.AddressType);
        result.Value.IsDefault.Should().BeFalse(); // Preserve original default status

        // Verify update was called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.Is<Address>(a => a.Id == addressId), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAddress_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            AddressType = AddressType.Shipping
        };

        // Don't set authenticated user

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();

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
    public async Task UpdateAddress_WithNonExistentAddress_ReturnsAddressNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            AddressType = AddressType.Shipping
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
    public async Task UpdateAddress_WithAddressBelongingToOtherUser_ReturnsAddressNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            AddressType = AddressType.Shipping
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
    public async Task UpdateAddress_WithMinimalRequiredFields_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "100 Simple St",
            City = "Austin",
            State = "TX",
            PostalCode = "78701",
            Country = "US",
            AddressType = AddressType.Shipping
            // No AddressLine2, Phone - optional fields
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, false);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.AddressLine1.Should().Be("100 Simple St");
        result.Value.AddressLine2.Should().BeNull();
        result.Value.City.Should().Be("Austin");
        result.Value.State.Should().Be("TX");
        result.Value.PostalCode.Should().Be("78701");
        result.Value.Country.Should().Be("US");
        result.Value.Phone.Should().BeNull();
        result.Value.AddressType.Should().Be(AddressType.Shipping);

        // Verify update was called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.Is<Address>(a => a.Id == addressId), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAddress_WithPhoneNumber_UpdatesPhoneSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "321 Elm Street",
            City = "Houston",
            State = "TX",
            PostalCode = "77001",
            Country = "US",
            Phone = "+1-713-555-0123",
            AddressType = AddressType.Shipping
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, false);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.Phone.Should().Be("+17135550123"); // PhoneNumber normalizes format
        result.Value.AddressType.Should().Be(AddressType.Shipping);

        // Verify update was called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.Is<Address>(a => a.Id == addressId), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAddress_WithAddressTypeChange_UpdatesTypeSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "789 Pine Street",
            City = "Chicago",
            State = "IL",
            PostalCode = "60601",
            Country = "US",
            AddressType = AddressType.Billing // Changed from Shipping to Billing
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, false); // Originally Shipping

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(addressId);
        result.Value.AddressType.Should().Be(AddressType.Billing);
        result.Value.AddressLine1.Should().Be("789 Pine Street");
        result.Value.City.Should().Be("Chicago");
        result.Value.State.Should().Be("IL");

        // Verify update was called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.Is<Address>(a => a.Id == addressId), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAddress_WithInvalidPhoneNumber_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new UpdateAddressCommandV1
        {
            Id = addressId,
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            Phone = "invalid-phone-format", // Invalid phone number
            AddressType = AddressType.Shipping
        };

        var address = CreateTestAddress(addressId, userId, AddressType.Shipping, false);

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
        // The exact error depends on phone number validation implementation

        // Verify update was not called due to validation error
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
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
}
