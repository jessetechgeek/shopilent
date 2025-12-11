using FastEndpoints;

namespace Shopilent.API.Endpoints.Identity.VerifyEmail.V1;

public class VerifyEmailRequestV1
{
    [QueryParam]
    public string Token { get; init; }
}