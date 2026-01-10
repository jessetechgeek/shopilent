using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Write;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Write;

public class ProductWriteRepository : AggregateWriteRepositoryBase<Product>, IProductWriteRepository
{
    public ProductWriteRepository(ApplicationDbContext dbContext, ILogger<ProductWriteRepository> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<Product> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await DbContext.Products
                .Include(p => p.Categories)
                .Include(p => p.Attributes)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching product by ID: {Id}", id);
            throw;
        }
    }


    public async Task<Product> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await DbContext.Products
            .Include(p => p.Categories)
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Slug.Value == slug, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Products.Where(p => p.Slug.Value == slug);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return false;

        var query = DbContext.Products.Where(p => p.Sku == sku);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
