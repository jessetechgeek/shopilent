namespace Shopilent.API.Endpoints.Identity.Logout.V1;

public class LogoutRequestV1
{
    public string? RefreshToken { get; init; }
    public string Reason { get; init; } = "User logged out";
}