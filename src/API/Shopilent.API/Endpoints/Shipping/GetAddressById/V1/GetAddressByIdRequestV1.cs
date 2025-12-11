using FastEndpoints;

namespace Shopilent.API.Endpoints.Shipping.GetAddressById.V1;

public class GetAddressByIdRequestV1
{
    [QueryParam]
    public Guid Id { get; init; }
}