using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Abstractions.Persistence;

public interface IUnitOfWork : IDisposable
{
    IProductReadRepository ProductReader { get; }
    IProductWriteRepository ProductWriter { get; }

    IProductVariantReadRepository ProductVariantReader { get; }
    IProductVariantWriteRepository ProductVariantWriter { get; }

    ICartReadRepository CartReader { get; }
    ICartWriteRepository CartWriter { get; }

    IOrderReadRepository OrderReader { get; }
    IOrderWriteRepository OrderWriter { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
