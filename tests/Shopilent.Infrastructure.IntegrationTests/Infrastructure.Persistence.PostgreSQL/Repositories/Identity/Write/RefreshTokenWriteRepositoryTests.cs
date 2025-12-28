using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Identity.Write;

[Collection("IntegrationTests")]
public class RefreshTokenWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;

    public RefreshTokenWriteRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidRefreshToken_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"test_token_{Guid.NewGuid():N}")
            .WithIpAddress("192.168.1.100")
            .WithUserAgent("Test User Agent")
            .Build();

        // Act - Save user with RefreshToken (EF Core will cascade save the RefreshToken)
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.RefreshTokenReader.GetByIdAsync(refreshToken.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(refreshToken.Id);
        result.UserId.Should().Be(user.Id);
        result.Token.Should().Be(refreshToken.Token);
        result.ExpiresAt.Should().BeCloseTo(refreshToken.ExpiresAt, TimeSpan.FromMilliseconds(100));
        result.IssuedAt.Should().BeCloseTo(refreshToken.IssuedAt, TimeSpan.FromMilliseconds(100));
        result.IsRevoked.Should().BeFalse();
        result.IpAddress.Should().Be("192.168.1.100");
        result.UserAgent.Should().Be("Test User Agent");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddAsync_RefreshTokenWithMinimalInfo_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"minimal_token_{Guid.NewGuid():N}")
            .WithIpAddress(null)
            .WithUserAgent(null)
            .Build();

        // Act - Save user with RefreshToken (EF Core will cascade save the RefreshToken)
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.RefreshTokenReader.GetByIdAsync(refreshToken.Id);
        result.Should().NotBeNull();
        result!.Token.Should().Be(refreshToken.Token);
        result.IpAddress.Should().BeNullOrEmpty();
        result.UserAgent.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ExistingRefreshToken_ShouldModifyToken()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"original_token_{Guid.NewGuid():N}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(refreshToken).State = EntityState.Detached;

        // Act - Load fresh entity and revoke
        var existingToken = await _unitOfWork.RefreshTokenWriter.GetByIdAsync(refreshToken.Id);
        existingToken!.Revoke("Test revocation");

        await _unitOfWork.RefreshTokenWriter.UpdateAsync(existingToken);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedToken = await _unitOfWork.RefreshTokenReader.GetByIdAsync(refreshToken.Id);
        updatedToken.Should().NotBeNull();
        updatedToken!.IsRevoked.Should().BeTrue();
        updatedToken.RevokedReason.Should().Be("Test revocation");
    }

    [Fact]
    public async Task DeleteAsync_ExistingRefreshToken_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"delete_token_{Guid.NewGuid():N}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _unitOfWork.RefreshTokenWriter.DeleteAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.RefreshTokenReader.GetByIdAsync(refreshToken.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingRefreshToken_ShouldReturnRefreshToken()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"get_token_{Guid.NewGuid():N}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetByIdAsync(refreshToken.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(refreshToken.Id);
        result.Token.Should().Be(refreshToken.Token);
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentRefreshToken_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTokenAsync_ExistingToken_ShouldReturnRefreshToken()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var tokenValue = $"lookup_token_{Guid.NewGuid():N}";
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken(tokenValue)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetByTokenAsync(tokenValue);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be(tokenValue);
        result.Id.Should().Be(refreshToken.Id);
    }

    [Fact]
    public async Task GetByTokenAsync_NonExistentToken_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentToken = "non_existent_token";

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetByTokenAsync(nonExistentToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ExistingUser_ShouldReturnUserTokens()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken1 = RefreshTokenBuilder.ForUser(user)
            .WithToken($"user_token1_{Guid.NewGuid():N}")
            .Build();
        var refreshToken2 = RefreshTokenBuilder.ForUser(user)
            .WithToken($"user_token2_{Guid.NewGuid():N}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(refreshToken1);
        await _unitOfWork.RefreshTokenWriter.AddAsync(refreshToken2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(token => token.UserId == user.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_NonExistentUser_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetByUserIdAsync(nonExistentUserId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveTokensAsync_ExistingActiveTokens_ShouldReturnOnlyActiveTokens()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        var activeToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"active_write_{Guid.NewGuid():N}")
            .Build();

        var expiredToken = RefreshTokenBuilder.CreateExpiredToken(user);

        var revokedToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"revoked_write_{Guid.NewGuid():N}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(activeToken);
        await _unitOfWork.RefreshTokenWriter.AddAsync(expiredToken);
        await _unitOfWork.RefreshTokenWriter.AddAsync(revokedToken);
        await _unitOfWork.SaveChangesAsync();

        // Revoke one token
        revokedToken.Revoke("Test revocation");
        await _unitOfWork.RefreshTokenWriter.UpdateAsync(revokedToken);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetActiveTokensAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Token.Should().Be(activeToken.Token);
    }

    [Fact]
    public async Task GetActiveTokensAsync_NoActiveTokens_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var expiredToken = RefreshTokenBuilder.CreateExpiredToken(user);

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(expiredToken);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.RefreshTokenWriter.GetActiveTokensAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OptimisticConcurrency_ConcurrentUpdates_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Get the current mock user ID and create a matching user in the database
        var currentUserService = GetService<ICurrentUserContext>();
        var mockUserId = currentUserService.UserId;

        if (mockUserId.HasValue)
        {
            // Create a user with the same ID as the mock user context
            var testUser = new UserBuilder()
                .WithEmail($"mock-user-{mockUserId:N}@example.com")
                .WithUsername($"mock-user-{mockUserId:N}")
                .Build();

            // Manually set the user ID to match the mock (using reflection)
            var idProperty = typeof(Domain.Common.Entity).GetProperty("Id");
            idProperty?.SetValue(testUser, mockUserId.Value);

            // Save the test user so it exists for the audit interceptor
            await _userWriteRepository.AddAsync(testUser);
            await _unitOfWork.SaveChangesAsync();
        }

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"concurrent_token_{Guid.NewGuid():N}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Act - Simulate concurrent access with two service scopes
        // Create separate service scopes to simulate true concurrent access
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var token1 = await unitOfWork1.RefreshTokenWriter.GetByIdAsync(refreshToken.Id);
        var token2 = await unitOfWork2.RefreshTokenWriter.GetByIdAsync(refreshToken.Id);

        // Both try to revoke with different reasons
        token1!.Revoke("First revocation");
        token2!.Revoke("Second revocation");

        // First update should succeed
        await unitOfWork1.RefreshTokenWriter.UpdateAsync(token1);
        await unitOfWork1.SaveChangesAsync();

        // Second update should fail with concurrency exception
        await unitOfWork2.RefreshTokenWriter.UpdateAsync(token2);
        var action = () => unitOfWork2.SaveChangesAsync();

        await action.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task BulkOperations_MultipleRefreshTokens_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var tokens = new List<RefreshToken>();

        for (int i = 0; i < 5; i++)
        {
            var token = RefreshTokenBuilder.ForUser(user)
                .WithToken($"bulk_token_{i}_{Guid.NewGuid():N}")
                .Build();
            tokens.Add(token);
        }

        // Act - Save user with all RefreshTokens (EF Core will cascade save all RefreshTokens)
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var userTokens = await _unitOfWork.RefreshTokenWriter.GetByUserIdAsync(user.Id);
        userTokens.Should().HaveCount(5);

        // Cleanup - Delete all tokens
        foreach (var token in userTokens)
        {
            await _unitOfWork.RefreshTokenWriter.DeleteAsync(token);
        }
        await _unitOfWork.SaveChangesAsync();

        // Verify deletion
        var remainingTokens = await _unitOfWork.RefreshTokenWriter.GetByUserIdAsync(user.Id);
        remainingTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_RevokeToken_ShouldUpdateRevokedStatus()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"revoke_test_{Guid.NewGuid():N}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.RefreshTokenWriter.AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        // Detach and reload
        DbContext.Entry(refreshToken).State = EntityState.Detached;
        var existingToken = await _unitOfWork.RefreshTokenWriter.GetByIdAsync(refreshToken.Id);

        // Act
        var revokeResult = existingToken!.Revoke("User logout");
        revokeResult.IsSuccess.Should().BeTrue();

        await _unitOfWork.RefreshTokenWriter.UpdateAsync(existingToken);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedToken = await _unitOfWork.RefreshTokenReader.GetByIdAsync(refreshToken.Id);
        updatedToken.Should().NotBeNull();
        updatedToken!.IsRevoked.Should().BeTrue();
        updatedToken.RevokedReason.Should().Be("User logout");

        // Token should not be active anymore
        var activeTokens = await _unitOfWork.RefreshTokenWriter.GetActiveTokensAsync(user.Id);
        activeTokens.Should().NotContain(t => t.Id == refreshToken.Id);
    }

    [Fact]
    public async Task AddAsync_ExpiredToken_ShouldStillPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var expiredToken = RefreshTokenBuilder.CreateExpiredToken(user);

        // Act - Save user with RefreshToken (EF Core will cascade save the RefreshToken)
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.RefreshTokenReader.GetByIdAsync(expiredToken.Id);
        result.Should().NotBeNull();
        result!.Token.Should().Be(expiredToken.Token);
        result.ExpiresAt.Should().BeBefore(DateTime.UtcNow);

        // Should not appear in active tokens
        var activeTokens = await _unitOfWork.RefreshTokenWriter.GetActiveTokensAsync(user.Id);
        activeTokens.Should().NotContain(t => t.Id == expiredToken.Id);
    }
}
