using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Infrastructure.Identity.Abstractions;

namespace Shopilent.Infrastructure.Identity.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly IUserReadRepository _userReadRepository;
    private readonly IRefreshTokenWriteRepository _refreshTokenWriteRepository;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthenticationService> _logger;

    internal AuthenticationService(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        IUserReadRepository userReadRepository,
        IRefreshTokenWriteRepository refreshTokenWriteRepository,
        IJwtService jwtService,
        IPasswordService passwordService,
        IEmailService emailService,
        ILogger<AuthenticationService> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _userReadRepository = userReadRepository;
        _refreshTokenWriteRepository = refreshTokenWriteRepository;
        _jwtService = jwtService;
        _passwordService = passwordService;
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
            try
            {
                // Find user by email
                var user = await _userWriteRepository.GetByEmailAsync(email.Value, cancellationToken);
                if (user == null)
                    return Result.Failure<AuthTokenResponse>(UserErrors.InvalidCredentials);

                // Try to auto-unlock if lockout period has expired
                user.TryAutoUnlock();

                // Check if user is active
                if (!user.IsActive)
                {
                    _logger.LogWarning("Login attempt for inactive account: {Email}", email.Value);
                    return Result.Failure<AuthTokenResponse>(UserErrors.AccountInactive);
                }

                // Verify password
                if (!_passwordService.VerifyPassword(password, user.PasswordHash))
                {
                    var loginFailureResult = user.RecordLoginFailure();
                    if (loginFailureResult.IsFailure)
                    {
                        _logger.LogWarning("Account locked due to failed login attempts: {Email}", email.Value);
                        await _unitOfWork.CommitAsync(cancellationToken);
                        return Result.Failure<AuthTokenResponse>(loginFailureResult.Error);
                    }

                    await _unitOfWork.CommitAsync(cancellationToken);
                    return Result.Failure<AuthTokenResponse>(UserErrors.InvalidCredentials);
                }

                // Record successful login
                user.RecordLoginSuccess();

                // Generate tokens
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var expiresAt = DateTime.UtcNow.AddDays(7); // 7 days refresh token validity

                // Add refresh token through domain entity (aggregate root)
                var tokenResult = user.AddRefreshToken(refreshToken, expiresAt, ipAddress, userAgent);
                if (tokenResult.IsFailure)
                    return Result.Failure<AuthTokenResponse>(tokenResult.Error);

                // Update the user entity
                await _userWriteRepository.UpdateAsync(user, cancellationToken);

                // Save all changes in a single operation
                await _unitOfWork.CommitAsync(cancellationToken);

                return Result.Success(new AuthTokenResponse()
                {
                    User = user, AccessToken = accessToken, RefreshToken = refreshToken,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user authentication and token generation");
                throw;
            }
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

            try
            {
                // Check if email already exists
                bool emailExists = await _userReadRepository.EmailExistsAsync(email.Value, null, cancellationToken);
                if (emailExists)
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

                // Hash password
                string passwordHash = _passwordService.HashPassword(password);

                // Create user
                var userResult = User.Create(email, passwordHash, fullNameResult.Value);
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

                // Save user
                await _userWriteRepository.AddAsync(user, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                // Generate tokens
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var expiresAt = DateTime.UtcNow.AddDays(7); // 7 days refresh token validity

                // Add refresh token
                var tokenResult = user.AddRefreshToken(refreshToken, expiresAt, ipAddress, userAgent);
                if (tokenResult.IsFailure)
                    return Result.Failure<AuthTokenResponse>(tokenResult.Error);

                // Update user with refresh token
                await _userWriteRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                // Send verification email
                // await _emailService.SendEmailVerificationAsync(email.Value, user.EmailVerificationToken);

                return Result.Success(new AuthTokenResponse()
                {
                    User = user, AccessToken = accessToken, RefreshToken = refreshToken,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                throw;
            }
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

            try
            {
                // Find the refresh token
                var token = await _refreshTokenWriteRepository.GetByTokenAsync(refreshToken, cancellationToken);
                if (token == null)
                    return Result.Failure<AuthTokenResponse>(RefreshTokenErrors.NotFound(refreshToken));

                // Check if token is expired
                if (token.IsExpired)
                    return Result.Failure<AuthTokenResponse>(RefreshTokenErrors.Expired);

                // Check if token is revoked
                if (token.IsRevoked)
                    return Result.Failure<AuthTokenResponse>(RefreshTokenErrors.Revoked(token.RevokedReason));

                // Get user
                var user = await _userWriteRepository.GetByIdAsync(token.UserId, cancellationToken);
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

                // Add new refresh token
                var expiresAt = DateTime.UtcNow.AddDays(7); // 7 days refresh token validity
                var tokenResult = user.AddRefreshToken(newRefreshToken, expiresAt, ipAddress, userAgent);
                if (tokenResult.IsFailure)
                    return Result.Failure<AuthTokenResponse>(tokenResult.Error);

                await _userWriteRepository.UpdateAsync(user, cancellationToken);

                // Save changes
                await _unitOfWork.CommitAsync(cancellationToken);

                // return Result.Success((user, accessToken, newRefreshToken));
                return Result.Success(new AuthTokenResponse()
                {
                    User = user, AccessToken = accessToken, RefreshToken = newRefreshToken,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                throw;
            }
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

            try
            {
                // Find the refresh token
                var token = await _refreshTokenWriteRepository.GetByTokenAsync(refreshToken, cancellationToken);
                if (token == null)
                    return Result.Failure(RefreshTokenErrors.NotFound(refreshToken));

                // Check if already revoked
                if (token.IsRevoked)
                    return Result.Success(); // Already revoked, no action needed

                // Get user
                var user = await _userWriteRepository.GetByIdAsync(token.UserId, cancellationToken);
                if (user == null)
                    return Result.Failure(UserErrors.NotFound(token.UserId));

                // Revoke token
                var result = user.RevokeRefreshToken(refreshToken, reason);
                if (result.IsFailure)
                    return result;

                // Save changes
                await _unitOfWork.CommitAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                throw;
            }
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
            var user = await _userReadRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
                return Result.Failure<UserDto>(UserErrors.NotFound(userId));

            // Check if user is active
            if (!user.IsActive)
                return Result.Failure<UserDto>(UserErrors.AccountInactive);

            return Result.Success(user);
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

            // Get user by email
            var user = await _userWriteRepository.GetByEmailAsync(email.Value, cancellationToken);
            if (user == null)
                return Result.Failure(UserErrors.NotFound(Guid.Empty));

            // Check if already verified
            if (user.EmailVerified)
                return Result.Success(); // Already verified, no action needed

            // Generate new verification token
            user.GenerateEmailVerificationToken();
            await _userWriteRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            // Send verification email
            // await _emailService.SendEmailVerificationAsync(email.Value, user.EmailVerificationToken);

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

            try
            {
                // Find user by verification token
                var user = await _userWriteRepository.GetByEmailVerificationTokenAsync(token, cancellationToken);
                if (user == null)
                    return Result.Failure(Error.Validation(
                        code: "EmailVerification.InvalidToken",
                        message: "The verification token is invalid or has expired."));

                // Check if token has expired
                if (user.EmailVerificationExpires.HasValue &&
                    user.EmailVerificationExpires.Value < DateTime.UtcNow)
                {
                    return Result.Failure(Error.Validation(
                        code: "EmailVerification.TokenExpired",
                        message: "The verification link has expired. Please request a new one."));
                }

                // Verify email
                var result = user.VerifyEmail();
                if (result.IsFailure)
                    return result;

                // Save changes
                await _userWriteRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email");
                throw;
            }
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

            // Find user by email (don't reveal if email exists or not for security)
            var user = await _userWriteRepository.GetByEmailAsync(email.Value, cancellationToken);
            if (user == null)
                return Result.Success(); // Pretend success even if user doesn't exist

            // Generate password reset token
            user.GeneratePasswordResetToken();
            await _userWriteRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            // Send password reset email
            // await _emailService.SendPasswordResetAsync(email.Value, user.PasswordResetToken);

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

            try
            {
                // Find user by reset token
                var user = await _userWriteRepository.GetByPasswordResetTokenAsync(token, cancellationToken);
                if (user == null)
                    return Result.Failure(Error.Validation(
                        code: "PasswordReset.InvalidToken",
                        message: "The reset token is invalid or has expired."));

                // Check if token has expired (1 hour validity)
                if (user.PasswordResetExpires.HasValue &&
                    user.PasswordResetExpires.Value < DateTime.UtcNow)
                {
                    return Result.Failure(Error.Validation(
                        code: "PasswordReset.TokenExpired",
                        message: "The password reset link has expired. Please request a new one."));
                }

                // Hash new password
                var passwordHash = _passwordService.HashPassword(newPassword);

                // Update password
                var updateResult = user.UpdatePassword(passwordHash);
                if (updateResult.IsFailure)
                    return updateResult;

                // Clear reset token
                var clearResult = user.ClearPasswordResetToken();
                if (clearResult.IsFailure)
                    return clearResult;

                // Revoke all refresh tokens
                user.RevokeAllRefreshTokens("Password changed");

                // Save changes
                await _userWriteRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                throw;
            }
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

            try
            {
                // Get user
                var user = await _userWriteRepository.GetByIdAsync(userId, cancellationToken);
                if (user == null)
                    return Result.Failure(UserErrors.NotFound(userId));

                // Verify current password
                if (!_passwordService.VerifyPassword(currentPassword, user.PasswordHash))
                    return Result.Failure(UserErrors.InvalidCredentials);

                // Hash new password
                var passwordHash = _passwordService.HashPassword(newPassword);

                // Update password
                var updateResult = user.UpdatePassword(passwordHash);
                if (updateResult.IsFailure)
                    return updateResult;

                // Revoke all refresh tokens
                user.RevokeAllRefreshTokens("Password changed");

                // Save changes
                await _userWriteRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                throw;
            }
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
