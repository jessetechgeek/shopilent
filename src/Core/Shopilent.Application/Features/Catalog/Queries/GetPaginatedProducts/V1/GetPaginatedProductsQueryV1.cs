using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Search;

namespace Shopilent.Application.Features.Catalog.Queries.GetPaginatedProducts.V1;

public sealed record GetPaginatedProductsQueryV1 :
    IQuery<SearchResponse<ProductSearchResponseDto>>,
    ICachedQuery<SearchResponse<ProductSearchResponseDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string SortColumn { get; init; } = "Name";
    public bool SortDescending { get; init; } = false;
    public bool IsActiveOnly { get; init; } = true;
    
    public string SearchQuery { get; init; } = "";
    public Dictionary<string, string[]> AttributeFilters { get; init; } = new();
    public decimal? PriceMin { get; init; }
    public decimal? PriceMax { get; init; }
    public string[] CategorySlugs { get; init; } = [];
    public bool InStockOnly { get; init; } = false;

    public string CacheKey =>
        $"products-page-{PageNumber}-size-{PageSize}-sort-{SortColumn}-{SortDescending}-active-{IsActiveOnly}-search-{SearchQuery.GetHashCode()}-filters-{GetAttributeFiltersHash()}-price-{PriceMin}-{PriceMax}-categories-{string.Join(",", CategorySlugs)}-stock-{InStockOnly}";

    public TimeSpan? Expiration => TimeSpan.FromMinutes(15);
    
    private int GetAttributeFiltersHash()
    {
        if (!AttributeFilters.Any()) return 0;
        
        var hash = 17;
        foreach (var (key, values) in AttributeFilters.OrderBy(x => x.Key))
        {
            hash = hash * 23 + key.GetHashCode();
            hash = hash * 23 + string.Join(",", values.OrderBy(x => x)).GetHashCode();
        }
        return hash;
    }
}