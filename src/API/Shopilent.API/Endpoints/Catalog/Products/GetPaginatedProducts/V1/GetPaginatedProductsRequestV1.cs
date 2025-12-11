using FastEndpoints;

namespace Shopilent.API.Endpoints.Catalog.Products.GetPaginatedProducts.V1;

public class GetPaginatedProductsRequestV1
{
    [QueryParam]
    public string FiltersBase64 { get; init; } = "";
}
