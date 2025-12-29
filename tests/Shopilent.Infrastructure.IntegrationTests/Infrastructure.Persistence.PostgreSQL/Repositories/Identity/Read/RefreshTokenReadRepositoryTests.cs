using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Identity.Read;

[Collection("IntegrationTests")]
public class RefreshTokenReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IRefreshTokenWriteRepository _refreshTokenWriteRepository = null!;
    private IRefreshTokenReadRepository _refreshTokenReadRepository = null!;

    public RefreshTokenReadRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _refreshTokenWriteRepository = GetService<IRefreshTokenWriteRepository>();
        _refreshTokenReadRepository = GetService<IRefreshTokenReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingRefreshToken_ShouldReturnRefreshToken()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user).Build();

        await _userWriteRepository.AddAsync(user);
        await _refreshTokenWriteRepository.AddAsync(refreshToken);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.GetByIdAsync(refreshToken.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(refreshToken.Id);
        result.UserId.Should().Be(user.Id);
        result.Token.Should().Be(refreshToken.Token);
        result.ExpiresAt.Should().BeCloseTo(refreshToken.ExpiresAt, TimeSpan.FromMilliseconds(100));
        result.IssuedAt.Should().BeCloseTo(refreshToken.IssuedAt, TimeSpan.FromMilliseconds(100));
        result.IsRevoked.Should().Be(refreshToken.IsRevoked);
        result.IpAddress.Should().Be(refreshToken.IpAddress);
        result.UserAgent.Should().Be(refreshToken.UserAgent);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentRefreshToken_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _refreshTokenReadRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTokenAsync_ExistingToken_ShouldReturnRefreshToken()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"test_token_{DateTime.Now.Ticks}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _refreshTokenWriteRepository.AddAsync(refreshToken);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.GetByTokenAsync(refreshToken.Token);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(refreshToken.Id);
        result.Token.Should().Be(refreshToken.Token);
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByTokenAsync_NonExistentToken_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentToken = "non_existent_token";

        // Act
        var result = await _refreshTokenReadRepository.GetByTokenAsync(nonExistentToken);

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
            .WithToken($"token1_{DateTime.Now.Ticks}")
            .Build();
        var refreshToken2 = RefreshTokenBuilder.ForUser(user)
            .WithToken($"token2_{DateTime.Now.Ticks}")
            .Build();

        // Different user token should not be included
        var otherUser = new UserBuilder()
            .WithEmail($"other-{DateTime.Now.Ticks}@example.com")
            .WithUsername($"otheruser-{DateTime.Now.Ticks}")
            .Build();
        var otherUserToken = RefreshTokenBuilder.ForUser(otherUser)
            .WithToken($"other_token_{DateTime.Now.Ticks}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _userWriteRepository.AddAsync(otherUser);
        await _refreshTokenWriteRepository.AddAsync(refreshToken1);
        await _refreshTokenWriteRepository.AddAsync(refreshToken2);
        await _refreshTokenWriteRepository.AddAsync(otherUserToken);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(token => token.UserId == user.Id);
        result.Select(t => t.Token).Should().Contain(new[] { refreshToken1.Token, refreshToken2.Token });
    }

    [Fact]
    public async Task GetByUserIdAsync_NonExistentUser_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _refreshTokenReadRepository.GetByUserIdAsync(nonExistentUserId);

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

        // Active token
        var activeToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"active_token_{DateTime.Now.Ticks}")
            .WithExpiresAt(DateTime.UtcNow.AddDays(1))
            .Build();

        // Expired token
        var expiredToken = RefreshTokenBuilder.CreateExpiredToken(user);

        // Create revoked token manually (since we need to save it first)
        var revokedToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"revoked_token_{DateTime.Now.Ticks}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _refreshTokenWriteRepository.AddAsync(activeToken);
        await _refreshTokenWriteRepository.AddAsync(expiredToken);
        await _refreshTokenWriteRepository.AddAsync(revokedToken);
        await _unitOfWork.CommitAsync();

        // Revoke the token after saving
        revokedToken.Revoke("Manual revocation for test");
        await _refreshTokenWriteRepository.UpdateAsync(revokedToken);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.GetActiveTokensAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Token.Should().Be(activeToken.Token);
        result.First().IsRevoked.Should().BeFalse();
        result.First().ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task GetActiveTokensAsync_NoActiveTokens_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        // Only expired token
        var expiredToken = RefreshTokenBuilder.CreateExpiredToken(user);

        await _userWriteRepository.AddAsync(user);
        await _refreshTokenWriteRepository.AddAsync(expiredToken);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.GetActiveTokensAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAllAsync_MultipleRefreshTokens_ShouldReturnAllTokens()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = new UserBuilder()
            .WithEmail($"user1-{DateTime.Now.Ticks}@example.com")
            .WithUsername($"user1-{DateTime.Now.Ticks}")
            .Build();
        var user2 = new UserBuilder()
            .WithEmail($"user2-{DateTime.Now.Ticks}@example.com")
            .WithUsername($"user2-{DateTime.Now.Ticks}")
            .Build();

        var token1 = RefreshTokenBuilder.ForUser(user1)
            .WithToken($"token1_{DateTime.Now.Ticks}")
            .Build();
        var token2 = RefreshTokenBuilder.ForUser(user2)
            .WithToken($"token2_{DateTime.Now.Ticks}")
            .Build();

        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _refreshTokenWriteRepository.AddAsync(token1);
        await _refreshTokenWriteRepository.AddAsync(token2);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(2); // Account for any existing tokens
        result.Should().Contain(token => token.Token == token1.Token);
        result.Should().Contain(token => token.Token == token2.Token);
    }

    [Fact]
    public async Task GetActiveTokensAsync_RevokedTokens_ShouldNotReturnRevokedTokens()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        var activeToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"active_{DateTime.Now.Ticks}")
            .Build();

        var revokedToken = RefreshTokenBuilder.ForUser(user)
            .WithToken($"revoked_{DateTime.Now.Ticks}")
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _refreshTokenWriteRepository.AddAsync(activeToken);
        await _refreshTokenWriteRepository.AddAsync(revokedToken);
        await _unitOfWork.CommitAsync();

        // Revoke one token
        revokedToken.Revoke("Test revocation");
        await _refreshTokenWriteRepository.UpdateAsync(revokedToken);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.GetActiveTokensAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Token.Should().Be(activeToken.Token);
        result.Should().NotContain(token => token.Token == revokedToken.Token);
    }

    [Fact]
    public async Task GetByTokenAsync_CaseSensitiveToken_ShouldMatchExactCase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var tokenValue = $"CaseSensitiveToken_{DateTime.Now.Ticks}";
        var refreshToken = RefreshTokenBuilder.ForUser(user)
            .WithToken(tokenValue)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _refreshTokenWriteRepository.AddAsync(refreshToken);
        await _unitOfWork.CommitAsync();

        // Act - Search with exact case
        var exactResult = await _refreshTokenReadRepository.GetByTokenAsync(tokenValue);

        // Search with different case
        var wrongCaseResult = await _refreshTokenReadRepository.GetByTokenAsync(tokenValue.ToLowerInvariant());

        // Assert
        exactResult.Should().NotBeNull();
        exactResult!.Token.Should().Be(tokenValue);

        wrongCaseResult.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveTokensAsync_MultipleUsersWithActiveTokens_ShouldReturnOnlyRequestedUserTokens()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = new UserBuilder()
            .WithEmail($"user1-{DateTime.Now.Ticks}@example.com")
            .WithUsername($"user1-{DateTime.Now.Ticks}")
            .Build();
        var user2 = new UserBuilder()
            .WithEmail($"user2-{DateTime.Now.Ticks}@example.com")
            .WithUsername($"user2-{DateTime.Now.Ticks}")
            .Build();

        var user1Token = RefreshTokenBuilder.ForUser(user1)
            .WithToken($"user1_token_{DateTime.Now.Ticks}")
            .Build();
        var user2Token = RefreshTokenBuilder.ForUser(user2)
            .WithToken($"user2_token_{DateTime.Now.Ticks}")
            .Build();

        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _refreshTokenWriteRepository.AddAsync(user1Token);
        await _refreshTokenWriteRepository.AddAsync(user2Token);
        await _unitOfWork.CommitAsync();

        // Act
        var user1Result = await _refreshTokenReadRepository.GetActiveTokensAsync(user1.Id);
        var user2Result = await _refreshTokenReadRepository.GetActiveTokensAsync(user2.Id);

        // Assert
        user1Result.Should().HaveCount(1);
        user1Result.First().Token.Should().Be(user1Token.Token);
        user1Result.Should().NotContain(token => token.Token == user2Token.Token);

        user2Result.Should().HaveCount(1);
        user2Result.First().Token.Should().Be(user2Token.Token);
        user2Result.Should().NotContain(token => token.Token == user1Token.Token);
    }

    [Fact]
    public async Task GetByUserIdAsync_UserWithNoTokens_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _refreshTokenReadRepository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
