using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Write;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Identity.Write;

public class UserWriteRepository : AggregateWriteRepositoryBase<User>, IUserWriteRepository
{
    public UserWriteRepository(ApplicationDbContext dbContext, ILogger<UserWriteRepository> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<User> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            return await DbContext.Users
                .Where(u => u.Email.Value == email)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while getting user by email: {Email}", email);
            throw;
        }
    }

    public async Task<User> GetByRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .Include(u => u.RefreshTokens)
            .Where(u => u.RefreshTokens.Any(rt => rt.Token == token && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User> GetByEmailVerificationTokenAsync(string token,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .Where(u => u.EmailVerificationToken == token && u.EmailVerificationExpires > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .Where(u => u.PasswordResetToken == token && u.PasswordResetExpires > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
