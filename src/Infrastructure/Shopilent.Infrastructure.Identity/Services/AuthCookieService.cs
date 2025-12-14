using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Infrastructure.Identity.Configuration;

namespace Shopilent.Infrastructure.Identity.Services;

public class AuthCookieService : IAuthCookieService
{
    private readonly CookieSettings _cookieSettings;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string ClientPlatformHeader = "X-Client-Platform";
    private const string MobilePlatformValue = "mobile";

    public AuthCookieService(
        IOptions<CookieSettings> cookieSettings,
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor)
    {
        _cookieSettings = cookieSettings.Value;
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsWebClient()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return true;
        }

        if (!context.Request.Headers.TryGetValue(ClientPlatformHeader, out var platformValue))
        {
            return true;
        }

        return !platformValue.ToString().Equals(MobilePlatformValue, StringComparison.OrdinalIgnoreCase);
    }

    public void SetAuthCookies(string accessToken, string refreshToken)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return;
        }

        var isSecure = _cookieSettings.SecureInProduction && _environment.IsProduction();
        var sameSiteMode = ParseSameSiteMode(_cookieSettings.SameSite);

        var accessTokenOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = sameSiteMode,
            Expires = DateTimeOffset.UtcNow.AddMinutes(_cookieSettings.AccessTokenExpiryMinutes),
            Path = "/api",
            Domain = string.IsNullOrEmpty(_cookieSettings.Domain) ? null : _cookieSettings.Domain
        };

        context.Response.Cookies.Append(_cookieSettings.AccessTokenName, accessToken, accessTokenOptions);

        var refreshTokenOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = sameSiteMode,
            Expires = DateTimeOffset.UtcNow.AddDays(_cookieSettings.RefreshTokenExpiryDays),
            Path = "/api/v1/auth",
            Domain = string.IsNullOrEmpty(_cookieSettings.Domain) ? null : _cookieSettings.Domain
        };

        context.Response.Cookies.Append(_cookieSettings.RefreshTokenName, refreshToken, refreshTokenOptions);
    }

    public void ClearAuthCookies()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return;
        }

        var isSecure = _cookieSettings.SecureInProduction && _environment.IsProduction();
        var sameSiteMode = ParseSameSiteMode(_cookieSettings.SameSite);

        // Clear access token cookie
        var accessTokenOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = sameSiteMode,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Path = "/api",
            Domain = string.IsNullOrEmpty(_cookieSettings.Domain) ? null : _cookieSettings.Domain
        };

        context.Response.Cookies.Append(_cookieSettings.AccessTokenName, string.Empty, accessTokenOptions);

        // Clear refresh token cookie
        var refreshTokenOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = sameSiteMode,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Path = "/api/v1/auth",
            Domain = string.IsNullOrEmpty(_cookieSettings.Domain) ? null : _cookieSettings.Domain
        };

        context.Response.Cookies.Append(_cookieSettings.RefreshTokenName, string.Empty, refreshTokenOptions);
    }

    public string? GetRefreshTokenFromCookie()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return null;
        }

        return context.Request.Cookies.TryGetValue(_cookieSettings.RefreshTokenName, out var token)
            ? token
            : null;
    }

    public string? GetAccessTokenFromCookie()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return null;
        }

        return context.Request.Cookies.TryGetValue(_cookieSettings.AccessTokenName, out var token)
            ? token
            : null;
    }

    private static SameSiteMode ParseSameSiteMode(string sameSite)
    {
        return sameSite.ToLower() switch
        {
            "strict" => SameSiteMode.Strict,
            "lax" => SameSiteMode.Lax,
            "none" => SameSiteMode.None,
            _ => SameSiteMode.Lax
        };
    }
}
