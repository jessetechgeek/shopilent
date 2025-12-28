using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Infrastructure.Identity.Abstractions;

namespace Shopilent.Infrastructure.Identity.Services;

/// <summary>
/// ASP.NET Core Identity implementation of IAuthenticationService.
/// Wraps UserManager and SignInManager to provide authentication services
/// while maintaining compatibility with our existing interface.
/// </summary>
internal sealed class AspNetIdentityAuthenticationService : IAuthenticationService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AspNetIdentityAuthenticationService> _logger;

    public AspNetIdentityAuthenticationService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        IJwtService jwtService,
        IEmailService emailService,
        ILogger<AspNetIdentityAuthenticationService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _jwtService = jwtService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResponse>> LoginAsync(
        Email email,
        string password,
        string ipAddress = null,
        string userAgent = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (email == null)
                return Result.Failure<AuthTokenResponse>(UserErrors.EmailRequired);

            if (string.IsNullOrWhiteSpace(password))
                return Result.Failure<AuthTokenResponse>(UserErrors.PasswordRequired);

            // Find user by email
            var user = await _userManager.FindByEmailAsync(email.Value);
            if (user == null)
                return Result.Failure<AuthTokenResponse>(UserErrors.InvalidCredentials);

            // Check if user is locked out (ASP.NET Identity handles this)
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Login attempt for locked account: {Email}", email.Value);
                return Result.Failure<AuthTokenResponse>(UserErrors.AccountLocked);
            }

            // Check if user is active (our domain logic)
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt for inactive account: {Email}", email.Value);
                return Result.Failure<AuthTokenResponse>(UserErrors.AccountInactive);
            }

            // Verify password using Identity's password hasher
            var passwordCheckResult =
                await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

            if (!passwordCheckResult.Succeeded)
            {
                if (passwordCheckResult.IsLockedOut)
                {
                    _logger.LogWarning("Account locked due to failed login attempts: {Email}", email.Value);
                    return Result.Failure<AuthTokenResponse>(UserErrors.AccountLocked);
                }

                // Record failure in our domain
                user.RecordLoginFailure();
                await _userManager.UpdateAsync(user);

                return Result.Failure<AuthTokenResponse>(UserErrors.InvalidCredentials);
            }

            // Record successful login in our domain
            user.RecordLoginSuccess();

            // Generate tokens (using our existing JWT service)
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddDays(7);

            // Add refresh token through domain entity
            var tokenResult = user.AddRefreshToken(refreshToken, expiresAt, ipAddress, userAgent);
            if (tokenResult.IsFailure)
                return Result.Failure<AuthTokenResponse>(tokenResult.Error);

            // Save changes
            await _userManager.UpdateAsync(user);

            return Result.Success(new AuthTokenResponse
            {
                User = user, AccessToken = accessToken, RefreshToken = refreshToken,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for email: {Email}", email?.Value);
            return Result.Failure<AuthTokenResponse>(
                Error.Failure(
                    code: "Authentication.Failed",
                    message: "An error occurred during authentication. Please try again."));
        }
    }

    public async Task<Result<AuthTokenResponse>> RegisterAsync(
        Email email,
        string password,
        string firstName,
        string lastName,
        string phone = null,
        string ipAddress = null,
        string userAgent = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (email == null)
                return Result.Failure<AuthTokenResponse>(UserErrors.EmailRequired);

            if (string.IsNullOrWhiteSpace(password))
                return Result.Failure<AuthTokenResponse>(UserErrors.PasswordRequired);

            if (string.IsNullOrWhiteSpace(firstName))
                return Result.Failure<AuthTokenResponse>(UserErrors.FirstNameRequired);

            if (string.IsNullOrWhiteSpace(lastName))
                return Result.Failure<AuthTokenResponse>(UserErrors.LastNameRequired);

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(email.Value);
            if (existingUser != null)
                return Result.Failure<AuthTokenResponse>(UserErrors.EmailAlreadyExists(email.Value));

            // Create FullName value object
            var fullNameResult = FullName.Create(firstName, lastName);
            if (fullNameResult.IsFailure)
                return Result.Failure<AuthTokenResponse>(fullNameResult.Error);

            // Create PhoneNumber value object if provided
            PhoneNumber phoneNumber = null;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneResult = PhoneNumber.Create(phone);
                if (phoneResult.IsFailure)
                    return Result.Failure<AuthTokenResponse>(phoneResult.Error);
                phoneNumber = phoneResult.Value;
            }

            // Create user (with temporary password hash - Identity will set the real one)
            var userResult = User.Create(email, "TEMP", fullNameResult.Value);
            if (userResult.IsFailure)
                return Result.Failure<AuthTokenResponse>(userResult.Error);

            var user = userResult.Value;

            // Set phone if provided
            if (phoneNumber != null)
            {
                user.UpdatePersonalInfo(fullNameResult.Value, phoneNumber);
            }

            // Generate email verification token
            user.GenerateEmailVerificationToken();

            // Create user with Identity (this will hash the password using Identity's hasher)
            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return Result.Failure<AuthTokenResponse>(
                    Error.Validation("Registration.Failed", errors));
            }

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddDays(7);

            // Add refresh token
            var tokenResult = user.AddRefreshToken(refreshToken, expiresAt, ipAddress, userAgent);
            if (tokenResult.IsFailure)
                return Result.Failure<AuthTokenResponse>(tokenResult.Error);

            // Update user with refresh token
            await _userManager.UpdateAsync(user);

            // Send verification email (optional - commented out for now)
            // await _emailService.SendEmailVerificationAsync(email.Value, user.EmailVerificationToken);

            return Result.Success(new AuthTokenResponse
            {
                User = user, AccessToken = accessToken, RefreshToken = refreshToken,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", email?.Value);
            return Result.Failure<AuthTokenResponse>(
                Error.Failure(
                    code: "Registration.Failed",
                    message: "An error occurred during registration. Please try again."));
        }
    }

    public async Task<Result<AuthTokenResponse>> RefreshTokenAsync(
        string refreshToken,
        string ipAddress = null,
        string userAgent = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
                return Result.Failure<AuthTokenResponse>(RefreshTokenErrors.EmptyToken);

            // Find the refresh token
            var token = await _unitOfWork.RefreshTokenWriter.GetByTokenAsync(refreshToken, cancellationToken);
            if (token == null)
                return Result.Failure<AuthTokenResponse>(RefreshTokenErrors.NotFound(refreshToken));

            // Check if token is expired
            if (token.IsExpired)
                return Result.Failure<AuthTokenResponse>(RefreshTokenErrors.Expired);

            // Check if token is revoked
            if (token.IsRevoked)
                return Result.Failure<AuthTokenResponse>(RefreshTokenErrors.Revoked(token.RevokedReason));

            // Get user
            var user = await _userManager.FindByIdAsync(token.UserId.ToString());
            if (user == null)
                return Result.Failure<AuthTokenResponse>(UserErrors.NotFound(token.UserId));

            // Check if user is active
            if (!user.IsActive)
                return Result.Failure<AuthTokenResponse>(UserErrors.AccountInactive);

            // Revoke the current token
            token.Revoke("Replaced by new token");

            // Generate new tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var newRefreshToken = _jwtService.GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddDays(7);

            // Add new refresh token
            var tokenResult = user.AddRefreshToken(newRefreshToken, expiresAt, ipAddress, userAgent);
            if (tokenResult.IsFailure)
                return Result.Failure<AuthTokenResponse>(tokenResult.Error);

            // Save changes
            await _userManager.UpdateAsync(user);

            return Result.Success(new AuthTokenResponse
            {
                User = user, AccessToken = accessToken, RefreshToken = newRefreshToken,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return Result.Failure<AuthTokenResponse>(
                Error.Failure(
                    code: "RefreshToken.Failed",
                    message: "An error occurred while refreshing the token. Please try again."));
        }
    }

    public async Task<Result> RevokeTokenAsync(
        string refreshToken,
        string reason = "User logged out",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
                return Result.Failure(RefreshTokenErrors.EmptyToken);

            // Find the refresh token
            var token = await _unitOfWork.RefreshTokenWriter.GetByTokenAsync(refreshToken, cancellationToken);
            if (token == null)
                return Result.Failure(RefreshTokenErrors.NotFound(refreshToken));

            // Check if already revoked
            if (token.IsRevoked)
                return Result.Success(); // Already revoked, no action needed

            // Get user
            var user = await _userManager.FindByIdAsync(token.UserId.ToString());
            if (user == null)
                return Result.Failure(UserErrors.NotFound(token.UserId));

            // Revoke token
            var result = user.RevokeRefreshToken(refreshToken, reason);
            if (result.IsFailure)
                return result;

            // Save changes
            await _userManager.UpdateAsync(user);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token");
            return Result.Failure(
                Error.Failure(
                    code: "RevokeToken.Failed",
                    message: "An error occurred while revoking the token. Please try again."));
        }
    }

    public async Task<Result<UserDto>> ValidateTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken))
                return Result.Failure<UserDto>(UserErrors.InvalidCredentials);

            // Validate token
            if (!_jwtService.ValidateAccessToken(accessToken))
                return Result.Failure<UserDto>(UserErrors.InvalidCredentials);

            // Extract user ID from token
            var (isValid, email, userId) = _jwtService.DecodeAccessToken(accessToken);
            if (!isValid || userId == Guid.Empty)
                return Result.Failure<UserDto>(UserErrors.InvalidCredentials);

            // Get user
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return Result.Failure<UserDto>(UserErrors.NotFound(userId));

            // Check if user is active
            if (!user.IsActive)
                return Result.Failure<UserDto>(UserErrors.AccountInactive);

            // Map to DTO
            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                FirstName = user.FullName.FirstName,
                LastName = user.FullName.LastName,
                Phone = user.Phone?.Value,
                Role = user.Role,
                IsActive = user.IsActive,
                EmailVerified = user.EmailVerified,
                LastLogin = user.LastLogin,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return Result.Success(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating access token");
            return Result.Failure<UserDto>(
                Error.Failure(
                    code: "ValidateToken.Failed",
                    message: "An error occurred while validating the token."));
        }
    }

    public async Task<Result> SendEmailVerificationAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (email == null)
                return Result.Failure(UserErrors.EmailRequired);

            var user = await _userManager.FindByEmailAsync(email.Value);
            if (user == null)
                return Result.Failure(UserErrors.NotFound(Guid.Empty));

            // Check if already verified
            if (user.EmailVerified)
                return Result.Success(); // Already verified

            // Generate new verification token (ASP.NET Identity token)
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            // Also update our domain token
            user.GenerateEmailVerificationToken();
            await _userManager.UpdateAsync(user);

            // Send verification email
            // await _emailService.SendEmailVerificationAsync(email.Value, token);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email verification: {Email}", email?.Value);
            return Result.Failure(
                Error.Failure(
                    code: "EmailVerification.Failed",
                    message: "An error occurred while sending the verification email. Please try again."));
        }
    }

    public async Task<Result> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                return Result.Failure(Error.Validation(
                    code: "EmailVerification.InvalidToken",
                    message: "The verification token is invalid."));

            // Find user by verification token (using our domain method)
            var user = await _userWriteRepository.GetByEmailVerificationTokenAsync(token, cancellationToken);
            if (user == null)
                return Result.Failure(Error.Validation(
                    code: "EmailVerification.InvalidToken",
                    message: "The verification token is invalid or has expired."));

            // Verify email through domain (this sets EmailVerified internally)
            var result = user.VerifyEmail();
            if (result.IsFailure)
                return result;

            // Save changes
            await _userManager.UpdateAsync(user);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email with token: {Token}", token);
            return Result.Failure(
                Error.Failure(
                    code: "EmailVerification.Failed",
                    message: "An error occurred while verifying your email. Please try again."));
        }
    }

    public async Task<Result> RequestPasswordResetAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (email == null)
                return Result.Failure(UserErrors.EmailRequired);

            var user = await _userManager.FindByEmailAsync(email.Value);
            if (user == null)
                return Result.Success(); // Don't reveal if user exists

            // Generate password reset token using Identity
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Also update our domain token
            user.GeneratePasswordResetToken();
            await _userManager.UpdateAsync(user);

            // Send password reset email
            // await _emailService.SendPasswordResetAsync(email.Value, token);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting password reset: {Email}", email?.Value);
            return Result.Failure(
                Error.Failure(
                    code: "PasswordReset.Failed",
                    message: "An error occurred while processing your request. Please try again."));
        }
    }

    public async Task<Result> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                return Result.Failure(Error.Validation(
                    code: "PasswordReset.InvalidToken",
                    message: "The reset token is invalid."));

            if (string.IsNullOrWhiteSpace(newPassword))
                return Result.Failure(UserErrors.PasswordRequired);

            // Find user by reset token (using our domain method)
            var user = await _userWriteRepository.GetByPasswordResetTokenAsync(token, cancellationToken);
            if (user == null)
                return Result.Failure(Error.Validation(
                    code: "PasswordReset.InvalidToken",
                    message: "The reset token is invalid or has expired."));

            // Reset password using Identity (this will hash with Identity's hasher)
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                return Result.Failure(Error.Validation("PasswordReset.Failed", errors));
            }

            // Clear reset token in domain
            user.ClearPasswordResetToken();

            // Revoke all refresh tokens
            user.RevokeAllRefreshTokens("Password changed");

            await _userManager.UpdateAsync(user);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return Result.Failure(
                Error.Failure(
                    code: "PasswordReset.Failed",
                    message: "An error occurred while resetting your password. Please try again."));
        }
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
                return Result.Failure(UserErrors.PasswordRequired);

            if (string.IsNullOrWhiteSpace(newPassword))
                return Result.Failure(UserErrors.PasswordRequired);

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return Result.Failure(UserErrors.NotFound(userId));

            // Change password using Identity
            var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (!changeResult.Succeeded)
            {
                if (changeResult.Errors.Any(e => e.Code == "PasswordMismatch"))
                {
                    return Result.Failure(UserErrors.InvalidCredentials);
                }

                var errors = string.Join(", ", changeResult.Errors.Select(e => e.Description));
                return Result.Failure(Error.Validation("ChangePassword.Failed", errors));
            }

            // Revoke all refresh tokens
            user.RevokeAllRefreshTokens("Password changed");

            await _userManager.UpdateAsync(user);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user ID: {UserId}", userId);
            return Result.Failure(
                Error.Failure(
                    code: "ChangePassword.Failed",
                    message: "An error occurred while changing your password. Please try again."));
        }
    }
}
