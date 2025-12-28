using Shopilent.Domain.Audit.Repositories;
using Shopilent.Domain.Audit.Repositories.Read;
using Shopilent.Domain.Audit.Repositories.Write;
using Shopilent.Domain.Catalog.Repositories;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Repositories;
using Shopilent.Domain.Identity.Repositories;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Outbox.Repositories.Read;
using Shopilent.Domain.Outbox.Repositories.Write;
using Shopilent.Domain.Payments.Repositories;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Domain.Shipping.Repositories;
using Shopilent.Domain.Shipping.Repositories.Read;
using Shopilent.Domain.Shipping.Repositories.Write;

namespace Shopilent.Application.Abstractions.Persistence;

public interface IUnitOfWork : IDisposable
{
    IProductReadRepository ProductReader { get; }
    IProductWriteRepository ProductWriter { get; }

    IAttributeReadRepository AttributeReader { get; }
    IAttributeWriteRepository AttributeWriter { get; }

    IProductVariantReadRepository ProductVariantReader { get; }
    IProductVariantWriteRepository ProductVariantWriter { get; }

    IUserReadRepository UserReader { get; }
    IUserWriteRepository UserWriter { get; }

    IRefreshTokenReadRepository RefreshTokenReader { get; }
    IRefreshTokenWriteRepository RefreshTokenWriter { get; }

    ICartReadRepository CartReader { get; }
    ICartWriteRepository CartWriter { get; }

    IOrderReadRepository OrderReader { get; }
    IOrderWriteRepository OrderWriter { get; }

    IPaymentReadRepository PaymentReader { get; }
    IPaymentWriteRepository PaymentWriter { get; }

    IPaymentMethodReadRepository PaymentMethodReader { get; }
    IPaymentMethodWriteRepository PaymentMethodWriter { get; }

    IAddressReadRepository AddressReader { get; }
    IAddressWriteRepository AddressWriter { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
