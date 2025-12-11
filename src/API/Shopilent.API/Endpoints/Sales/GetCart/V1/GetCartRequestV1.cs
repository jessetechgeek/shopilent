using FastEndpoints;

namespace Shopilent.API.Endpoints.Sales.GetCart.V1;

public class GetCartRequestV1
{
    [QueryParam]
    public Guid? CartId { get; init; }
}