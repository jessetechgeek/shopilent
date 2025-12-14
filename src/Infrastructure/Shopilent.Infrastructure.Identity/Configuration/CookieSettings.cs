namespace Shopilent.Infrastructure.Identity.Configuration;

public class CookieSettings
{
    public string AccessTokenName { get; set; } = "accessToken";
    public string RefreshTokenName { get; set; } = "refreshToken";
    public int AccessTokenExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 7;
    public string Domain { get; set; } = "";
    public bool SecureInProduction { get; set; } = true;
    public string SameSite { get; set; } = "Lax";
}
