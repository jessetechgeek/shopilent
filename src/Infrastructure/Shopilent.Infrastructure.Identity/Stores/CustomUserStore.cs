using Microsoft.AspNetCore.Identity;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Infrastructure.Identity.Stores;

/// <summary>
/// Custom UserStore implementation that maps ASP.NET Core Identity operations
/// to our existing User entity and repository pattern.
/// No database schema changes required - works with existing tables.
/// </summary>
internal sealed  class CustomUserStore :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserRoleStore<User>,
    IUserLockoutStore<User>,
    IUserTwoFactorStore<User>,
    IUserSecurityStampStore<User>,
    IUserPhoneNumberStore<User>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;

    public CustomUserStore(IUnitOfWork unitOfWork, IUserWriteRepository userWriteRepository)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
    }

    #region IUserStore<User>

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await _userWriteRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "CreateUserFailed",
                Description = ex.Message
            });
        }
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await _userWriteRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UpdateUserFailed",
                Description = ex.Message
            });
        }
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await _userWriteRepository.DeleteAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DeleteUserFailed",
                Description = ex.Message
            });
        }
    }

    public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return Task.FromResult<User?>(null);

        return _userWriteRepository.GetByIdAsync(userGuid, cancellationToken);
    }

    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        // In our system, username = email
        // ASP.NET Identity passes normalized (uppercase) username
        // Convert back to lowercase for our repository search
        var email = normalizedUserName?.ToLowerInvariant();
        return _userWriteRepository.GetByEmailAsync(email, cancellationToken);
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value.ToUpperInvariant());
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value);
    }

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
    {
        // Email is a value object - normalization handled by Email class
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        // Email changes are handled through User.UpdateEmail() domain method
        // This method is required by interface but we handle email changes differently
        return Task.CompletedTask;
    }

    #endregion

    #region IUserPasswordStore<User>

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(user.PasswordHash));
    }

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        // Password changes should go through User.UpdatePassword() to trigger domain events
        // But for Identity's password reset flow, we need to support direct hash setting
        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            user.UpdatePassword(passwordHash);
        }
        return Task.CompletedTask;
    }

    #endregion

    #region IUserEmailStore<User>

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        // ASP.NET Identity passes normalized (uppercase) email
        // Convert back to lowercase for our repository search
        var email = normalizedEmail?.ToLowerInvariant();
        return _userWriteRepository.GetByEmailAsync(email, cancellationToken);
    }

    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value);
    }

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailVerified);
    }

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email.Value.ToUpperInvariant());
    }

    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        // Email changes should go through User.UpdateEmail() domain method
        // This is required by interface but we handle it differently
        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailResult = Email.Create(email);
            if (emailResult.IsSuccess)
            {
                user.UpdateEmail(emailResult.Value);
            }
        }
        return Task.CompletedTask;
    }

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        if (confirmed && !user.EmailVerified)
        {
            user.VerifyEmail();
        }
        return Task.CompletedTask;
    }

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        // Email normalization is handled by our Email value object
        return Task.CompletedTask;
    }

    #endregion

    #region IUserRoleStore<User>

    public Task AddToRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        if (Enum.TryParse<Domain.Identity.Enums.UserRole>(roleName, true, out var role))
        {
            user.SetRole(role);
        }
        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken)
    {
        IList<string> roles = new List<string> { user.Role.ToString() };
        return Task.FromResult(roles);
    }

    public Task<IList<User>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        // GetUsersByRoleAsync method doesn't exist in repository
        // This is rarely used by Identity, so we'll return empty list
        // If needed, implement query in repository layer
        IList<User> emptyList = new List<User>();
        return Task.FromResult(emptyList);
    }

    public Task<bool> IsInRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Role.ToString().Equals(roleName, StringComparison.OrdinalIgnoreCase));
    }

    public Task RemoveFromRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        // In our domain, users always have exactly one role
        // Removing from a role means setting back to Customer (default)
        user.SetRole(Domain.Identity.Enums.UserRole.Customer);
        return Task.CompletedTask;
    }

    #endregion

    #region IUserLockoutStore<User>

    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.FailedLoginAttempts);
    }

    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken)
    {
        // Lockout is always enabled in our system
        return Task.FromResult(true);
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken)
    {
        // If user is not active and has failed attempts, they're locked out
        // Our domain uses permanent lockout that requires admin intervention
        // But we'll return a future date for Identity's compatibility
        if (!user.IsActive && user.FailedLoginAttempts >= 5)
        {
            // Return a far future date to indicate locked status
            return Task.FromResult<DateTimeOffset?>(DateTimeOffset.MaxValue);
        }
        return Task.FromResult<DateTimeOffset?>(null);
    }

    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        user.RecordLoginFailure();
        return Task.FromResult(user.FailedLoginAttempts);
    }

    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        user.RecordLoginSuccess();
        return Task.CompletedTask;
    }

    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        // Lockout is always enabled in our domain
        return Task.CompletedTask;
    }

    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        if (lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            // User is being locked out
            user.Deactivate();
        }
        else
        {
            // User lockout is being removed
            user.Activate();
        }
        return Task.CompletedTask;
    }

    #endregion

    #region IUserTwoFactorStore<User>

    public Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken)
    {
        // Two-factor is not yet implemented in our domain
        // Will be added in future enhancement
        return Task.FromResult(false);
    }

    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        // Two-factor support will be added to domain model in future
        return Task.CompletedTask;
    }

    #endregion

    #region IUserSecurityStampStore<User>

    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken)
    {
        // Security stamp can be derived from password hash or last password change
        // This invalidates all sessions when password changes
        return Task.FromResult<string?>(user.PasswordHash?.GetHashCode().ToString());
    }

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        // Security stamp is derived from password hash in our implementation
        // No need to store separately
        return Task.CompletedTask;
    }

    #endregion

    #region IUserPhoneNumberStore<User>

    public Task<string?> GetPhoneNumberAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Phone?.Value);
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(User user, CancellationToken cancellationToken)
    {
        // Phone verification not yet implemented
        return Task.FromResult(user.Phone != null);
    }

    public Task SetPhoneNumberAsync(User user, string? phoneNumber, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneResult = PhoneNumber.Create(phoneNumber);
            if (phoneResult.IsSuccess)
            {
                user.UpdatePersonalInfo(user.FullName, phoneResult.Value);
            }
        }
        return Task.CompletedTask;
    }

    public Task SetPhoneNumberConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        // Phone verification not yet implemented
        return Task.CompletedTask;
    }

    #endregion

    public void Dispose()
    {
        // UnitOfWork disposal is handled by DI container
    }
}
