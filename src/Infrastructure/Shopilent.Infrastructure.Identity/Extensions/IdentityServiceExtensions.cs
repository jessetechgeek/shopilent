using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Infrastructure.Identity.Configuration.Extensions;
using Shopilent.Infrastructure.Identity.Configuration.Settings;
using Shopilent.Infrastructure.Identity.Factories;
using Shopilent.Infrastructure.Identity.Services;

namespace Shopilent.Infrastructure.Identity.Extensions;

public static class IdentityServiceExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<Configuration.Settings.PasswordOptions>(options =>
        {
            options.SaltSize = 16;
            options.HashSize = 32;
            options.Iterations = 10000;
        });
        services.Configure<Configuration.CookieSettings>(configuration.GetSection("Cookies"));

        // Register services
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IAuthCookieService, AuthCookieService>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Register AuthenticationService using factory
        services.AddScoped<IAuthenticationService>(provider => 
            AuthenticationServiceFactory.Create(
                provider.GetRequiredService<Application.Abstractions.Persistence.IUnitOfWork>(),
                provider.GetRequiredService<Application.Abstractions.Email.IEmailService>(),
                provider.GetRequiredService<ILogger<AuthenticationService>>(),
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtSettings>>(),
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PasswordOptions>>()));

        // Add JWT authentication
        services.AddJwtAuthentication(configuration);
        
        services.AddAuthorizationPolicies();


        return services;
    }
}