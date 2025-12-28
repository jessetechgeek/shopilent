using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;

namespace Shopilent.Application.Abstractions.Persistence;

public interface IUnitOfWork : IDisposable
{
    IProductReadRepository ProductReader { get; }
    IProductWriteRepository ProductWriter { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
