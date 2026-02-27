using Shopilent.API.Common.Configuration;
using Shopilent.API.Common.Middleware;

namespace Shopilent.API.Common.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        return services;
    }

    public static IApplicationBuilder UseApiRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IpRateLimitingMiddleware>();
    }
}
