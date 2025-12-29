using Microsoft.EntityFrameworkCore;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityName = ex.Entries.FirstOrDefault()?.Entity.GetType().Name ?? "Entity";
            var entityId = ex.Entries.FirstOrDefault()?.Property("Id")?.CurrentValue ?? "Unknown";
            throw new ConcurrencyConflictException(entityName, entityId, ex);
        }
    }
}
