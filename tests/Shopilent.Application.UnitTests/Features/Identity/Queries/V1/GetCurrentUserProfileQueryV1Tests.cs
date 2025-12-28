using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Identity.Queries.GetCurrentUserProfile.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Shipping.DTOs;

namespace Shopilent.Application.UnitTests.Features.Identity.Queries.V1;

public class GetCurrentUserProfileQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetCurrentUserProfileQueryV1Tests()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => Fixture.MockUserReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetCurrentUserProfileQueryHandlerV1>());

        services.AddMediatRWithValidation();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidUserId_ReturnsUserProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Michael",
            Phone = "+1234567890",
            Role = UserRole.Customer,
            IsActive = true,
            EmailVerified = true,
            LastLogin = DateTime.UtcNow.AddDays(-1),
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
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

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
    public async Task Handle_UserWithAddressesAndTokens_ReturnsCompleteProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

        var addresses = new List<AddressDto>
        {
            new AddressDto
            {
                Id = Guid.NewGuid(),
                AddressLine1 = "123 Main St",
                AddressLine2 = "Apt 4B",
                City = "New York",
                State = "NY",
                PostalCode = "10001",
                Country = "US",
                IsDefault = true
            }
        };

        var refreshTokens = new List<RefreshTokenDto>
        {
            new RefreshTokenDto
            {
                Id = Guid.NewGuid(),
                Token = "refresh-token-1",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                IsRevoked = false
            }
        };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.Admin,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Addresses = addresses.AsReadOnly(),
            RefreshTokens = refreshTokens.AsReadOnly(),
            FailedLoginAttempts = 2,
            LastFailedAttempt = DateTime.UtcNow.AddDays(-2),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
            LastModified = DateTime.UtcNow.AddDays(-1)
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Addresses.Should().HaveCount(1);
        result.Value.RefreshTokens.Should().HaveCount(1);
        result.Value.FailedLoginAttempts.Should().Be(2);
        result.Value.LastFailedAttempt.Should().NotBeNull();
        result.Value.CreatedBy.Should().NotBeNull();
        result.Value.ModifiedBy.Should().NotBeNull();
        result.Value.LastModified.Should().NotBeNull();

        // Verify address details
        var address = result.Value.Addresses.First();
        address.AddressLine1.Should().Be("123 Main St");
        address.AddressLine2.Should().Be("Apt 4B");
        address.City.Should().Be("New York");
        address.IsDefault.Should().BeTrue();

        // Verify refresh token details
        var refreshToken = result.Value.RefreshTokens.First();
        refreshToken.Token.Should().Be("refresh-token-1");
        refreshToken.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UserProfile.GetFailed");
        result.Error.Message.Should().Contain("Failed to retrieve user profile");
    }

    [Fact]
    public async Task Handle_QueryImplementsCachedQuery()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

        // Assert - Verify the query implements ICachedQuery
        query.CacheKey.Should().Be($"user-profile-{userId}");
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Manager)]
    public async Task Handle_DifferentUserRoles_ReturnsCorrectProfile(UserRole userRole)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_DifferentActiveStates_ReturnsCorrectProfile(bool isActive)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.Customer,
            IsActive = isActive,
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
        result.Value.IsActive.Should().Be(isActive);
    }

    [Fact]
    public async Task Handle_EmptyAddressesAndTokensLists_ReturnsProfileWithEmptyLists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserProfileQueryV1 { UserId = userId };

        var expectedUserDetail = new UserDetailDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.Customer,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Addresses = new List<AddressDto>(), // Empty list
            RefreshTokens = new List<RefreshTokenDto>(), // Empty list
            FailedLoginAttempts = 0
        };

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(userId, CancellationToken))
            .ReturnsAsync(expectedUserDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Addresses.Should().BeEmpty();
        result.Value.RefreshTokens.Should().BeEmpty();
    }
}
