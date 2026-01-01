using FastEndpoints;

namespace Shopilent.API.Endpoints.Search.QuickSearch.V1;

public class QuickSearchRequestV1
{
    [QueryParam]
    public string Query { get; init; } = "";

    [QueryParam]
    public int Limit { get; init; } = 5;
}
