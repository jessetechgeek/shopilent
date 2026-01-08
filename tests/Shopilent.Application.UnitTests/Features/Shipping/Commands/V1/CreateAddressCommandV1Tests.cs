using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Shipping.Commands.CreateAddress.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Application.UnitTests.Features.Shipping.Commands.V1;

public class CreateAddressCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public CreateAddressCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserReadRepository.Object);
        services.AddTransient(sp => Fixture.MockAddressWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<CreateAddressCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<CreateAddressCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<CreateAddressCommandV1>, CreateAddressCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task CreateAddress_WithValidData_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "123 Main Street",
            AddressLine2 = "Apt 4B",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            Phone = "+1-555-123-4567",
            AddressType = AddressType.Shipping,
            IsDefault = false
        };

        var user = new UserBuilder().WithId(userId).BuildDto();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock address writer methods
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(new List<Address>());

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Address>(), CancellationToken))
            .ReturnsAsync((Address addr, CancellationToken _) => addr);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AddressLine1.Should().Be(command.AddressLine1);
        result.Value.AddressLine2.Should().Be(command.AddressLine2);
        result.Value.City.Should().Be(command.City);
        result.Value.State.Should().Be(command.State);
        result.Value.PostalCode.Should().Be(command.PostalCode);
        result.Value.Country.Should().Be(command.Country);
        result.Value.Phone.Should().Be("+15551234567"); // PhoneNumber normalizes format
        result.Value.AddressType.Should().Be(command.AddressType);
        result.Value.IsDefault.Should().Be(command.IsDefault);
        result.Value.UserId.Should().Be(userId);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateAddress_WithUnauthenticatedUser_ReturnsErrorResult()
    {
        // Arrange
        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US"
        };

        // Don't set authenticated user

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CreateAddress.Unauthorized");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task CreateAddress_WithNonExistentUser_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US"
        };

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
        result.Error.Code.Should().Be("CreateAddress.UserNotFound");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task CreateAddress_WithDefaultAddress_SetsAsDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "456 Oak Avenue",
            City = "Los Angeles",
            State = "CA",
            PostalCode = "90210",
            Country = "US",
            AddressType = AddressType.Billing,
            IsDefault = true
        };

        var user = new UserBuilder().WithId(userId).BuildDto();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock address writer methods
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(new List<Address>());

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Address>(), CancellationToken))
            .ReturnsAsync((Address addr, CancellationToken _) => addr);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsDefault.Should().BeTrue();
        result.Value.AddressType.Should().Be(AddressType.Billing);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateAddress_WithBillingType_CreatesCorrectAddressType()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "789 Pine Street",
            City = "Chicago",
            State = "IL",
            PostalCode = "60601",
            Country = "US",
            AddressType = AddressType.Billing,
            IsDefault = false
        };

        var user = new UserBuilder().WithId(userId).BuildDto();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock address writer methods
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(new List<Address>());

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Address>(), CancellationToken))
            .ReturnsAsync((Address addr, CancellationToken _) => addr);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AddressType.Should().Be(AddressType.Billing);
        result.Value.AddressLine1.Should().Be("789 Pine Street");
        result.Value.City.Should().Be("Chicago");
        result.Value.State.Should().Be("IL");

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateAddress_WithPhoneNumber_IncludesPhoneInResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "321 Elm Street",
            City = "Houston",
            State = "TX",
            PostalCode = "77001",
            Country = "US",
            Phone = "+1-713-555-0123",
            AddressType = AddressType.Shipping
        };

        var user = new UserBuilder().WithId(userId).BuildDto();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock address writer methods
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(new List<Address>());

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Address>(), CancellationToken))
            .ReturnsAsync((Address addr, CancellationToken _) => addr);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Phone.Should().Be("+17135550123"); // PhoneNumber normalizes format
        result.Value.AddressType.Should().Be(AddressType.Shipping);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateAddress_WithMinimalRequiredFields_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "100 Simple St",
            City = "Austin",
            State = "TX",
            PostalCode = "78701",
            Country = "US"
            // No AddressLine2, Phone - optional fields
            // AddressType will default to Shipping
            // IsDefault will default to false
        };

        var user = new UserBuilder().WithId(userId).BuildDto();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock address writer methods
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(new List<Address>());

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Address>(), CancellationToken))
            .ReturnsAsync((Address addr, CancellationToken _) => addr);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AddressLine1.Should().Be("100 Simple St");
        result.Value.AddressLine2.Should().BeNull();
        result.Value.City.Should().Be("Austin");
        result.Value.State.Should().Be("TX");
        result.Value.PostalCode.Should().Be("78701");
        result.Value.Country.Should().Be("US");
        result.Value.Phone.Should().BeNull();
        result.Value.AddressType.Should().Be(AddressType.Shipping); // Default
        result.Value.IsDefault.Should().BeFalse(); // Default

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateAddress_WithInvalidPhoneNumber_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateAddressCommandV1
        {
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US",
            Phone = "invalid-phone-format" // Invalid phone number
        };

        var user = new UserBuilder().WithId(userId).BuildDto();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock address writer methods
        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(new List<Address>());

        Fixture.MockAddressWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Address>(), CancellationToken))
            .ReturnsAsync((Address addr, CancellationToken _) => addr);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The exact error depends on phone number validation implementation

        // Verify save was not called due to validation error
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }
}
