using FastEndpoints;

namespace Shopilent.API.Endpoints.Identity.VerifyEmail.V1;

public class VerifyEmailRequestV1
{
    [BindFrom("token")]
    public string Token { get; init; }
}