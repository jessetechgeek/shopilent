using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Domain.Identity;
using Shopilent.Infrastructure.Identity.Abstractions;
using Shopilent.Infrastructure.Identity.Configuration;
using Shopilent.Infrastructure.Identity.Configuration.Extensions;
using Shopilent.Infrastructure.Identity.Configuration.Settings;
using Shopilent.Infrastructure.Identity.Factories;
using Shopilent.Infrastructure.Identity.Services;
using Shopilent.Infrastructure.Identity.Stores;

namespace Shopilent.Infrastructure.Identity.Extensions;

public static class IdentityServiceExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure auth provider settings
        services.Configure<AuthProviderSettings>(configuration.GetSection("Authentication"));
        var authProviderSettings = configuration.GetSection("Authentication").Get<AuthProviderSettings>()
            ?? new AuthProviderSettings();

        // Validate provider configuration
        if (!authProviderSettings.IsValid(out var errorMessage))
        {
            throw new InvalidOperationException($"Invalid authentication configuration: {errorMessage}");
        }

        // Configure options
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<Configuration.Settings.PasswordOptions>(options =>
        {
            options.SaltSize = 16;
            options.HashSize = 32;
            options.Iterations = 100000;
        });
        services.Configure<Configuration.CookieSettings>(configuration.GetSection("Cookies"));

        // Register common services (used by both providers)
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IAuthCookieService, AuthCookieService>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Register JWT and Password services (used by both providers)
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordService, PasswordService>();

        // Register authentication provider based on configuration
        if (authProviderSettings.IsAspNetIdentityProvider)
        {
            // ASP.NET Core Identity configuration
            services.AddIdentity<User, IdentityRole>(options =>
            {
                // Password settings (stronger security)
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 1;

                // Lockout settings (with time-based reset)
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.RequireUniqueEmail = true;

                // SignIn settings
                options.SignIn.RequireConfirmedEmail = false; // Can be enabled based on requirements
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddUserStore<CustomUserStore>()
            .AddRoleStore<CustomRoleStore>()
            .AddDefaultTokenProviders();
            // Note: Uses ASP.NET Identity's default PasswordHasher<User> (PBKDF2 with 100k+ iterations)

            // Register AspNetIdentityAuthenticationService
            services.AddScoped<IAuthenticationService, AspNetIdentityAuthenticationService>();

            Console.WriteLine("✓ Authentication Provider: ASP.NET Core Identity (with custom stores)");
        }
        else
        {
            // Custom JWT authentication (original implementation)
            services.AddScoped<IAuthenticationService>(provider =>
                AuthenticationServiceFactory.Create(
                    provider.GetRequiredService<Application.Abstractions.Persistence.IUnitOfWork>(),
                    provider.GetRequiredService<Application.Abstractions.Email.IEmailService>(),
                    provider.GetRequiredService<ILogger<AuthenticationService>>(),
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtSettings>>(),
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Configuration.Settings.PasswordOptions>>()));

            Console.WriteLine("✓ Authentication Provider: Custom JWT");
        }

        // Add JWT authentication (used by both providers for token generation/validation)
        services.AddJwtAuthentication(configuration);

        services.AddAuthorizationPolicies();

        return services;
    }
}
