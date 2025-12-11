using FastEndpoints;

namespace Shopilent.API.Endpoints.Catalog.Categories.GetPaginatedCategories.V1;

public class GetPaginatedCategoriesRequestV1
{
    [QueryParam]
    public int PageNumber { get; init; } = 1;

    [QueryParam]
    public int PageSize { get; init; } = 10;

    [QueryParam]
    public string SortColumn { get; init; } = "Name";

    [QueryParam]
    public bool SortDescending { get; init; } = false;
}