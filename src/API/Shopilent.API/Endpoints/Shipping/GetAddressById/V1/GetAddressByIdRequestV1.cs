using FastEndpoints;

namespace Shopilent.API.Endpoints.Shipping.GetAddressById.V1;

public class GetAddressByIdRequestV1
{
    [BindFrom("id")]
    public Guid Id { get; init; }
}