using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Models;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Abstractions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Read;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Read;

public class ProductReadRepository : AggregateReadRepositoryBase<Product, ProductDto>, IProductReadRepository
{
    public ProductReadRepository(IDapperConnectionFactory connectionFactory, ILogger<ProductReadRepository> logger)
        : base(connectionFactory, logger)
    {
    }

    public override async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.description AS Description,
                p.base_price AS BasePrice,
                p.currency AS Currency,
                p.sku AS Sku,
                p.slug AS Slug,
                p.metadata AS Metadata,
                p.is_active AS IsActive,
                p.created_at AS CreatedAt,
                p.updated_at AS UpdatedAt
            FROM products p
            WHERE p.id = @Id";

        return await Connection.QueryFirstOrDefaultAsync<ProductDto>(sql, new { Id = id });
    }

    public override async Task<IReadOnlyList<ProductDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                id AS Id,
                name AS Name,
                description AS Description,
                base_price AS BasePrice,
                currency AS Currency,
                sku AS Sku,
                slug AS Slug,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM products
            ORDER BY name";

        var productDtos = await Connection.QueryAsync<ProductDto>(sql);
        return productDtos.ToList();
    }

    public async Task<ProductDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Single optimized query using PostgreSQL JSON aggregation to fetch everything at once
        const string sql = @"
        SELECT
            p.id AS Id,
            p.name AS Name,
            p.description AS Description,
            p.base_price AS BasePrice,
            p.currency AS Currency,
            p.sku AS Sku,
            p.slug AS Slug,
            p.metadata::text AS MetadataJson,
            p.is_active AS IsActive,
            p.created_by AS CreatedBy,
            p.modified_by AS ModifiedBy,
            p.last_modified AS LastModified,
            p.created_at AS CreatedAt,
            p.updated_at AS UpdatedAt,
            -- Categories as JSON array
            COALESCE(
                (SELECT jsonb_agg(
                    jsonb_build_object(
                        'id', c.id,
                        'name', c.name,
                        'description', c.description,
                        'parentId', c.parent_id,
                        'slug', c.slug,
                        'level', c.level,
                        'path', c.path,
                        'isActive', c.is_active,
                        'createdAt', c.created_at,
                        'updatedAt', c.updated_at
                    )
                )
                FROM categories c
                JOIN product_categories pc ON c.id = pc.category_id
                WHERE pc.product_id = p.id), '[]'::jsonb)::text AS CategoriesJson,
            -- Attributes as JSON array
            COALESCE(
                (SELECT jsonb_agg(
                    jsonb_build_object(
                        'id', pa.id,
                        'productId', pa.product_id,
                        'attributeId', pa.attribute_id,
                        'attributeName', a.name,
                        'attributeDisplayName', a.display_name,
                        'isVariant', a.is_variant,
                        'values', pa.values,
                        'createdAt', pa.created_at,
                        'updatedAt', pa.updated_at
                    )
                )
                FROM product_attributes pa
                JOIN attributes a ON pa.attribute_id = a.id
                WHERE pa.product_id = p.id), '[]'::jsonb)::text AS AttributesJson,
            -- Variants as JSON array with nested attributes and images
            COALESCE(
                (SELECT jsonb_agg(
                    jsonb_build_object(
                        'id', pv.id,
                        'productId', pv.product_id,
                        'sku', pv.sku,
                        'price', pv.price,
                        'currency', pv.currency,
                        'stockQuantity', pv.stock_quantity,
                        'isActive', pv.is_active,
                        'metadata', pv.metadata,
                        'createdAt', pv.created_at,
                        'updatedAt', pv.updated_at,
                        'attributes', COALESCE(
                            (SELECT jsonb_agg(
                                jsonb_build_object(
                                    'variantId', va.variant_id,
                                    'attributeId', va.attribute_id,
                                    'attributeName', a2.name,
                                    'attributeDisplayName', a2.display_name,
                                    'value', va.value
                                )
                            )
                            FROM variant_attributes va
                            JOIN attributes a2 ON va.attribute_id = a2.id
                            WHERE va.variant_id = pv.id), '[]'::jsonb),
                        'images', COALESCE(
                            (SELECT jsonb_agg(
                                jsonb_build_object(
                                    'imageKey', pvi.image_key,
                                    'thumbnailKey', pvi.thumbnail_key,
                                    'altText', pvi.alt_text,
                                    'isDefault', pvi.is_default,
                                    'displayOrder', pvi.display_order
                                ) ORDER BY pvi.display_order, pvi.is_default DESC
                            )
                            FROM product_variant_images pvi
                            WHERE pvi.variant_id = pv.id), '[]'::jsonb)
                    )
                )
                FROM product_variants pv
                WHERE pv.product_id = p.id), '[]'::jsonb)::text AS VariantsJson,
            -- Product images as JSON array
            COALESCE(
                (SELECT jsonb_agg(
                    jsonb_build_object(
                        'imageKey', pi.image_key,
                        'thumbnailKey', pi.thumbnail_key,
                        'altText', pi.alt_text,
                        'isDefault', pi.is_default,
                        'displayOrder', pi.display_order
                    ) ORDER BY pi.display_order, pi.is_default DESC
                )
                FROM product_images pi
                WHERE pi.product_id = p.id), '[]'::jsonb)::text AS ImagesJson
        FROM products p
        WHERE p.id = @Id";

        ProductDetailDto productDetail = null;

        using (var reader = await Connection.ExecuteReaderAsync(sql, new { Id = id }))
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            if (reader.Read())
            {
                productDetail = new ProductDetailDto
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Description")),
                    BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                    Currency = reader.GetString(reader.GetOrdinal("Currency")),
                    Sku = reader.IsDBNull(reader.GetOrdinal("Sku"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Sku")),
                    Slug = reader.GetString(reader.GetOrdinal("Slug")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy"))
                        ? null
                        : (Guid?)reader.GetGuid(reader.GetOrdinal("CreatedBy")),
                    ModifiedBy = reader.IsDBNull(reader.GetOrdinal("ModifiedBy"))
                        ? null
                        : (Guid?)reader.GetGuid(reader.GetOrdinal("ModifiedBy")),
                    LastModified = reader.IsDBNull(reader.GetOrdinal("LastModified"))
                        ? null
                        : (DateTime?)reader.GetDateTime(reader.GetOrdinal("LastModified")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                    Metadata = new Dictionary<string, object>(),
                    Categories = new List<CategoryDto>(),
                    Attributes = new List<ProductAttributeDto>(),
                    Variants = new List<ProductVariantDto>(),
                    Images = new List<ProductImageDto>()
                };

                // Parse metadata
                if (!reader.IsDBNull(reader.GetOrdinal("MetadataJson")))
                {
                    string metadataJson = reader.GetString(reader.GetOrdinal("MetadataJson"));
                    if (!string.IsNullOrEmpty(metadataJson))
                    {
                        try
                        {
                            productDetail.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                metadataJson, jsonOptions) ?? new Dictionary<string, object>();
                        }
                        catch
                        {
                            productDetail.Metadata = new Dictionary<string, object>();
                        }
                    }
                }

                // Parse categories
                string categoriesJson = reader.GetString(reader.GetOrdinal("CategoriesJson"));
                if (!string.IsNullOrEmpty(categoriesJson) && categoriesJson != "[]")
                {
                    try
                    {
                        productDetail.Categories = JsonSerializer.Deserialize<List<CategoryDto>>(
                            categoriesJson, jsonOptions) ?? new List<CategoryDto>();
                    }
                    catch
                    {
                        productDetail.Categories = new List<CategoryDto>();
                    }
                }

                // Parse attributes
                string attributesJson = reader.GetString(reader.GetOrdinal("AttributesJson"));
                if (!string.IsNullOrEmpty(attributesJson) && attributesJson != "[]")
                {
                    try
                    {
                        productDetail.Attributes = JsonSerializer.Deserialize<List<ProductAttributeDto>>(
                            attributesJson, jsonOptions) ?? new List<ProductAttributeDto>();
                    }
                    catch
                    {
                        productDetail.Attributes = new List<ProductAttributeDto>();
                    }
                }

                // Parse variants with nested attributes and images
                string variantsJson = reader.GetString(reader.GetOrdinal("VariantsJson"));
                if (!string.IsNullOrEmpty(variantsJson) && variantsJson != "[]")
                {
                    try
                    {
                        productDetail.Variants = JsonSerializer.Deserialize<List<ProductVariantDto>>(
                            variantsJson, jsonOptions) ?? new List<ProductVariantDto>();
                    }
                    catch
                    {
                        productDetail.Variants = new List<ProductVariantDto>();
                    }
                }

                // Parse product images
                string imagesJson = reader.GetString(reader.GetOrdinal("ImagesJson"));
                if (!string.IsNullOrEmpty(imagesJson) && imagesJson != "[]")
                {
                    try
                    {
                        productDetail.Images = JsonSerializer.Deserialize<List<ProductImageDto>>(
                            imagesJson, jsonOptions) ?? new List<ProductImageDto>();
                    }
                    catch
                    {
                        productDetail.Images = new List<ProductImageDto>();
                    }
                }
            }
        }

        return productDetail;
    }

    public async Task<ProductDto> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                id AS Id,
                name AS Name,
                description AS Description,
                base_price AS BasePrice,
                currency AS Currency,
                sku AS Sku,
                slug AS Slug,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM products
            WHERE slug = @Slug";

        return await Connection.QueryFirstOrDefaultAsync<ProductDto>(sql, new { Slug = slug });
    }

    public async Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.description AS Description,
                p.base_price AS BasePrice,
                p.currency AS Currency,
                p.sku AS Sku,
                p.slug AS Slug,
                p.is_active AS IsActive,
                p.created_at AS CreatedAt,
                p.updated_at AS UpdatedAt
            FROM products p
            JOIN product_categories pc ON p.id = pc.product_id
            WHERE pc.category_id = @CategoryId
            ORDER BY p.name";

        var productDtos = await Connection.QueryAsync<ProductDto>(sql, new { CategoryId = categoryId });
        return productDtos.ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string searchTerm, Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder(@"
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.description AS Description,
                p.base_price AS BasePrice,
                p.currency AS Currency,
                p.sku AS Sku,
                p.slug AS Slug,
                p.is_active AS IsActive,
                p.created_at AS CreatedAt,
                p.updated_at AS UpdatedAt
            FROM products p");

        var parameters = new DynamicParameters();
        parameters.Add("SearchTerm", $"%{searchTerm}%");

        if (categoryId.HasValue)
        {
            sql.Append(@"
                JOIN product_categories pc ON p.id = pc.product_id
                WHERE (p.name ILIKE @SearchTerm OR p.description ILIKE @SearchTerm OR p.sku ILIKE @SearchTerm)
                AND pc.category_id = @CategoryId");

            parameters.Add("CategoryId", categoryId.Value);
        }
        else
        {
            sql.Append(@"
                WHERE p.name ILIKE @SearchTerm OR p.description ILIKE @SearchTerm OR p.sku ILIKE @SearchTerm");
        }

        sql.Append(" ORDER BY p.name");

        var productDtos = await Connection.QueryAsync<ProductDto>(sql.ToString(), parameters);
        return productDtos.ToList();
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        string sql;
        object parameters;

        if (excludeId.HasValue)
        {
            sql = @"
                SELECT COUNT(1) FROM products
                WHERE slug = @Slug AND id != @ExcludeId";
            parameters = new { Slug = slug, ExcludeId = excludeId.Value };
        }
        else
        {
            sql = @"
                SELECT COUNT(1) FROM products
                WHERE slug = @Slug";
            parameters = new { Slug = slug };
        }

        int count = await Connection.ExecuteScalarAsync<int>(sql, parameters);
        return count > 0;
    }

    public async Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return false;

        string sql;
        object parameters;

        if (excludeId.HasValue)
        {
            sql = @"
                SELECT COUNT(1) FROM products
                WHERE sku = @Sku AND id != @ExcludeId";
            parameters = new { Sku = sku, ExcludeId = excludeId.Value };
        }
        else
        {
            sql = @"
                SELECT COUNT(1) FROM products
                WHERE sku = @Sku";
            parameters = new { Sku = sku };
        }

        int count = await Connection.ExecuteScalarAsync<int>(sql, parameters);
        return count > 0;
    }

    public override async Task<PaginatedResult<ProductDto>> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        string sortColumn = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        // Validate the sort column and provide a default
        string orderByClause;
        if (string.IsNullOrEmpty(sortColumn))
        {
            orderByClause = "p.name";
        }
        else
        {
            // Map the sortColumn (which might be a DTO property) to the actual DB column
            sortColumn = sortColumn.ToLower();
            orderByClause = sortColumn switch
            {
                "name" => "p.name",
                "price" or "baseprice" => "p.base_price",
                "sku" => "p.sku",
                "isactive" or "active" => "p.is_active",
                "createdat" or "created" => "p.created_at",
                _ => "p.name" // Default
            };
        }

        orderByClause += sortDescending ? " DESC" : " ASC";

        // Count query
        const string countSql = @"
        SELECT COUNT(*)
        FROM products p
        WHERE p.is_active = true";

        // Main query with images using JsonDictionaryTypeHandler
        var sql = $@"
        SELECT
            p.id AS Id,
            p.name AS Name,
            p.description AS Description,
            p.base_price AS BasePrice,
            p.currency AS Currency,
            p.sku AS Sku,
            p.slug AS Slug,
            p.metadata AS Metadata,
            p.is_active AS IsActive,
            p.created_at AS CreatedAt,
            p.updated_at AS UpdatedAt,
            -- Images as JSON array
            COALESCE(
                (SELECT jsonb_agg(
                    jsonb_build_object(
                        'imageKey', pi.image_key,
                        'thumbnailKey', pi.thumbnail_key,
                        'altText', pi.alt_text,
                        'isDefault', pi.is_default,
                        'displayOrder', pi.display_order
                    ) ORDER BY pi.display_order, pi.is_default DESC
                )
                FROM product_images pi
                WHERE pi.product_id = p.id), '[]'::jsonb)::text AS ImagesJson
        FROM products p
        WHERE p.is_active = true
        ORDER BY {orderByClause}
        LIMIT @PageSize OFFSET @Offset";

        var parameters = new
        {
            PageSize = pageSize,
            Offset = (pageNumber - 1) * pageSize
        };

        // Execute the queries
        var totalCount = await Connection.ExecuteScalarAsync<int>(countSql);

        // Execute query using Dapper mapping with JsonDictionaryTypeHandler
        var productDtos = await Connection.QueryAsync<ProductDto>(sql, parameters);

        return new PaginatedResult<ProductDto>(productDtos.AsList(), totalCount, pageNumber, pageSize);
    }

    // Override the DataTable method to provide custom implementation
    public override async Task<DataTableResult<ProductDto>> GetDataTableAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return new DataTableResult<ProductDto>(0, "Invalid request");

        try
        {
            // Base query
            var selectSql = new StringBuilder(@"
                SELECT
                    p.id AS Id,
                    p.name AS Name,
                    p.description AS Description,
                    p.base_price AS BasePrice,
                    p.currency AS Currency,
                    p.sku AS Sku,
                    p.slug AS Slug,
                    p.is_active AS IsActive,
                    p.created_at AS CreatedAt,
                    p.updated_at AS UpdatedAt,
                    -- Images as JSON array
                    COALESCE(
                        (SELECT jsonb_agg(
                            jsonb_build_object(
                                'imageKey', pi.image_key,
                                'thumbnailKey', pi.thumbnail_key,
                                'altText', pi.alt_text,
                                'isDefault', pi.is_default,
                                'displayOrder', pi.display_order
                            ) ORDER BY pi.display_order, pi.is_default DESC
                        )
                        FROM product_images pi
                        WHERE pi.product_id = p.id), '[]'::jsonb)::text AS ImagesJson
                FROM products p");

            // Count query
            const string countSql = "SELECT COUNT(*) FROM products p";

            // Where clause for filtering
            var whereClause = new StringBuilder();
            var parameters = new DynamicParameters();

            // Apply global search if provided
            if (!string.IsNullOrEmpty(request.Search?.Value))
            {
                whereClause.Append(" WHERE (");
                whereClause.Append("p.name ILIKE @SearchValue OR ");
                whereClause.Append("p.description ILIKE @SearchValue OR ");
                whereClause.Append("p.sku ILIKE @SearchValue OR ");
                whereClause.Append("p.slug ILIKE @SearchValue");
                whereClause.Append(")");
                parameters.Add("SearchValue", $"%{request.Search.Value}%");
            }

            // Build ORDER BY clause
            var orderByClause = new StringBuilder(" ORDER BY ");

            if (request.Order != null && request.Order.Any())
            {
                for (int i = 0; i < request.Order.Count; i++)
                {
                    if (i > 0) orderByClause.Append(", ");

                    var order = request.Order[i];
                    if (order.Column < request.Columns.Count)
                    {
                        var column = request.Columns[order.Column];
                        if (column.Orderable)
                        {
                            // Map column names to database columns
                            var dbColumn = column.Data.ToLower() switch
                            {
                                "name" => "p.name",
                                "price" or "baseprice" => "p.base_price",
                                "sku" => "p.sku",
                                "isactive" or "active" => "p.is_active",
                                "createdat" => "p.created_at",
                                _ => "p.name" // Default
                            };

                            orderByClause.Append($"{dbColumn} {(order.IsDescending ? "DESC" : "ASC")}");
                        }
                        else
                        {
                            orderByClause.Append("p.name ASC");
                        }
                    }
                    else
                    {
                        orderByClause.Append("p.name ASC");
                    }
                }
            }
            else
            {
                orderByClause.Append("p.name ASC");
            }

            // Pagination
            var paginationClause = " LIMIT @Length OFFSET @Start";
            parameters.Add("Length", request.Length);
            parameters.Add("Start", request.Start);

            // Build final queries
            var finalCountSql = countSql + whereClause.ToString();
            var finalSelectSql = selectSql.ToString() + whereClause.ToString() + orderByClause.ToString() +
                                 paginationClause;

            // Execute queries
            var totalCount = await Connection.ExecuteScalarAsync<int>(countSql);
            var filteredCount = whereClause.Length > 0
                ? await Connection.ExecuteScalarAsync<int>(finalCountSql, parameters)
                : totalCount;

            var productDtos = new List<ProductDto>();
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            using (var reader = await Connection.ExecuteReaderAsync(finalSelectSql, parameters))
            {
                while (reader.Read())
                {
                    var product = new ProductDto
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("Description")),
                        BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                        Currency = reader.GetString(reader.GetOrdinal("Currency")),
                        Sku = reader.IsDBNull(reader.GetOrdinal("Sku"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("Sku")),
                        Slug = reader.GetString(reader.GetOrdinal("Slug")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                        Metadata = new Dictionary<string, object>(),
                        Images = new List<ProductImageDto>()
                    };

                    // Parse images
                    string imagesJson = reader.GetString(reader.GetOrdinal("ImagesJson"));
                    if (!string.IsNullOrEmpty(imagesJson) && imagesJson != "[]")
                    {
                        try
                        {
                            product.Images = JsonSerializer.Deserialize<List<ProductImageDto>>(
                                imagesJson, jsonOptions) ?? new List<ProductImageDto>();
                        }
                        catch
                        {
                            product.Images = new List<ProductImageDto>();
                        }
                    }

                    productDtos.Add(product);
                }
            }

            return new DataTableResult<ProductDto>(request.Draw, totalCount, filteredCount, productDtos);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing ProductDto DataTable query");
            return new DataTableResult<ProductDto>(request.Draw, $"Error: {ex.Message}");
        }
    }

    public async Task<PaginatedResult<ProductDto>> GetPaginatedByCategoryAsync(
        Guid categoryId,
        int pageNumber,
        int pageSize,
        string sortColumn = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        // Validate the sort column and provide a default
        string orderByClause;
        if (string.IsNullOrEmpty(sortColumn))
        {
            orderByClause = "p.name";
        }
        else
        {
            // Map the sortColumn to the actual DB column
            sortColumn = sortColumn.ToLower();
            orderByClause = sortColumn switch
            {
                "name" => "p.name",
                "price" or "baseprice" => "p.base_price",
                "sku" => "p.sku",
                "isactive" or "active" => "p.is_active",
                "createdat" or "created" => "p.created_at",
                _ => "p.name" // Default
            };
        }

        orderByClause += sortDescending ? " DESC" : " ASC";

        // Count query
        const string countSql = @"
        SELECT COUNT(*)
        FROM products p
        JOIN product_categories pc ON p.id = pc.product_id
        WHERE pc.category_id = @CategoryId AND p.is_active = true";

        // Data query with pagination
        var sql = $@"
        SELECT
            p.id AS Id,
            p.name AS Name,
            p.description AS Description,
            p.base_price AS BasePrice,
            p.currency AS Currency,
            p.sku AS Sku,
            p.slug AS Slug,
            p.is_active AS IsActive,
            p.created_at AS CreatedAt,
            p.updated_at AS UpdatedAt
        FROM products p
        JOIN product_categories pc ON p.id = pc.product_id
        WHERE pc.category_id = @CategoryId AND p.is_active = true
        ORDER BY {orderByClause}
        LIMIT @PageSize OFFSET @Offset";

        var parameters = new
        {
            CategoryId = categoryId,
            PageSize = pageSize,
            Offset = (pageNumber - 1) * pageSize
        };

        // Execute the queries
        var totalCount = await Connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await Connection.QueryAsync<ProductDto>(sql, parameters);

        return new PaginatedResult<ProductDto>(items.AsList(), totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<ProductDto>> GetByIdsAsync(IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
            return new List<ProductDto>();

        // Convert the IDs to an array for SQL parameters
        var idArray = ids.ToArray();

        const string sql = @"
        SELECT
            p.id AS Id,
            p.name AS Name,
            p.description AS Description,
            p.base_price AS BasePrice,
            p.currency AS Currency,
            p.sku AS Sku,
            p.slug AS Slug,
            p.is_active AS IsActive,
            p.created_at AS CreatedAt,
            p.updated_at AS UpdatedAt
        FROM products p
        WHERE p.id = ANY(@Ids)
        ORDER BY array_position(@Ids, p.id)";

        var parameters = new { Ids = idArray };
        var productDtos = await Connection.QueryAsync<ProductDto>(sql, parameters);
        return productDtos.ToList();
    }


    public async Task<DataTableResult<ProductDetailDto>> GetProductDetailDataTableAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
                return new DataTableResult<ProductDetailDto>(0, "Invalid request");

            // Build the main product query with embedded counts using window functions
            var whereClause = new StringBuilder();
            var parameters = new DynamicParameters();

            // Apply global search if provided
            if (!string.IsNullOrEmpty(request.Search?.Value))
            {
                whereClause.Append(" WHERE (");
                whereClause.Append("p.name ILIKE @SearchValue OR ");
                whereClause.Append("p.description ILIKE @SearchValue OR ");
                whereClause.Append("p.sku ILIKE @SearchValue OR ");
                whereClause.Append("p.slug ILIKE @SearchValue");
                whereClause.Append(")");
                parameters.Add("SearchValue", $"%{request.Search.Value}%");
            }

            // Build ORDER BY clause
            var orderByClause = new StringBuilder(" ORDER BY ");

            if (request.Order != null && request.Order.Any())
            {
                for (int i = 0; i < request.Order.Count; i++)
                {
                    if (i > 0) orderByClause.Append(", ");

                    var order = request.Order[i];
                    if (order.Column < request.Columns.Count)
                    {
                        var column = request.Columns[order.Column];
                        if (column.Orderable)
                        {
                            // Map column names to database columns
                            var dbColumn = column.Data.ToLower() switch
                            {
                                "name" => "p.name",
                                "price" or "baseprice" => "p.base_price",
                                "sku" => "p.sku",
                                "isactive" or "active" => "p.is_active",
                                "createdat" => "p.created_at",
                                _ => "p.name" // Default
                            };

                            orderByClause.Append($"{dbColumn} {(order.IsDescending ? "DESC" : "ASC")}");
                        }
                        else
                        {
                            orderByClause.Append("p.name ASC");
                        }
                    }
                    else
                    {
                        orderByClause.Append("p.name ASC");
                    }
                }
            }
            else
            {
                orderByClause.Append("p.name ASC");
            }

            // Pagination
            var paginationClause = " LIMIT @Length OFFSET @Start";
            parameters.Add("Length", request.Length);
            parameters.Add("Start", request.Start);

            // Get total count first
            const string totalCountSql = "SELECT COUNT(*) FROM products";
            var totalCount = await Connection.ExecuteScalarAsync<int>(totalCountSql);

            // Get filtered count
            var filteredCountSql = "SELECT COUNT(*) FROM products p" + whereClause.ToString();
            var filteredCount = whereClause.Length > 0
                ? await Connection.ExecuteScalarAsync<int>(filteredCountSql, parameters)
                : totalCount;

            // Main data query with JSON aggregation
            var productSql = @"
            WITH selected_products AS (
                SELECT
                    p.id,
                    p.name,
                    p.description,
                    p.base_price,
                    p.sku,
                    p.slug,
                    p.is_active,
                    p.metadata,
                    p.created_by,
                    p.modified_by,
                    p.last_modified,
                    p.created_at,
                    p.updated_at
                FROM products p"
                             + whereClause.ToString()
                             + orderByClause.ToString()
                             + paginationClause + @"
            )
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.description AS Description,
                p.base_price AS BasePrice,
                'USD' AS Currency,
                p.sku AS Sku,
                p.slug AS Slug,
                p.is_active AS IsActive,
                p.metadata AS Metadata,
                p.created_by AS CreatedBy,
                p.modified_by AS ModifiedBy,
                p.last_modified AS LastModified,
                p.created_at AS CreatedAt,
                p.updated_at AS UpdatedAt,
                -- Categories as JSON array
                COALESCE(
                    (SELECT jsonb_agg(
                        jsonb_build_object(
                            'id', c.id,
                            'name', c.name,
                            'description', c.description,
                            'parentId', c.parent_id,
                            'slug', c.slug,
                            'level', c.level,
                            'path', c.path,
                            'isActive', c.is_active,
                            'createdAt', c.created_at,
                            'updatedAt', c.updated_at
                        )
                    )
                    FROM categories c
                    JOIN product_categories pc ON c.id = pc.category_id
                    WHERE pc.product_id = p.id
                    GROUP BY pc.product_id), '[]'::jsonb)::text AS CategoriesJson,
                -- Attributes as JSON array
                COALESCE(
                    (SELECT jsonb_agg(
                        jsonb_build_object(
                            'id', pa.id,
                            'productId', pa.product_id,
                            'attributeId', pa.attribute_id,
                            'attributeName', a.name,
                            'attributeDisplayName', a.display_name,
                            'values', pa.values,
                            'createdAt', pa.created_at,
                            'updatedAt', pa.updated_at
                        )
                    )
                    FROM product_attributes pa
                    JOIN attributes a ON pa.attribute_id = a.id
                    WHERE pa.product_id = p.id
                    GROUP BY pa.product_id), '[]'::jsonb)::text AS AttributesJson,
                -- Variants as JSON array with nested attributes and images
                COALESCE(
                    (SELECT jsonb_agg(
                        jsonb_build_object(
                            'id', pv.id,
                            'productId', pv.product_id,
                            'sku', pv.sku,
                            'price', pv.price,
                            'currency', 'USD',
                            'stockQuantity', pv.stock_quantity,
                            'isActive', pv.is_active,
                            'metadata', pv.metadata,
                            'createdAt', pv.created_at,
                            'updatedAt', pv.updated_at,
                            'attributes', COALESCE(
                                (SELECT jsonb_agg(
                                    jsonb_build_object(
                                        'variantId', va.variant_id,
                                        'attributeId', va.attribute_id,
                                        'attributeName', a2.name,
                                        'attributeDisplayName', a2.display_name,
                                        'value', va.value
                                    )
                                )
                                FROM variant_attributes va
                                JOIN attributes a2 ON va.attribute_id = a2.id
                                WHERE va.variant_id = pv.id
                                GROUP BY va.variant_id), '[]'::jsonb),
                            'images', COALESCE(
                                (SELECT jsonb_agg(
                                    jsonb_build_object(
                                        'imageKey', pvi.image_key,
                                        'thumbnailKey', pvi.thumbnail_key,
                                        'altText', pvi.alt_text,
                                        'isDefault', pvi.is_default,
                                        'displayOrder', pvi.display_order
                                    ) ORDER BY pvi.display_order, pvi.is_default DESC
                                )
                                FROM product_variant_images pvi
                                WHERE pvi.variant_id = pv.id), '[]'::jsonb)
                        )
                    )
                    FROM product_variants pv
                    WHERE pv.product_id = p.id
                    GROUP BY pv.product_id), '[]'::jsonb)::text AS VariantsJson,
                -- Product images as JSON array
                COALESCE(
                    (SELECT jsonb_agg(
                        jsonb_build_object(
                            'imageKey', pi.image_key,
                            'thumbnailKey', pi.thumbnail_key,
                            'altText', pi.alt_text,
                            'isDefault', pi.is_default,
                            'displayOrder', pi.display_order
                        ) ORDER BY pi.display_order, pi.is_default DESC
                    )
                    FROM product_images pi
                    WHERE pi.product_id = p.id), '[]'::jsonb)::text AS ImagesJson
            FROM selected_products p";

            // Create a list to hold the results
            var productDtos = new List<ProductDetailDto>();

            // Execute data query and process results
            using (var reader = await Connection.ExecuteReaderAsync(productSql, parameters))
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                while (reader.Read())
                {
                    // Create base product DTO
                    var product = new ProductDetailDto
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("Description")),
                        BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                        Currency = reader.GetString(reader.GetOrdinal("Currency")),
                        Sku = reader.IsDBNull(reader.GetOrdinal("Sku"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("Sku")),
                        Slug = reader.GetString(reader.GetOrdinal("Slug")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy"))
                            ? null
                            : (Guid?)reader.GetGuid(reader.GetOrdinal("CreatedBy")),
                        ModifiedBy = reader.IsDBNull(reader.GetOrdinal("ModifiedBy"))
                            ? null
                            : (Guid?)reader.GetGuid(reader.GetOrdinal("ModifiedBy")),
                        LastModified = reader.IsDBNull(reader.GetOrdinal("LastModified"))
                            ? null
                            : (DateTime?)reader.GetDateTime(reader.GetOrdinal("LastModified")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                        Metadata = new Dictionary<string, object>(),
                        Categories = new List<CategoryDto>(),
                        Attributes = new List<ProductAttributeDto>(),
                        Variants = new List<ProductVariantDto>(),
                        Images = new List<ProductImageDto>()
                    };

                    // Metadata is automatically handled by JsonDictionaryTypeHandler
                    // No manual parsing needed

                    // Parse categories
                    string categoriesJson = reader.GetString(reader.GetOrdinal("CategoriesJson"));
                    if (!string.IsNullOrEmpty(categoriesJson) && categoriesJson != "[]")
                    {
                        product.Categories = JsonSerializer.Deserialize<List<CategoryDto>>(
                            categoriesJson, jsonOptions);
                    }

                    // Parse attributes
                    string attributesJson = reader.GetString(reader.GetOrdinal("AttributesJson"));
                    if (!string.IsNullOrEmpty(attributesJson) && attributesJson != "[]")
                    {
                        product.Attributes = JsonSerializer.Deserialize<List<ProductAttributeDto>>(
                            attributesJson, jsonOptions);
                    }

                    // Parse variants
                    string variantsJson = reader.GetString(reader.GetOrdinal("VariantsJson"));
                    if (!string.IsNullOrEmpty(variantsJson) && variantsJson != "[]")
                    {
                        product.Variants = JsonSerializer.Deserialize<List<ProductVariantDto>>(
                            variantsJson, jsonOptions);
                    }

                    // Parse product images
                    string imagesJson = reader.GetString(reader.GetOrdinal("ImagesJson"));
                    if (!string.IsNullOrEmpty(imagesJson) && imagesJson != "[]")
                    {
                        try
                        {
                            product.Images = JsonSerializer.Deserialize<List<ProductImageDto>>(
                                imagesJson, jsonOptions) ?? new List<ProductImageDto>();
                        }
                        catch
                        {
                            product.Images = new List<ProductImageDto>();
                        }
                    }

                    productDtos.Add(product);
                }
            }

            return new DataTableResult<ProductDetailDto>(request.Draw, totalCount, filteredCount, productDtos);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing ProductDetailDto DataTable query: {ErrorMessage}", ex.Message);
            return new DataTableResult<ProductDetailDto>(request.Draw, $"Error: {ex.Message}");
        }
    }
}
