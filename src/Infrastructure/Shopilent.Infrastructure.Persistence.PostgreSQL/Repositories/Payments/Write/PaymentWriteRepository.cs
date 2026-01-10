using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Write;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Write;

public class PaymentWriteRepository : AggregateWriteRepositoryBase<Payment>, IPaymentWriteRepository
{
    public PaymentWriteRepository(ApplicationDbContext dbContext, ILogger<PaymentWriteRepository> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<Payment> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Payments
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Payment> GetByTransactionIdAsync(string transactionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            return null;

        return await DbContext.Payments
            .FirstOrDefaultAsync(p => p.TransactionId == transactionId, cancellationToken);
    }

    public async Task<Payment> GetByExternalReferenceAsync(string externalReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
            return null;

        return await DbContext.Payments
            .FirstOrDefaultAsync(p => p.ExternalReference == externalReference, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByOrderIdAsync(Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Payments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByStatusAsync(PaymentStatus status,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Payments
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}