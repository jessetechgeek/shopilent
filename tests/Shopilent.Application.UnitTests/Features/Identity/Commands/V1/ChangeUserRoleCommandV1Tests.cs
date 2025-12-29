using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Identity.Commands.ChangeUserRole.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.Errors;

namespace Shopilent.Application.UnitTests.Features.Identity.Commands.V1;

public class ChangeUserRoleCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public ChangeUserRoleCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<ChangeUserRoleCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<ChangeUserRoleCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<ChangeUserRoleCommandV1>, ChangeUserRoleCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task ChangeUserRole_WithValidData_ReturnsSuccessfulResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var command = new ChangeUserRoleCommandV1
        {
            UserId = targetUserId,
            NewRole = UserRole.Manager
        };

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .WithRole(UserRole.Customer)
            .Build();

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(currentUserId, isAdmin: true);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(targetUserId, CancellationToken))
            .ReturnsAsync(targetUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the user's role was changed
        targetUser.Role.Should().Be(UserRole.Manager);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ChangeUserRole_WithNonExistentUser_ReturnsErrorResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var command = new ChangeUserRoleCommandV1
        {
            UserId = targetUserId,
            NewRole = UserRole.Manager
        };

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(currentUserId, isAdmin: true);

        // Mock user not found
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(targetUserId, CancellationToken))
            .ReturnsAsync((User)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.NotFound(targetUserId).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }


    [Fact]
    public async Task ChangeUserRole_PromoteToAdmin_ReturnsSuccessfulResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var command = new ChangeUserRoleCommandV1
        {
            UserId = targetUserId,
            NewRole = UserRole.Admin
        };

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .WithRole(UserRole.Manager)
            .Build();

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(currentUserId, isAdmin: true);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(targetUserId, CancellationToken))
            .ReturnsAsync(targetUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the user's role was changed to Admin
        targetUser.Role.Should().Be(UserRole.Admin);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ChangeUserRole_DemoteFromAdmin_ReturnsSuccessfulResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var command = new ChangeUserRoleCommandV1
        {
            UserId = targetUserId,
            NewRole = UserRole.Customer
        };

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .WithRole(UserRole.Admin)
            .Build();

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(currentUserId, isAdmin: true);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(targetUserId, CancellationToken))
            .ReturnsAsync(targetUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the user's role was changed to Customer
        targetUser.Role.Should().Be(UserRole.Customer);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ChangeUserRole_WithSameRole_ReturnsSuccessfulResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var command = new ChangeUserRoleCommandV1
        {
            UserId = targetUserId,
            NewRole = UserRole.Manager
        };

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .WithRole(UserRole.Manager) // Already has the target role
            .Build();

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(currentUserId, isAdmin: true);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(targetUserId, CancellationToken))
            .ReturnsAsync(targetUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the user's role remains the same
        targetUser.Role.Should().Be(UserRole.Manager);

        // Verify save was NOT called since no change was made (handler optimizes for this case)
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ChangeUserRole_ChangingSelfRole_ReturnsSuccessfulResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();

        var command = new ChangeUserRoleCommandV1
        {
            UserId = currentUserId, // Changing own role
            NewRole = UserRole.Customer
        };

        var currentUser = new UserBuilder()
            .WithId(currentUserId)
            .WithRole(UserRole.Admin)
            .Build();

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(currentUserId, isAdmin: true);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(currentUserId, CancellationToken))
            .ReturnsAsync(currentUser);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the user's own role was changed
        currentUser.Role.Should().Be(UserRole.Customer);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }
}
