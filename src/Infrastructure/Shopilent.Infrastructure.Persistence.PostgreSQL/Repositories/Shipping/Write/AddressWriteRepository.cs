using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Repositories.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Write;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Shipping.Write;

public class AddressWriteRepository : AggregateWriteRepositoryBase<Address>, IAddressWriteRepository
{
    public AddressWriteRepository(ApplicationDbContext dbContext, ILogger<AddressWriteRepository> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<Address> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Addresses
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Address>> GetByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Address> GetDefaultAddressAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Addresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
