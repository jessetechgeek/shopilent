using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;
    private IDbContextTransaction _transaction;
    private bool _disposed;

    public IProductReadRepository ProductReader { get; }
    public IProductWriteRepository ProductWriter { get; }

    public IAttributeReadRepository AttributeReader { get; }
    public IAttributeWriteRepository AttributeWriter { get; }

    public IProductVariantReadRepository ProductVariantReader { get; }
    public IProductVariantWriteRepository ProductVariantWriter { get; }

    public ICartReadRepository CartReader { get; }
    public ICartWriteRepository CartWriter { get; }

    public IOrderReadRepository OrderReader { get; }
    public IOrderWriteRepository OrderWriter { get; }

    public IPaymentReadRepository PaymentReader { get; }
    public IPaymentWriteRepository PaymentWriter { get; }

    public UnitOfWork(
        ApplicationDbContext dbContext,
        IProductReadRepository productRepository,
        IProductWriteRepository productWriter,
        IAttributeReadRepository attributeRepository,
        IAttributeWriteRepository attributeWriter,
        IProductVariantReadRepository productVariantRepository,
        IProductVariantWriteRepository productVariantWriter,
        ICartReadRepository cartRepository,
        ICartWriteRepository cartWriter,
        IOrderReadRepository orderRepository,
        IOrderWriteRepository orderWriter,
        IPaymentReadRepository paymentRepository,
        IPaymentWriteRepository paymentWriter,
        IPaymentMethodReadRepository paymentMethodRepository,
        IPaymentMethodWriteRepository paymentMethodWriter)
    {
        _dbContext = dbContext;
        ProductReader = productRepository;
        ProductWriter = productWriter;
        AttributeReader = attributeRepository;
        AttributeWriter = attributeWriter;
        ProductVariantReader = productVariantRepository;
        ProductVariantWriter = productVariantWriter;
        CartReader = cartRepository;
        CartWriter = cartWriter;
        OrderReader = orderRepository;
        OrderWriter = orderWriter;
        PaymentReader = paymentRepository;
        PaymentWriter = paymentWriter;
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
