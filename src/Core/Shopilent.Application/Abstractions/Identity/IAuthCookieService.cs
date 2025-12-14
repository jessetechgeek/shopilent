namespace Shopilent.Application.Abstractions.Identity;

public interface IAuthCookieService
{
    bool IsWebClient();
    void SetAuthCookies(string accessToken, string refreshToken);
    void ClearAuthCookies();
    string? GetRefreshTokenFromCookie();
    string? GetAccessTokenFromCookie();
}
