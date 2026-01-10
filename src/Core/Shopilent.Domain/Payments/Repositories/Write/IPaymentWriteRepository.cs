using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Repositories.Write;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Domain.Payments.Repositories.Write;

public interface IPaymentWriteRepository : IAggregateWriteRepository<Payment>
{
    // EF Core will be used for reads in write repository as well
    Task<Payment> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Payment> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<Payment> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);
}