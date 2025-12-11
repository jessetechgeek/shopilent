using FastEndpoints;

namespace Shopilent.API.Endpoints.Sales.GetRecentOrders.V1;

public class GetRecentOrdersRequestV1
{
    [QueryParam]
    public int Count { get; init; } = 10;
}