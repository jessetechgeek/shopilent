using Shopilent.Application.Abstractions.Search;

namespace Shopilent.API.Endpoints.Catalog.Products.GetPaginatedProducts.V1;

public class GetPaginatedProductsResponseV1
{
    public ProductSearchResponseDto[] Items { get; init; } = [];
    public SearchFacets Facets { get; init; } = new();
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
}