using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;

namespace Shopilent.Domain.Identity;

public class RefreshToken : Entity
{
    private RefreshToken()
    {
        // Required by EF Core
    }

    private RefreshToken(Guid userId, string token, DateTime expiresAt, string ipAddress = null,
        string userAgent = null)
    {
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        IssuedAt = DateTime.UtcNow;
        IsRevoked = false;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    internal static RefreshToken Create(Guid userId, string token, DateTime expiresAt, string ipAddress = null,
        string userAgent = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentNullException(nameof(userId));

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be empty", nameof(token));

        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiry date must be in the future", nameof(expiresAt));

        return new RefreshToken(userId, token, expiresAt, ipAddress, userAgent);
    }

    internal static Result<RefreshToken> CreateWithStandardExpiry(Guid userId, string token, string ipAddress = null,
        string userAgent = null)
    {
        if (userId == Guid.Empty)
            return Result.Failure<RefreshToken>(UserErrors.NotFound(Guid.Empty));

        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure<RefreshToken>(RefreshTokenErrors.EmptyToken);

        return Result.Success(new RefreshToken(userId, token, DateTime.UtcNow.AddDays(7), ipAddress, userAgent));
    }

    public Guid UserId { get; private set; }
    public string Token { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string RevokedReason { get; private set; }
    public string IpAddress { get; private set; }
    public string UserAgent { get; private set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    public Result Revoke(string reason)
    {
        if (IsRevoked)
            return Result.Success(); // Already revoked

        IsRevoked = true;
        RevokedReason = reason;
        return Result.Success();
    }
}
