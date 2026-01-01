using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Search;


namespace Shopilent.Application.Features.Search.Queries.UniversalSearch.V1;

public record UniversalSearchQueryV1(
    string Query = "",
    string[] CategorySlugs = default!,
    Dictionary<string, string[]> AttributeFilters = default!,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    bool InStockOnly = false,
    bool ActiveOnly = true,
    int PageNumber = 1,
    int PageSize = 20,
    string SortBy = "relevance",
    bool SortDescending = false
) : IQuery<SearchResponse<ProductSearchResponseDto>>
{
    public UniversalSearchQueryV1() : this("", Array.Empty<string>(), new Dictionary<string, string[]>()) { }
}