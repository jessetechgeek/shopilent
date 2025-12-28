using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Identity.Commands.UpdateUser.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Identity.Commands.V1;

public class UpdateUserCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public UpdateUserCommandV1Tests()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateUserCommandHandlerV1>());

        services.AddMediatRWithValidation();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Michael",
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var existingUser = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingUser);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.User.Should().NotBeNull();
        result.Value.User.FullName.FirstName.Should().Be("John");
        result.Value.User.FullName.LastName.Should().Be("Doe");
        result.Value.User.FullName.MiddleName.Should().Be("Michael");

        // Verify repository interactions
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockUserWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe"
        };

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync((User)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.NotFound(userId).Code);

        // Verify repository interactions
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockUserWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidFirstName_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "", // Invalid empty first name
            LastName = "Doe"
        };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("Original", "Name").Value;
        var existingUser = User.Create(email, "hashedPassword", fullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.FirstNameRequired");
        result.Error.Message.Should().Contain("First name cannot be empty");

        // Verify that update was not called
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidLastName_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "" // Invalid empty last name
        };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("Original", "Name").Value;
        var existingUser = User.Create(email, "hashedPassword", fullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.LastNameRequired");
        result.Error.Message.Should().Contain("Last name cannot be empty");

        // Verify that update was not called
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidPhoneNumber_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Phone = "invalid-phone" // Invalid phone format
        };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("Original", "Name").Value;
        var existingUser = User.Create(email, "hashedPassword", fullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.InvalidPhoneFormat");

        // Verify that update was not called
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutPhoneAndMiddleName_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe"
            // Phone and MiddleName are null/empty
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var existingUser = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingUser);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.User.Should().NotBeNull();
        result.Value.User.FullName.FirstName.Should().Be("John");
        result.Value.User.FullName.LastName.Should().Be("Doe");
        result.Value.User.FullName.MiddleName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidRequestWithEmptyPhoneString_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Phone = "" // Empty phone should be treated as null
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var existingUser = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingUser);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.User.Should().NotBeNull();
        result.Value.User.FullName.FirstName.Should().Be("John");
        result.Value.User.FullName.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe"
        };

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UpdateUser.Failed");
        result.Error.Message.Should().Contain("Failed to update user");
    }

    [Theory]
    [InlineData("John", "Doe", "Michael", "+1234567890")]
    [InlineData("Jane", "Smith", null, "+9876543210")]
    [InlineData("Bob", "Johnson", "", null)]
    [InlineData("Alice", "Williams", "Marie", "")]
    public async Task Handle_ValidVariousInputCombinations_ReturnsSuccess(
        string firstName, string lastName, string middleName, string phone)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommandV1
        {
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            MiddleName = middleName,
            Phone = phone
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var existingUser = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(existingUser);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<User>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.User.Should().NotBeNull();
        result.Value.User.FullName.FirstName.Should().Be(firstName);
        result.Value.User.FullName.LastName.Should().Be(lastName);

        if (string.IsNullOrEmpty(middleName))
        {
            // FullName.Create stores empty strings as-is, not as null
            result.Value.User.FullName.MiddleName.Should().Be(middleName);
        }
        else
        {
            result.Value.User.FullName.MiddleName.Should().Be(middleName);
        }
    }
}
