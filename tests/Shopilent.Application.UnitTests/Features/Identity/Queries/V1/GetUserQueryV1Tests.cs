using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Identity.Queries.GetUser.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Shipping.DTOs;

namespace Shopilent.Application.UnitTests.Features.Identity.Queries.V1;

public class GetUserQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetUserQueryV1Tests()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => Fixture.MockUserReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetUserQueryHandlerV1>());

        services.AddMediatRWithValidation();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidUserId_ReturnsUserDetail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "john.doe@example.com",
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Michael",
            Phone = "+1234567890",
            Role = UserRole.Customer,
            IsActive = true,
            EmailVerified = true,
            LastLogin = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            UpdatedAt = DateTime.UtcNow.AddDays(-10),
            Addresses = new List<AddressDto>(),
            RefreshTokens = new List<RefreshTokenDto>(),
            FailedLoginAttempts = 0,
            LastFailedAttempt = null,
            CreatedBy = null,
            ModifiedBy = null,
            LastModified = null
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(expectedUserDetail.Id);
        result.Value.Email.Should().Be(expectedUserDetail.Email);
        result.Value.FirstName.Should().Be(expectedUserDetail.FirstName);
        result.Value.LastName.Should().Be(expectedUserDetail.LastName);
        result.Value.MiddleName.Should().Be(expectedUserDetail.MiddleName);
        result.Value.Phone.Should().Be(expectedUserDetail.Phone);
        result.Value.Role.Should().Be(expectedUserDetail.Role);
        result.Value.IsActive.Should().Be(expectedUserDetail.IsActive);
        result.Value.EmailVerified.Should().Be(expectedUserDetail.EmailVerified);

        // Verify repository interaction
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync((UserDetailDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.NotFound(userId).Code);

        // Verify repository interaction
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AdminUserWithCompleteProfile_ReturnsCompleteDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        var addresses = new List<AddressDto>
        {
            new AddressDto
            {
                Id = Guid.NewGuid(),
                AddressLine1 = "456 Admin Ave",
                AddressLine2 = "Suite 789",
                City = "Washington",
                State = "DC",
                PostalCode = "20001",
                Country = "US",
                IsDefault = true
            },
            new AddressDto
            {
                Id = Guid.NewGuid(),
                AddressLine1 = "789 Secondary St",
                City = "Arlington",
                State = "VA",
                PostalCode = "22201",
                Country = "US",
                IsDefault = false
            }
        };

        var refreshTokens = new List<RefreshTokenDto>
        {
            new RefreshTokenDto
            {
                Id = Guid.NewGuid(),
                Token = "active-refresh-token",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsRevoked = false
            },
            new RefreshTokenDto
            {
                Id = Guid.NewGuid(),
                Token = "revoked-refresh-token",
                ExpiresAt = DateTime.UtcNow.AddDays(15),
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                IsRevoked = true
            }
        };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "admin@company.com",
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Michael",
            Phone = "+1987654321",
            Role = UserRole.Admin,
            IsActive = true,
            EmailVerified = true,
            LastLogin = DateTime.UtcNow.AddHours(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-120),
            UpdatedAt = DateTime.UtcNow.AddDays(-3),
            Addresses = addresses.AsReadOnly(),
            RefreshTokens = refreshTokens.AsReadOnly(),
            FailedLoginAttempts = 1,
            LastFailedAttempt = DateTime.UtcNow.AddDays(-7),
            CreatedBy = creatorId,
            ModifiedBy = modifierId,
            LastModified = DateTime.UtcNow.AddDays(-3)
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Role.Should().Be(UserRole.Admin);
        result.Value.Addresses.Should().HaveCount(2);
        result.Value.RefreshTokens.Should().HaveCount(2);
        result.Value.FailedLoginAttempts.Should().Be(1);
        result.Value.LastFailedAttempt.Should().NotBeNull();
        result.Value.CreatedBy.Should().Be(creatorId);
        result.Value.ModifiedBy.Should().Be(modifierId);
        result.Value.LastModified.Should().NotBeNull();

        // Verify address details
        var defaultAddress = result.Value.Addresses.First(a => a.IsDefault);
        defaultAddress.AddressLine1.Should().Be("456 Admin Ave");
        defaultAddress.AddressLine2.Should().Be("Suite 789");
        defaultAddress.City.Should().Be("Washington");

        var secondaryAddress = result.Value.Addresses.First(a => !a.IsDefault);
        secondaryAddress.AddressLine1.Should().Be("789 Secondary St");
        secondaryAddress.City.Should().Be("Arlington");

        // Verify refresh token details
        var activeToken = result.Value.RefreshTokens.First(t => !t.IsRevoked);
        activeToken.Token.Should().Be("active-refresh-token");
        activeToken.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var revokedToken = result.Value.RefreshTokens.First(t => t.IsRevoked);
        revokedToken.Token.Should().Be("revoked-refresh-token");
        revokedToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InactiveUserWithFailedLogins_ReturnsCorrectDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "inactive.user@example.com",
            FirstName = "Inactive",
            LastName = "User",
            Role = UserRole.Customer,
            IsActive = false,
            EmailVerified = false,
            LastLogin = DateTime.UtcNow.AddDays(-30),
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            UpdatedAt = DateTime.UtcNow.AddDays(-20),
            Addresses = new List<AddressDto>(),
            RefreshTokens = new List<RefreshTokenDto>(),
            FailedLoginAttempts = 5,
            LastFailedAttempt = DateTime.UtcNow.AddHours(-6),
            CreatedBy = null,
            ModifiedBy = null,
            LastModified = null
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsActive.Should().BeFalse();
        result.Value.EmailVerified.Should().BeFalse();
        result.Value.FailedLoginAttempts.Should().Be(5);
        result.Value.LastFailedAttempt.Should().NotBeNull();
        result.Value.Addresses.Should().BeEmpty();
        result.Value.RefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ThrowsAsync(new Exception("Database connection error"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.GetDetailFailed");
        result.Error.Message.Should().Contain("Failed to retrieve user detail");
        result.Error.Message.Should().Contain("Database connection error");
    }

    [Fact]
    public async Task Handle_QueryImplementsCachedQuery()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        // Assert - Verify the query implements ICachedQuery
        query.CacheKey.Should().Be($"user-detail-{userId}");
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Manager)]
    public async Task Handle_DifferentUserRoles_ReturnsCorrectRole(UserRole userRole)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = userRole,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Addresses = new List<AddressDto>(),
            RefreshTokens = new List<RefreshTokenDto>(),
            FailedLoginAttempts = 0
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(userRole);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(10)]
    public async Task Handle_DifferentFailedLoginAttempts_ReturnsCorrectCount(int failedAttempts)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        var lastFailedAttempt = failedAttempts > 0 ? DateTime.UtcNow.AddHours(-2) : (DateTime?)null;

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Customer,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Addresses = new List<AddressDto>(),
            RefreshTokens = new List<RefreshTokenDto>(),
            FailedLoginAttempts = failedAttempts,
            LastFailedAttempt = lastFailedAttempt
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FailedLoginAttempts.Should().Be(failedAttempts);
        result.Value.LastFailedAttempt.Should().Be(lastFailedAttempt);
    }

    [Fact]
    public async Task Handle_UserWithNullOptionalFields_ReturnsUserWithNullFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQueryV1 { Id = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "minimal@example.com",
            FirstName = "Minimal",
            LastName = "User",
            MiddleName = null, // Null middle name
            Phone = null, // Null phone
            Role = UserRole.Customer,
            IsActive = true,
            EmailVerified = true,
            LastLogin = null, // Never logged in
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Addresses = new List<AddressDto>(),
            RefreshTokens = new List<RefreshTokenDto>(),
            FailedLoginAttempts = 0,
            LastFailedAttempt = null,
            CreatedBy = null,
            ModifiedBy = null,
            LastModified = null
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MiddleName.Should().BeNull();
        result.Value.Phone.Should().BeNull();
        result.Value.LastLogin.Should().BeNull();
        result.Value.LastFailedAttempt.Should().BeNull();
        result.Value.CreatedBy.Should().BeNull();
        result.Value.ModifiedBy.Should().BeNull();
        result.Value.LastModified.Should().BeNull();
    }
}
