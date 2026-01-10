using Dapper;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Abstractions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Read;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Read;

public class PaymentReadRepository : AggregateReadRepositoryBase<Payment, PaymentDto>, IPaymentReadRepository
{
    public PaymentReadRepository(IDapperConnectionFactory connectionFactory, ILogger<PaymentReadRepository> logger)
        : base(connectionFactory, logger)
    {
    }

    public override async Task<PaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            WHERE id = @Id";

        return await Connection.QueryFirstOrDefaultAsync<PaymentDto>(sql, new { Id = id });
    }

    public override async Task<IReadOnlyList<PaymentDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            ORDER BY created_at DESC";

        var paymentDtos = await Connection.QueryAsync<PaymentDto>(sql);
        return paymentDtos.ToList();
    }

    public async Task<PaymentDto> GetByTransactionIdAsync(string transactionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            return null;

        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            WHERE transaction_id = @TransactionId";

        return await Connection.QueryFirstOrDefaultAsync<PaymentDto>(sql, new { TransactionId = transactionId });
    }

    public async Task<PaymentDto> GetByExternalReferenceAsync(string externalReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
            return null;

        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            WHERE external_reference = @ExternalReference";

        return await Connection.QueryFirstOrDefaultAsync<PaymentDto>(sql,
            new { ExternalReference = externalReference });
    }

    public async Task<IReadOnlyList<PaymentDto>> GetByOrderIdAsync(Guid orderId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            WHERE order_id = @OrderId
            ORDER BY created_at DESC";

        var paymentDtos = await Connection.QueryAsync<PaymentDto>(sql, new { OrderId = orderId });
        return paymentDtos.ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> GetByStatusAsync(PaymentStatus status,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            WHERE status = @Status
            ORDER BY created_at DESC";

        var paymentDtos = await Connection.QueryAsync<PaymentDto>(sql, new { Status = status.ToString() });
        return paymentDtos.ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> GetRecentPaymentsAsync(int count,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            ORDER BY created_at DESC
            LIMIT @Count";

        var paymentDtos = await Connection.QueryAsync<PaymentDto>(sql, new { Count = count });
        return paymentDtos.ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> GetByPaymentMethodIdAsync(Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                order_id AS OrderId,
                user_id AS UserId,
                amount AS Amount,
                currency AS Currency,
                method AS MethodType,
                provider AS Provider,
                status AS Status,
                external_reference AS ExternalReference,
                transaction_id AS TransactionId,
                payment_method_id AS PaymentMethodId,
                processed_at AS ProcessedAt,
                error_message AS ErrorMessage,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM payments
            WHERE payment_method_id = @PaymentMethodId
            ORDER BY created_at DESC";

        var paymentDtos = await Connection.QueryAsync<PaymentDto>(sql, new { PaymentMethodId = paymentMethodId });
        return paymentDtos.ToList();
    }
}