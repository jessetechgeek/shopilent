using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Repositories.Read;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Domain.Payments.Repositories.Read;

public interface IPaymentReadRepository : IAggregateReadRepository<PaymentDto>
{
    Task<PaymentDto> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);

    Task<PaymentDto> GetByExternalReferenceAsync(string externalReference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentDto>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentDto>> GetByStatusAsync(PaymentStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentDto>> GetRecentPaymentsAsync(int count, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PaymentDto>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken cancellationToken = default);

}