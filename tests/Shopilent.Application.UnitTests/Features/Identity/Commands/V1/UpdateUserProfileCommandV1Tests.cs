using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Identity.Commands.UpdateUserProfile.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Identity.Commands.V1;

public class UpdateUserProfileCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public UpdateUserProfileCommandV1Tests()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateUserProfileCommandHandlerV1>());

        services.AddMediatRWithValidation();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", originalFullName).Value;
        var userId = user.Id; // Use the actual user's ID
        var currentUserId = Guid.NewGuid();

        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Michael",
            Phone = "+1234567890"
        };

        Fixture.SetAuthenticatedUser(currentUserId);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
        result.Value.MiddleName.Should().Be("Michael");
        result.Value.Phone.Should().Be("+1234567890");
        result.Value.UpdatedAt.Should().BeOnOrBefore(DateTime.UtcNow);

        // Verify user was updated
        user.FullName.FirstName.Should().Be("John");
        user.FullName.LastName.Should().Be("Doe");
        user.FullName.MiddleName.Should().Be("Michael");
        user.Phone.Value.Should().Be("+1234567890");
        user.ModifiedBy.Should().Be(currentUserId);

        // Verify repository interactions
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
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

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidFirstName_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "", // Invalid empty first name
            LastName = "Doe"
        };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.FirstNameRequired");
        result.Error.Message.Should().Contain("First name cannot be empty");

        // Verify that save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidLastName_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "" // Invalid empty last name
        };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.LastNameRequired");
        result.Error.Message.Should().Contain("Last name cannot be empty");

        // Verify that save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidPhoneNumber_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Phone = "invalid-phone" // Invalid phone format
        };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.InvalidPhoneFormat");

        // Verify that save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutPhoneAndMiddleName_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe"
            // Phone and MiddleName are null
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
        result.Value.MiddleName.Should().BeNull();
        result.Value.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidRequestWithEmptyPhoneString_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Phone = "" // Empty phone should be treated as null
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoCurrentUserContext_StillWorksButNoAuditInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe"
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", originalFullName).Value;

        // Don't set authenticated user (no current user context)
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");

        // ModifiedBy should not be set when no current user context
        user.ModifiedBy.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
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
        result.Error.Code.Should().Be("User.UpdateProfileFailed");
        result.Error.Message.Should().Contain("Failed to update user profile");
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
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            MiddleName = middleName,
            Phone = phone
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FirstName.Should().Be(firstName);
        result.Value.LastName.Should().Be(lastName);

        if (string.IsNullOrEmpty(middleName))
        {
            // FullName.Create stores empty strings as-is, not as null
            result.Value.MiddleName.Should().Be(middleName);
        }
        else
        {
            result.Value.MiddleName.Should().Be(middleName);
        }

        if (string.IsNullOrEmpty(phone))
        {
            result.Value.Phone.Should().BeNull();
        }
        else
        {
            result.Value.Phone.Should().Be(phone);
        }
    }

    [Fact]
    public async Task Handle_ResponseTimestamp_IsReasonablyCurrent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserProfileCommandV1
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe"
        };

        var email = Email.Create("test@example.com").Value;
        var originalFullName = FullName.Create("Original", "Name").Value;
        var user = User.Create(email, "hashedPassword", originalFullName).Value;

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ReturnsAsync(1);

        var beforeExecution = DateTime.UtcNow;

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        var afterExecution = DateTime.UtcNow;

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        result.Value.UpdatedAt.Should().BeOnOrBefore(afterExecution);
    }
}
