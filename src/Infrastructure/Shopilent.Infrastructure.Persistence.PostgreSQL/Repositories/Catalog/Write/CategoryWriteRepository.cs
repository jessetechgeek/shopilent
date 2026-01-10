using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Common.Write;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Write;

public class CategoryWriteRepository : AggregateWriteRepositoryBase<Category>, ICategoryWriteRepository
{
    public CategoryWriteRepository(ApplicationDbContext dbContext, ILogger<CategoryWriteRepository> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<Category> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Categories
            .Include(c => c.ProductCategories)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Category> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await DbContext.Categories
            .Include(c => c.ProductCategories)
            .FirstOrDefaultAsync(c => c.Slug.Value == slug, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Categories.Where(c => c.Slug.Value == slug);

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}