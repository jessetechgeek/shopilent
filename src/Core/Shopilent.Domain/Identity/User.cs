using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Errors;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Identity;

public class User : AggregateRoot
{
    private User()
    {
        // Required by EF Core
    }

    private User(Email email, string passwordHash, FullName fullName, UserRole role = UserRole.Customer)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Role = role;
        IsActive = true;
        EmailVerified = false;
        FailedLoginAttempts = 0;

        _addresses = new List<Address>();
        _refreshTokens = new List<RefreshToken>();
        _orders = new List<Order>();
    }

    public static Result<User> Create(Email email, string passwordHash, FullName fullName,
        UserRole role = UserRole.Customer)
    {
        if (email == null)
            return Result.Failure<User>(UserErrors.EmailRequired);

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>(UserErrors.PasswordRequired);

        if (fullName == null || string.IsNullOrWhiteSpace(fullName.FirstName))
            return Result.Failure<User>(UserErrors.FirstNameRequired);

        if (string.IsNullOrWhiteSpace(fullName.LastName))
            return Result.Failure<User>(UserErrors.LastNameRequired);

        var user = new User(email, passwordHash, fullName, role);
        user.AddDomainEvent(new UserCreatedEvent(user.Id));
        return Result.Success(user);
    }

    public static Result<User> CreatePreVerified(Email email, string passwordHash, FullName fullName,
        UserRole role = UserRole.Customer)
    {
        var result = Create(email, passwordHash, fullName, role);
        if (result.IsFailure)
            return result;

        var user = result.Value;
        user.EmailVerified = true;
        return Result.Success(user);
    }

    public static Result<User> CreateAdmin(Email email, string passwordHash, FullName fullName)
    {
        return Create(email, passwordHash, fullName, UserRole.Admin);
    }

    public static Result<User> CreateManager(Email email, string passwordHash, FullName fullName)
    {
        return Create(email, passwordHash, fullName, UserRole.Manager);
    }

    public Email Email { get; private set; }
    public FullName FullName { get; private set; }
    public string PasswordHash { get; private set; }
    public PhoneNumber Phone { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? LastLogin { get; private set; }
    public bool EmailVerified { get; private set; }
    public string EmailVerificationToken { get; private set; }
    public DateTime? EmailVerificationExpires { get; private set; }
    public string PasswordResetToken { get; private set; }
    public DateTime? PasswordResetExpires { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LastFailedAttempt { get; private set; }

    private readonly List<Address> _addresses = new();
    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private readonly List<Cart> _carts = new();
    public IReadOnlyCollection<Cart> Carts => _carts.AsReadOnly();

    private readonly List<Order> _orders = new();
    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();

    public Result UpdatePersonalInfo(FullName fullName, PhoneNumber phone = null)
    {
        if (fullName == null || string.IsNullOrWhiteSpace(fullName.FirstName))
            return Result.Failure(UserErrors.FirstNameRequired);

        if (string.IsNullOrWhiteSpace(fullName.LastName))
            return Result.Failure(UserErrors.LastNameRequired);

        FullName = fullName;
        Phone = phone;

        AddDomainEvent(new UserUpdatedEvent(Id));
        return Result.Success();
    }

    public Result UpdateEmail(Email email)
    {
        if (email == null)
            return Result.Failure(UserErrors.EmailRequired);

        Email = email;
        EmailVerified = false;
        GenerateEmailVerificationToken();

        AddDomainEvent(new UserEmailChangedEvent(Id, email.Value));
        return Result.Success();
    }

    public Result UpdatePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure(UserErrors.PasswordRequired);

        PasswordHash = passwordHash;
        RevokeAllRefreshTokens("Password changed");

        AddDomainEvent(new UserPasswordChangedEvent(Id));
        return Result.Success();
    }

    public Result SetRole(UserRole role)
    {
        Role = role;
        Console.WriteLine(Role);
        AddDomainEvent(new UserRoleChangedEvent(Id, role));
        return Result.Success();
    }

    public Result RecordLoginSuccess()
    {
        LastLogin = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LastFailedAttempt = null;
        return Result.Success();
    }

    public Result RecordLoginFailure()
    {
        // Reset attempts if last failure was more than 15 minutes ago
        if (LastFailedAttempt.HasValue &&
            DateTime.UtcNow - LastFailedAttempt.Value > TimeSpan.FromMinutes(15))
        {
            FailedLoginAttempts = 0;
        }

        FailedLoginAttempts++;
        LastFailedAttempt = DateTime.UtcNow;

        if (FailedLoginAttempts >= 5)
        {
            IsActive = false;
            AddDomainEvent(new UserLockedOutEvent(Id));
            return Result.Failure(UserErrors.AccountLocked);
        }

        return Result.Success();
    }

    public Result TryAutoUnlock()
    {
        // Auto-unlock if locked due to failed attempts and 30 minutes have passed
        if (!IsActive && LastFailedAttempt.HasValue)
        {
            if (DateTime.UtcNow - LastFailedAttempt.Value > TimeSpan.FromMinutes(30))
            {
                return Activate();
            }
        }
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Success();

        IsActive = true;
        FailedLoginAttempts = 0;
        LastFailedAttempt = null;

        AddDomainEvent(new UserStatusChangedEvent(Id, true));
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        RevokeAllRefreshTokens("User deactivated");

        AddDomainEvent(new UserStatusChangedEvent(Id, false));
        return Result.Success();
    }

    public Result VerifyEmail()
    {
        EmailVerified = true;
        EmailVerificationToken = null;
        EmailVerificationExpires = null;

        AddDomainEvent(new UserEmailVerifiedEvent(Id));
        return Result.Success();
    }

    public Result GenerateEmailVerificationToken()
    {
        EmailVerificationToken = Guid.NewGuid().ToString("N");
        EmailVerificationExpires = DateTime.UtcNow.AddDays(1);
        return Result.Success();
    }

    public Result GeneratePasswordResetToken()
    {
        PasswordResetToken = Guid.NewGuid().ToString("N");
        PasswordResetExpires = DateTime.UtcNow.AddHours(1);
        return Result.Success();
    }

    public Result ClearPasswordResetToken()
    {
        PasswordResetToken = null;
        PasswordResetExpires = null;
        return Result.Success();
    }

    public Result<RefreshToken> AddRefreshToken(string token, DateTime expiresAt, string ipAddress = null,
        string userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure<RefreshToken>(RefreshTokenErrors.EmptyToken);

        if (expiresAt <= DateTime.UtcNow)
            return Result.Failure<RefreshToken>(RefreshTokenErrors.InvalidExpiry);

        var refreshToken = RefreshToken.Create(this, token, expiresAt, ipAddress, userAgent);
        _refreshTokens.Add(refreshToken);
        return Result.Success(refreshToken);
    }

    public Result RevokeRefreshToken(string token, string reason)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(RefreshTokenErrors.EmptyToken);

        var refreshToken = _refreshTokens.Find(rt => rt.Token == token && !rt.IsRevoked);
        if (refreshToken == null)
            return Result.Failure(RefreshTokenErrors.NotFound(token));

        //throw failure if token is already revoked
        if (refreshToken.IsRevoked)
            return Result.Failure(RefreshTokenErrors.AlreadyRevoked);

        refreshToken.Revoke(reason);
        return Result.Success();
    }

    public Result RevokeAllRefreshTokens(string reason)
    {
        foreach (var token in _refreshTokens.FindAll(rt => !rt.IsRevoked))
        {
            token.Revoke(reason);
        }

        return Result.Success();
    }

    public Result<Address> AddAddress(
        PostalAddress postalAddress,
        AddressType addressType = AddressType.Shipping,
        PhoneNumber phone = null,
        bool isDefault = false)
    {
        if (postalAddress == null)
            return Result.Failure<Address>(AddressErrors.AddressLine1Required);

        var result = Address.Create(
            this.Id,
            postalAddress,
            addressType,
            phone,
            isDefault);

        if (isDefault)
        {
            // Update all other default addresses (only one default address allowed per user)
            foreach (var existingAddress in _addresses.FindAll(a => a.IsDefault))
            {
                existingAddress.SetDefault(false);
            }
        }

        _addresses.Add(result);
        return Result.Success(result);
    }

    public Result RemoveAddress(Guid addressId)
    {
        var address = _addresses.Find(a => a.Id == addressId);
        if (address == null)
            return Result.Failure(AddressErrors.NotFound(addressId));

        _addresses.Remove(address);
        return Result.Success();
    }
}
