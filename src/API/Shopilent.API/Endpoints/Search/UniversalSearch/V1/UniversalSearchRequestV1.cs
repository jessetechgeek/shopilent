using FastEndpoints;

namespace Shopilent.API.Endpoints.Search.UniversalSearch.V1;

public class UniversalSearchRequestV1
{
    [QueryParam]
    public string FiltersBase64 { get; init; } = "";
}