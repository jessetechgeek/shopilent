using FastEndpoints;

namespace Shopilent.API.Endpoints.Users.GetUser.V1;

public class GetUserRequestV1
{
    [QueryParam]
    public Guid Id { get; set; }
}