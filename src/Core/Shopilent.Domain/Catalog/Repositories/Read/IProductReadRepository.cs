using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Common.Repositories.Read;

namespace Shopilent.Domain.Catalog.Repositories.Read;

public interface IProductReadRepository : IAggregateReadRepository<ProductDto>
{
    Task<ProductDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDetailDto> GetDetailBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<ProductDto> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> SearchAsync(string searchTerm, Guid? categoryId = null,
        CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null, CancellationToken cancellationToken = default);
    
    Task<PaginatedResult<ProductDto>> GetPaginatedByCategoryAsync(
        Guid categoryId,
        int pageNumber, 
        int pageSize, 
        string sortColumn = null, 
        bool sortDescending = false,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<ProductDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    
    Task<DataTableResult<ProductDetailDto>> GetProductDetailDataTableAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default);
}