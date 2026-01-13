using Dapper;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Abstractions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Read;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Sales.Read;

public class CartReadRepository : AggregateReadRepositoryBase<Cart, CartDto>, ICartReadRepository
{
    public CartReadRepository(IDapperConnectionFactory connectionFactory, ILogger<CartReadRepository> logger)
        : base(connectionFactory, logger)
    {
    }

    public override async Task<CartDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                c.id AS Id,
                c.user_id AS UserId,
                c.metadata AS Metadata,
                c.created_at AS CreatedAt,
                c.updated_at AS UpdatedAt
            FROM carts c
            WHERE c.id = @Id";

        var cart = await Connection.QueryFirstOrDefaultAsync<CartDto>(sql, new { Id = id });

        if (cart != null)
        {
            // Load cart items
            const string itemsSql = @"
                SELECT
                    ci.id AS Id,
                    ci.cart_id AS CartId,
                    ci.product_id AS ProductId,
                    p.name AS ProductName,
                    p.slug AS ProductSlug,
                    ci.variant_id AS VariantId,
                    pv.sku AS VariantSku,
                    COALESCE(pv.price, p.base_price) AS UnitPrice,
                    p.currency AS Currency,
                    ci.quantity AS Quantity,
                    (COALESCE(pv.price, p.base_price) * ci.quantity) AS TotalPrice,
                    COALESCE(
                        (SELECT thumbnail_key FROM product_variant_images WHERE variant_id = ci.variant_id AND is_default = true LIMIT 1),
                        (SELECT thumbnail_key FROM product_images WHERE product_id = ci.product_id AND is_default = true LIMIT 1)
                    ) AS ImageUrl,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            cart.Items = (await Connection.QueryAsync<CartItemDto>(
                itemsSql, new { CartId = id })).ToList();

            // Calculate total amount and total items
            cart.TotalAmount = cart.Items.Sum(i => i.TotalPrice);
            cart.TotalItems = cart.Items.Sum(i => i.Quantity);
        }

        return cart;
    }

    public override async Task<IReadOnlyList<CartDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                user_id AS UserId,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM carts";

        var carts = await Connection.QueryAsync<CartDto>(sql);
        var cartList = carts.ToList();

        foreach (var cart in cartList)
        {
            // Load cart items
            const string itemsSql = @"
                SELECT
                    ci.id AS Id,
                    ci.cart_id AS CartId,
                    ci.product_id AS ProductId,
                    p.name AS ProductName,
                    p.slug AS ProductSlug,
                    ci.variant_id AS VariantId,
                    pv.sku AS VariantSku,
                    COALESCE(pv.price, p.base_price) AS UnitPrice,
                    p.currency AS Currency,
                    ci.quantity AS Quantity,
                    (COALESCE(pv.price, p.base_price) * ci.quantity) AS TotalPrice,
                    COALESCE(
                        (SELECT thumbnail_key FROM product_variant_images WHERE variant_id = ci.variant_id AND is_default = true LIMIT 1),
                        (SELECT thumbnail_key FROM product_images WHERE product_id = ci.product_id AND is_default = true LIMIT 1)
                    ) AS ImageUrl,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            cart.Items = (await Connection.QueryAsync<CartItemDto>(
                itemsSql, new { CartId = cart.Id })).ToList();

            // Calculate total amount and total items
            cart.TotalAmount = cart.Items.Sum(i => i.TotalPrice);
            cart.TotalItems = cart.Items.Sum(i => i.Quantity);
        }

        return cartList;
    }

    public async Task<CartDto> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                user_id AS UserId,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM carts
            WHERE user_id = @UserId
            ORDER BY updated_at DESC
            LIMIT 1";

        var cart = await Connection.QueryFirstOrDefaultAsync<CartDto>(sql, new { UserId = userId });

        if (cart != null)
        {
            // Load cart items
            const string itemsSql = @"
                SELECT
                    ci.id AS Id,
                    ci.cart_id AS CartId,
                    ci.product_id AS ProductId,
                    p.name AS ProductName,
                    p.slug AS ProductSlug,
                    ci.variant_id AS VariantId,
                    pv.sku AS VariantSku,
                    COALESCE(pv.price, p.base_price) AS UnitPrice,
                    p.currency AS Currency,
                    ci.quantity AS Quantity,
                    (COALESCE(pv.price, p.base_price) * ci.quantity) AS TotalPrice,
                    COALESCE(
                        (SELECT thumbnail_key FROM product_variant_images WHERE variant_id = ci.variant_id AND is_default = true LIMIT 1),
                        (SELECT thumbnail_key FROM product_images WHERE product_id = ci.product_id AND is_default = true LIMIT 1)
                    ) AS ImageUrl,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            cart.Items = (await Connection.QueryAsync<CartItemDto>(
                itemsSql, new { CartId = cart.Id })).ToList();

            // Calculate total amount and total items
            cart.TotalAmount = cart.Items.Sum(i => i.TotalPrice);
            cart.TotalItems = cart.Items.Sum(i => i.Quantity);
        }

        return cart;
    }

    public async Task<IReadOnlyList<CartDto>> GetAbandonedCartsAsync(TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.Subtract(olderThan);

        const string sql = @"
            SELECT 
                id AS Id,
                user_id AS UserId,
                metadata AS Metadata,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM carts
            WHERE updated_at < @CutoffDate
            AND EXISTS (
                SELECT 1 FROM cart_items ci
                WHERE ci.cart_id = carts.id
            )";

        var carts = await Connection.QueryAsync<CartDto>(sql, new { CutoffDate = cutoffDate });
        var cartList = carts.ToList();

        foreach (var cart in cartList)
        {
            // Load cart items
            const string itemsSql = @"
                SELECT
                    ci.id AS Id,
                    ci.cart_id AS CartId,
                    ci.product_id AS ProductId,
                    p.name AS ProductName,
                    p.slug AS ProductSlug,
                    ci.variant_id AS VariantId,
                    pv.sku AS VariantSku,
                    COALESCE(pv.price, p.base_price) AS UnitPrice,
                    p.currency AS Currency,
                    ci.quantity AS Quantity,
                    (COALESCE(pv.price, p.base_price) * ci.quantity) AS TotalPrice,
                    COALESCE(
                        (SELECT thumbnail_key FROM product_variant_images WHERE variant_id = ci.variant_id AND is_default = true LIMIT 1),
                        (SELECT thumbnail_key FROM product_images WHERE product_id = ci.product_id AND is_default = true LIMIT 1)
                    ) AS ImageUrl,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            cart.Items = (await Connection.QueryAsync<CartItemDto>(
                itemsSql, new { CartId = cart.Id })).ToList();

            // Calculate total amount and total items
            cart.TotalAmount = cart.Items.Sum(i => i.TotalPrice);
            cart.TotalItems = cart.Items.Sum(i => i.Quantity);
        }

        return cartList;
    }
}