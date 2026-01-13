using System.Text.Json;
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
                    COALESCE(
                        (SELECT jsonb_agg(
                            jsonb_build_object(
                                'variantId', va.variant_id,
                                'attributeId', va.attribute_id,
                                'attributeName', a.name,
                                'attributeDisplayName', a.display_name,
                                'value', va.value
                            )
                        )
                        FROM variant_attributes va
                        JOIN attributes a ON va.attribute_id = a.id
                        WHERE va.variant_id = ci.variant_id), '[]'::jsonb
                    )::text AS VariantAttributesJson,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            var rawItems = await Connection.QueryAsync<CartItemRaw>(itemsSql, new { CartId = id });
            cart.Items = DeserializeCartItems(rawItems);

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
                    COALESCE(
                        (SELECT jsonb_agg(
                            jsonb_build_object(
                                'variantId', va.variant_id,
                                'attributeId', va.attribute_id,
                                'attributeName', a.name,
                                'attributeDisplayName', a.display_name,
                                'value', va.value
                            )
                        )
                        FROM variant_attributes va
                        JOIN attributes a ON va.attribute_id = a.id
                        WHERE va.variant_id = ci.variant_id), '[]'::jsonb
                    )::text AS VariantAttributesJson,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            var rawItems = await Connection.QueryAsync<CartItemRaw>(itemsSql, new { CartId = cart.Id });
            cart.Items = DeserializeCartItems(rawItems);

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
                    COALESCE(
                        (SELECT jsonb_agg(
                            jsonb_build_object(
                                'variantId', va.variant_id,
                                'attributeId', va.attribute_id,
                                'attributeName', a.name,
                                'attributeDisplayName', a.display_name,
                                'value', va.value
                            )
                        )
                        FROM variant_attributes va
                        JOIN attributes a ON va.attribute_id = a.id
                        WHERE va.variant_id = ci.variant_id), '[]'::jsonb
                    )::text AS VariantAttributesJson,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            var rawItems = await Connection.QueryAsync<CartItemRaw>(itemsSql, new { CartId = cart.Id });
            cart.Items = DeserializeCartItems(rawItems);

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
                    COALESCE(
                        (SELECT jsonb_agg(
                            jsonb_build_object(
                                'variantId', va.variant_id,
                                'attributeId', va.attribute_id,
                                'attributeName', a.name,
                                'attributeDisplayName', a.display_name,
                                'value', va.value
                            )
                        )
                        FROM variant_attributes va
                        JOIN attributes a ON va.attribute_id = a.id
                        WHERE va.variant_id = ci.variant_id), '[]'::jsonb
                    )::text AS VariantAttributesJson,
                    ci.created_at AS CreatedAt,
                    ci.updated_at AS UpdatedAt
                FROM cart_items ci
                JOIN products p ON ci.product_id = p.id
                LEFT JOIN product_variants pv ON ci.variant_id = pv.id
                WHERE ci.cart_id = @CartId
                ORDER BY ci.created_at DESC";

            var rawItems = await Connection.QueryAsync<CartItemRaw>(itemsSql, new { CartId = cart.Id });
            cart.Items = DeserializeCartItems(rawItems);

            // Calculate total amount and total items
            cart.TotalAmount = cart.Items.Sum(i => i.TotalPrice);
            cart.TotalItems = cart.Items.Sum(i => i.Quantity);
        }

        return cartList;
    }

    private static List<CartItemDto> DeserializeCartItems(IEnumerable<CartItemRaw> rawItems)
    {
        var items = new List<CartItemDto>();

        foreach (var raw in rawItems)
        {
            var item = new CartItemDto
            {
                Id = raw.Id,
                CartId = raw.CartId,
                ProductId = raw.ProductId,
                ProductName = raw.ProductName,
                ProductSlug = raw.ProductSlug,
                VariantId = raw.VariantId,
                VariantSku = raw.VariantSku,
                UnitPrice = raw.UnitPrice,
                Currency = raw.Currency,
                Quantity = raw.Quantity,
                TotalPrice = raw.TotalPrice,
                ImageUrl = raw.ImageUrl,
                CreatedAt = raw.CreatedAt,
                UpdatedAt = raw.UpdatedAt,
                VariantAttributes = new List<CartItemVariantAttributeDto>()
            };

            if (!string.IsNullOrEmpty(raw.VariantAttributesJson))
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    // Deserialize to temporary raw attribute structure
                    var rawAttributes = JsonSerializer.Deserialize<List<RawVariantAttribute>>(
                        raw.VariantAttributesJson, options);

                    if (rawAttributes != null)
                    {
                        // Map to simplified DTO
                        item.VariantAttributes = rawAttributes.Select(attr => new CartItemVariantAttributeDto
                        {
                            AttributeDisplayName = attr.AttributeDisplayName,
                            Value = ExtractValueString(attr.Value)
                        }).ToList();
                    }
                }
                catch
                {
                    item.VariantAttributes = new List<CartItemVariantAttributeDto>();
                }
            }

            items.Add(item);
        }

        return items;
    }

    private static string ExtractValueString(JsonElement valueElement)
    {
        try
        {
            // If it's a JSON object with a "value" property, extract it
            if (valueElement.ValueKind == JsonValueKind.Object)
            {
                if (valueElement.TryGetProperty("value", out var nestedValue))
                {
                    return nestedValue.GetString() ?? string.Empty;
                }
            }
            // If it's a direct string value
            else if (valueElement.ValueKind == JsonValueKind.String)
            {
                return valueElement.GetString() ?? string.Empty;
            }

            // Fallback: convert to string
            return valueElement.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private class RawVariantAttribute
    {
        public string AttributeDisplayName { get; set; }
        public JsonElement Value { get; set; }
    }

    private class CartItemRaw
    {
        public Guid Id { get; set; }
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductSlug { get; set; }
        public Guid? VariantId { get; set; }
        public string VariantSku { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string ImageUrl { get; set; }
        public string VariantAttributesJson { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}