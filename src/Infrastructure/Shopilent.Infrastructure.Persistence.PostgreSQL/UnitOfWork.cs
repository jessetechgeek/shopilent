using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;
    private IDbContextTransaction _transaction;
    private bool _disposed;

    public IProductReadRepository ProductReader { get; }
    public IProductWriteRepository ProductWriter { get; }

    public IProductVariantReadRepository ProductVariantReader { get; }
    public IProductVariantWriteRepository ProductVariantWriter { get; }

    public UnitOfWork(
        ApplicationDbContext dbContext,
        IProductReadRepository productRepository,
        IProductWriteRepository productWriter,
        IProductVariantReadRepository productVariantRepository,
        IProductVariantWriteRepository productVariantWriter)
    {
        _dbContext = dbContext;
        ProductReader = productRepository;
        ProductWriter = productWriter;
        ProductVariantReader = productVariantRepository;
        ProductVariantWriter = productVariantWriter;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
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

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _transaction?.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _transaction?.RollbackAsync(cancellationToken);
        }
        finally
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _transaction?.Dispose();
                _dbContext.Dispose();
            }

            _disposed = true;
        }
    }
}
