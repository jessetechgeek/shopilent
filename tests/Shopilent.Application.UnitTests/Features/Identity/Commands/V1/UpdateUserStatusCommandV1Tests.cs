using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Identity.Commands.UpdateUserStatus.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Identity.Commands.V1;

public class UpdateUserStatusCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public UpdateUserStatusCommandV1Tests()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateUserStatusCommandHandlerV1>());

        services.AddMediatRWithValidation();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ActivateUser_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = true };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;
        user.Deactivate(); // Deactivate first to test activation

        var currentUserId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(currentUserId);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();

        // Verify repository interactions
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeactivateUser_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = false };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;
        // User is active by default

        var currentUserId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(currentUserId);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();

        // Verify repository interactions
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = true };

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
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserTriesToDeactivateSelf_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = false };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;

        // Set the current user context to the same user being deactivated
        Fixture.SetAuthenticatedUser(userId);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.CannotDeactivateSelf.Code);

        // Verify repository interactions
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserCanActivateSelf_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = true };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;
        user.Deactivate(); // Deactivate first

        // Set the current user context to the same user being activated
        Fixture.SetAuthenticatedUser(userId);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();

        // Verify repository interactions
        Fixture.MockUserWriteRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoCurrentUserContext_StillWorksForActivation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = true };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;
        user.Deactivate();

        // Don't set authenticated user (no current user context)
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoCurrentUserContext_StillWorksForDeactivation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = false };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;

        // Don't set authenticated user (no current user context)
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = true };

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.UpdateStatusFailed");
        result.Error.Message.Should().Contain("Failed to update user status");
    }

    [Fact]
    public async Task Handle_WhenSaveEntitiesFails_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = true };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;
        user.Deactivate();

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // SaveChangesAsync returns false, but this is still considered success
        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(0);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_WithAuditInfoWhenUserContextAvailable_SetsAuditInfo(bool isActive)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var command = new UpdateUserStatusCommandV1 { Id = userId, IsActive = isActive };

        var email = Email.Create("test@example.com").Value;
        var fullName = FullName.Create("John", "Doe").Value;
        var user = User.Create(email, "hashedPassword", fullName).Value;

        if (isActive)
            user.Deactivate(); // Start deactivated to test activation
        // else user is active by default for deactivation test

        Fixture.SetAuthenticatedUser(currentUserId);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().Be(isActive);
        user.ModifiedBy.Should().Be(currentUserId);
    }
}
