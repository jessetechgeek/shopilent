using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Shipping.Commands.DeleteAddress.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Application.UnitTests.Features.Shipping.Commands.V1;

public class DeleteAddressCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public DeleteAddressCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockUserReadRepository.Object);
        services.AddTransient(sp => Fixture.MockAddressWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<DeleteAddressCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DeleteAddressCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<DeleteAddressCommandV1>, DeleteAddressCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task DeleteAddress_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new DeleteAddressCommandV1 { Id = addressId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com", IsActive = true };
        var address = CreateTestAddress(addressId, userId);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.DeleteAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.DeleteAsync(It.Is<Address>(a => a.Id == addressId), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAddress_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var addressId = Guid.NewGuid();

        var command = new DeleteAddressCommandV1 { Id = addressId };

        // Don't set authenticated user (CurrentUserContext.UserId will be null)

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();

        // Verify delete was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAddress_WithNonExistentUser_ReturnsUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new DeleteAddressCommandV1 { Id = addressId };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock user not found
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync((UserDto)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("User.NotFound");

        // Verify delete was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAddress_WithNonExistentAddress_ReturnsAddressNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new DeleteAddressCommandV1 { Id = addressId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com", IsActive = true };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        // Mock address not found
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync((Address)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("Address.NotFound");

        // Verify delete was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAddress_WithAddressBelongingToOtherUser_ReturnsAddressNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new DeleteAddressCommandV1 { Id = addressId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com", IsActive = true };
        var address = CreateTestAddress(addressId, otherUserId); // Address belongs to other user

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("Address.NotFound");

        // Verify delete was not called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Address>(), CancellationToken),
            Times.Never);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAddress_WithShippingAddress_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new DeleteAddressCommandV1 { Id = addressId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com", IsActive = true };
        var address = CreateTestAddress(addressId, userId, AddressType.Shipping);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.DeleteAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.DeleteAsync(It.Is<Address>(a => a.Id == addressId && a.AddressType == AddressType.Shipping),
                CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAddress_WithBillingAddress_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var command = new DeleteAddressCommandV1 { Id = addressId };

        var userDto = new UserDto { Id = userId, Email = "test@example.com", IsActive = true };
        var address = CreateTestAddress(addressId, userId, AddressType.Billing);

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(userDto);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByIdAsync(addressId, CancellationToken))
            .ReturnsAsync(address);

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.DeleteAsync(It.IsAny<Address>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockAddressWriteRepository.Verify(
            repo => repo.DeleteAsync(It.Is<Address>(a => a.Id == addressId && a.AddressType == AddressType.Billing),
                CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    private static Address CreateTestAddress(Guid addressId, Guid userId,
        AddressType addressType = AddressType.Shipping)
    {
        return new AddressBuilder()
            .WithId(addressId)
            .WithUserId(userId)
            .WithStreetAddress("123 Main Street")
            .WithLocation("New York", "NY", "10001", "US")
            .WithAddressType(addressType)
            .Build();
    }
}
