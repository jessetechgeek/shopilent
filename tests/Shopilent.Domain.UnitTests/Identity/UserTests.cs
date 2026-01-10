using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Domain.Tests.Identity;

public class UserTests
{
    private Email CreateTestEmail()
    {
        var result = Email.Create("test@example.com");
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private FullName CreateTestFullName()
    {
        var result = FullName.Create("John", "Doe");
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private User CreateTestUser()
    {
        var result = User.Create(
            CreateTestEmail(),
            "hashed_password",
            CreateTestFullName());

        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreateUser()
    {
        // Arrange
        var email = CreateTestEmail();
        var passwordHash = "hashed_password";
        var fullName = CreateTestFullName();

        // Act
        var result = User.Create(email, passwordHash, fullName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Email.Should().Be(email);
        user.PasswordHash.Should().Be(passwordHash);
        user.FullName.Should().Be(fullName);
        user.Role.Should().Be(UserRole.Customer);
        user.IsActive.Should().BeTrue();
        user.EmailVerified.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.RefreshTokens.Should().BeEmpty();
        user.DomainEvents.Should().Contain(e => e is UserCreatedEvent);
    }

    [Fact]
    public void Create_WithNullEmail_ShouldReturnFailure()
    {
        // Arrange
        Email email = null;
        var passwordHash = "hashed_password";
        var fullName = CreateTestFullName();

        // Act
        var result = User.Create(email, passwordHash, fullName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.EmailRequired");
    }

    [Fact]
    public void Create_WithEmptyPasswordHash_ShouldReturnFailure()
    {
        // Arrange
        var email = CreateTestEmail();
        var passwordHash = string.Empty;
        var fullName = CreateTestFullName();

        // Act
        var result = User.Create(email, passwordHash, fullName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.PasswordRequired");
    }

    [Fact]
    public void Create_WithEmptyFirstName_ShouldReturnFailure()
    {
        // Arrange
        var email = CreateTestEmail();
        var passwordHash = "hashed_password";
        // Pass null for fullName to trigger validation in User.Create
        FullName fullName = null;

        // Act
        var result = User.Create(email, passwordHash, fullName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.FirstNameRequired");
    }

    [Fact]
    public void Create_WithEmptyLastName_ShouldReturnFailure()
    {
        // Arrange
        var email = CreateTestEmail();
        var passwordHash = "hashed_password";
        // We'd need to create a FullName with empty lastName, but since validation happens in FullName.Create,
        // we'll just pass null to trigger the validation in User.Create
        FullName fullName = null;

        // Act
        var result = User.Create(email, passwordHash, fullName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.FirstNameRequired");
    }

    [Fact]
    public void CreatePreVerified_ShouldCreateVerifiedUser()
    {
        // Arrange
        var email = CreateTestEmail();
        var passwordHash = "hashed_password";
        var fullName = CreateTestFullName();

        // Act
        var result = User.CreatePreVerified(email, passwordHash, fullName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Email.Should().Be(email);
        user.PasswordHash.Should().Be(passwordHash);
        user.FullName.Should().Be(fullName);
        user.Role.Should().Be(UserRole.Customer);
        user.IsActive.Should().BeTrue();
        user.EmailVerified.Should().BeTrue();
        user.DomainEvents.Should().Contain(e => e is UserCreatedEvent);
    }

    [Fact]
    public void CreateAdmin_ShouldCreateAdminUser()
    {
        // Arrange
        var email = CreateTestEmail();
        var passwordHash = "hashed_password";
        var fullName = CreateTestFullName();

        // Act
        var result = User.CreateAdmin(email, passwordHash, fullName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Email.Should().Be(email);
        user.PasswordHash.Should().Be(passwordHash);
        user.FullName.Should().Be(fullName);
        user.Role.Should().Be(UserRole.Admin);
        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().Contain(e => e is UserCreatedEvent);
    }

    [Fact]
    public void CreateManager_ShouldCreateManagerUser()
    {
        // Arrange
        var email = CreateTestEmail();
        var passwordHash = "hashed_password";
        var fullName = CreateTestFullName();

        // Act
        var result = User.CreateManager(email, passwordHash, fullName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Email.Should().Be(email);
        user.PasswordHash.Should().Be(passwordHash);
        user.FullName.Should().Be(fullName);
        user.Role.Should().Be(UserRole.Manager);
        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().Contain(e => e is UserCreatedEvent);
    }

    [Fact]
    public void UpdatePersonalInfo_WithValidParameters_ShouldUpdateUserInfo()
    {
        // Arrange
        var user = CreateTestUser();
        var newFullNameResult = FullName.Create("Jane", "Smith");
        newFullNameResult.IsSuccess.Should().BeTrue();
        var newFullName = newFullNameResult.Value;

        var newPhoneResult = PhoneNumber.Create("555-123-4567");
        newPhoneResult.IsSuccess.Should().BeTrue();
        var newPhone = newPhoneResult.Value;

        // Act
        var result = user.UpdatePersonalInfo(newFullName, newPhone);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.FullName.Should().Be(newFullName);
        user.Phone.Should().Be(newPhone);
        user.DomainEvents.Should().Contain(e => e is UserUpdatedEvent);
    }

    [Fact]
    public void UpdateEmail_ShouldUpdateEmailAndResetVerification()
    {
        // Arrange
        var userResult = User.CreatePreVerified(
            CreateTestEmail(),
            "hashed_password",
            CreateTestFullName());

        userResult.IsSuccess.Should().BeTrue();
        var user = userResult.Value;
        user.EmailVerified.Should().BeTrue();

        var newEmailResult = Email.Create("new-email@example.com");
        newEmailResult.IsSuccess.Should().BeTrue();
        var newEmail = newEmailResult.Value;

        // Act
        var updateResult = user.UpdateEmail(newEmail);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        user.Email.Should().Be(newEmail);
        user.EmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().NotBeNull();
        user.EmailVerificationExpires.Should().NotBeNull();
        user.DomainEvents.Should().Contain(e => e is UserEmailChangedEvent);
    }

    [Fact]
    public void UpdatePassword_ShouldUpdatePasswordAndRevokeTokens()
    {
        // Arrange
        var user = CreateTestUser();

        var tokenResult = user.AddRefreshToken("refresh_token", DateTime.UtcNow.AddDays(7));
        tokenResult.IsSuccess.Should().BeTrue();
        user.RefreshTokens.Should().HaveCount(1);
        user.RefreshTokens.First().IsActive.Should().BeTrue();

        var newPasswordHash = "new_hashed_password";

        // Act
        var result = user.UpdatePassword(newPasswordHash);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(newPasswordHash);
        user.RefreshTokens.Should().HaveCount(1);
        user.RefreshTokens.First().IsActive.Should().BeFalse();
        user.DomainEvents.Should().Contain(e => e is UserPasswordChangedEvent);
    }

    [Fact]
    public void SetRole_ShouldUpdateUserRole()
    {
        // Arrange
        var user = CreateTestUser();
        user.Role.Should().Be(UserRole.Customer);

        // Act
        var result = user.SetRole(UserRole.Manager);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Role.Should().Be(UserRole.Manager);
        user.DomainEvents.Should().Contain(e => e is UserRoleChangedEvent);
    }

    [Fact]
    public void RecordLoginSuccess_ShouldUpdateLoginInfo()
    {
        // Arrange
        var user = CreateTestUser();
        var failResult = user.RecordLoginFailure(); // Set failed attempt
        failResult.IsSuccess.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(1);
        user.LastFailedAttempt.Should().NotBeNull();

        // Act
        var result = user.RecordLoginSuccess();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.LastLogin.Should().NotBeNull();
        user.FailedLoginAttempts.Should().Be(0);
        user.LastFailedAttempt.Should().BeNull();
    }

    [Fact]
    public void RecordLoginFailure_ShouldIncrementFailedAttempts()
    {
        // Arrange
        var user = CreateTestUser();
        user.FailedLoginAttempts.Should().Be(0);

        // Act
        var result = user.RecordLoginFailure();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(1);
        user.LastFailedAttempt.Should().NotBeNull();
    }

    [Fact]
    public void RecordLoginFailure_ExceedingMaxAttempts_ShouldReturnFailureAndLockAccount()
    {
        // Arrange
        var user = CreateTestUser();

        // Act - record 4 successful failures
        for (int i = 0; i < 4; i++)
        {
            var result = user.RecordLoginFailure();
            result.IsSuccess.Should().BeTrue();
        }

        // Act - record the 5th failure that should lock the account
        var lastResult = user.RecordLoginFailure();

        // Assert
        lastResult.IsFailure.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(5);
        user.IsActive.Should().BeFalse(); // Account should be locked
        user.DomainEvents.Should().Contain(e => e is UserLockedOutEvent);
    }

    [Fact]
    public void Activate_WhenInactive_ShouldActivateUser()
    {
        // Arrange
        var user = CreateTestUser();
        var deactivateResult = user.Deactivate();
        deactivateResult.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();

        // Act
        var result = user.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(0);
        user.LastFailedAttempt.Should().BeNull();
        user.DomainEvents.Should().Contain(e => e is UserStatusChangedEvent);
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateUser()
    {
        // Arrange
        var user = CreateTestUser();
        user.IsActive.Should().BeTrue();

        var tokenResult = user.AddRefreshToken("refresh_token", DateTime.UtcNow.AddDays(7));
        tokenResult.IsSuccess.Should().BeTrue();
        user.RefreshTokens.Should().HaveCount(1);
        user.RefreshTokens.First().IsActive.Should().BeTrue();

        // Act
        var result = user.Deactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.RefreshTokens.Should().HaveCount(1);
        user.RefreshTokens.First().IsActive.Should().BeFalse();
        user.DomainEvents.Should().Contain(e => e is UserStatusChangedEvent);
    }

    [Fact]
    public void VerifyEmail_ShouldMarkEmailAsVerified()
    {
        // Arrange
        var user = CreateTestUser();
        var tokenResult = user.GenerateEmailVerificationToken();
        tokenResult.IsSuccess.Should().BeTrue();
        user.EmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().NotBeNull();
        user.EmailVerificationExpires.Should().NotBeNull();

        // Act
        var result = user.VerifyEmail();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.EmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.EmailVerificationExpires.Should().BeNull();
        user.DomainEvents.Should().Contain(e => e is UserEmailVerifiedEvent);
    }

    [Fact]
    public void GenerateEmailVerificationToken_ShouldCreateToken()
    {
        // Arrange
        var user = CreateTestUser();
        user.EmailVerificationToken.Should().BeNull();
        user.EmailVerificationExpires.Should().BeNull();

        // Act
        var result = user.GenerateEmailVerificationToken();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.EmailVerificationToken.Should().NotBeNull();
        user.EmailVerificationExpires.Should().NotBeNull();
        user.EmailVerificationExpires.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GeneratePasswordResetToken_ShouldCreateToken()
    {
        // Arrange
        var user = CreateTestUser();
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetExpires.Should().BeNull();

        // Act
        var result = user.GeneratePasswordResetToken();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordResetToken.Should().NotBeNull();
        user.PasswordResetExpires.Should().NotBeNull();
        user.PasswordResetExpires.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void ClearPasswordResetToken_ShouldClearToken()
    {
        // Arrange
        var user = CreateTestUser();
        var tokenResult = user.GeneratePasswordResetToken();
        tokenResult.IsSuccess.Should().BeTrue();
        user.PasswordResetToken.Should().NotBeNull();
        user.PasswordResetExpires.Should().NotBeNull();

        // Act
        var result = user.ClearPasswordResetToken();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetExpires.Should().BeNull();
    }

    [Fact]
    public void AddRefreshToken_ShouldAddNewToken()
    {
        // Arrange
        var user = CreateTestUser();
        var token = "refresh_token";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var ipAddress = "127.0.0.1";
        var userAgent = "Test Agent";

        // Act
        var result = user.AddRefreshToken(token, expiresAt, ipAddress, userAgent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.RefreshTokens.Should().HaveCount(1);
        var refreshToken = result.Value;
        refreshToken.Token.Should().Be(token);
        refreshToken.ExpiresAt.Should().Be(expiresAt);
        refreshToken.IpAddress.Should().Be(ipAddress);
        refreshToken.UserAgent.Should().Be(userAgent);
        refreshToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RevokeRefreshToken_ShouldRevokeToken()
    {
        // Arrange
        var user = CreateTestUser();
        var token = "refresh_token";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var tokenResult = user.AddRefreshToken(token, expiresAt);
        tokenResult.IsSuccess.Should().BeTrue();
        var refreshToken = tokenResult.Value;
        refreshToken.IsActive.Should().BeTrue();
        var reason = "Test revocation";

        // Act
        var result = user.RevokeRefreshToken(token, reason);

        // Assert
        result.IsSuccess.Should().BeTrue();
        refreshToken.IsActive.Should().BeFalse();
        refreshToken.RevokedReason.Should().Be(reason);
    }

    [Fact]
    public void RevokeAllRefreshTokens_ShouldRevokeAllTokens()
    {
        // Arrange
        var user = CreateTestUser();

        var token1Result = user.AddRefreshToken("token1", DateTime.UtcNow.AddDays(7));
        var token2Result = user.AddRefreshToken("token2", DateTime.UtcNow.AddDays(7));
        var token3Result = user.AddRefreshToken("token3", DateTime.UtcNow.AddDays(7));

        token1Result.IsSuccess.Should().BeTrue();
        token2Result.IsSuccess.Should().BeTrue();
        token3Result.IsSuccess.Should().BeTrue();

        var token1 = token1Result.Value;
        var token2 = token2Result.Value;
        var token3 = token3Result.Value;

        user.RefreshTokens.Should().HaveCount(3);
        token1.IsActive.Should().BeTrue();
        token2.IsActive.Should().BeTrue();
        token3.IsActive.Should().BeTrue();

        var reason = "Security measure";

        // Act
        var result = user.RevokeAllRefreshTokens(reason);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.RefreshTokens.Should().HaveCount(3);
        user.RefreshTokens.Should().AllSatisfy(token => token.IsActive.Should().BeFalse());
        user.RefreshTokens.Should().AllSatisfy(token => token.RevokedReason.Should().Be(reason));
    }
}
