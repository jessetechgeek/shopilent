using Dapper;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Repositories.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Abstractions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Read;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Shipping.Read;

public class AddressReadRepository : AggregateReadRepositoryBase<Address, AddressDto>, IAddressReadRepository
{
    public AddressReadRepository(IDapperConnectionFactory connectionFactory, ILogger<AddressReadRepository> logger)
        : base(connectionFactory, logger)
    {
    }

    public override async Task<AddressDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                user_id AS UserId,
                address_line1 AS AddressLine1,
                address_line2 AS AddressLine2,
                city AS City,
                state AS State,
                postal_code AS PostalCode,
                country AS Country,
                phone AS Phone,
                is_default AS IsDefault,
                address_type AS AddressType,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM addresses
            WHERE id = @Id";

        return await Connection.QueryFirstOrDefaultAsync<AddressDto>(sql, new { Id = id });
    }

    public override async Task<IReadOnlyList<AddressDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                user_id AS UserId,
                address_line1 AS AddressLine1,
                address_line2 AS AddressLine2,
                city AS City,
                state AS State,
                postal_code AS PostalCode,
                country AS Country,
                phone AS Phone,
                is_default AS IsDefault,
                address_type AS AddressType,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM addresses";

        var addressDtos = await Connection.QueryAsync<AddressDto>(sql);
        return addressDtos.ToList();
    }

    public async Task<IReadOnlyList<AddressDto>> GetByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                user_id AS UserId,
                address_line1 AS AddressLine1,
                address_line2 AS AddressLine2,
                city AS City,
                state AS State,
                postal_code AS PostalCode,
                country AS Country,
                phone AS Phone,
                is_default AS IsDefault,
                address_type AS AddressType,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM addresses
            WHERE user_id = @UserId
            ORDER BY is_default DESC, created_at DESC";

        var addressDtos = await Connection.QueryAsync<AddressDto>(sql, new { UserId = userId });
        return addressDtos.ToList();
    }

    public async Task<AddressDto> GetDefaultAddressAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                id AS Id,
                user_id AS UserId,
                address_line1 AS AddressLine1,
                address_line2 AS AddressLine2,
                city AS City,
                state AS State,
                postal_code AS PostalCode,
                country AS Country,
                phone AS Phone,
                is_default AS IsDefault,
                address_type AS AddressType,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM addresses
            WHERE user_id = @UserId
            AND is_default = true
            LIMIT 1";

        return await Connection.QueryFirstOrDefaultAsync<AddressDto>(sql, new { UserId = userId });
    }

    public async Task<IReadOnlyList<AddressDto>> GetByAddressTypeAsync(Guid userId, AddressType addressType,
        CancellationToken cancellationToken = default)
    {
        string sql;
        object parameters;

        if (addressType == AddressType.Both)
        {
            sql = @"
                SELECT 
                    id AS Id,
                    user_id AS UserId,
                    address_line1 AS AddressLine1,
                    address_line2 AS AddressLine2,
                    city AS City,
                    state AS State,
                    postal_code AS PostalCode,
                    country AS Country,
                    phone AS Phone,
                    is_default AS IsDefault,
                    address_type AS AddressType,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM addresses
                WHERE user_id = @UserId
                AND address_type = @AddressType
                ORDER BY is_default DESC, created_at DESC";

            parameters = new { UserId = userId, AddressType = addressType.ToString() };
        }
        else
        {
            sql = @"
                SELECT 
                    id AS Id,
                    user_id AS UserId,
                    address_line1 AS AddressLine1,
                    address_line2 AS AddressLine2,
                    city AS City,
                    state AS State,
                    postal_code AS PostalCode,
                    country AS Country,
                    phone AS Phone,
                    is_default AS IsDefault,
                    address_type AS AddressType,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM addresses
                WHERE user_id = @UserId
                AND (address_type = @AddressType OR address_type = 'Both')
                ORDER BY is_default DESC, created_at DESC";

            parameters = new { UserId = userId, AddressType = addressType.ToString() };
        }

        var addressDtos = await Connection.QueryAsync<AddressDto>(sql, parameters);
        return addressDtos.ToList();
    }
}