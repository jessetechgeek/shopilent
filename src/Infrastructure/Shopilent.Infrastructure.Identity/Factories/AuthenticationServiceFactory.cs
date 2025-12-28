using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Infrastructure.Identity.Configuration.Settings;
using Shopilent.Infrastructure.Identity.Services;

namespace Shopilent.Infrastructure.Identity.Factories;

public static class AuthenticationServiceFactory
{
    public static IAuthenticationService Create(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        IUserReadRepository userReadRepository,
        IRefreshTokenWriteRepository refreshTokenWriteRepository,
        IEmailService emailService,
        ILogger<AuthenticationService> logger,
        IOptions<JwtSettings> jwtSettings,
        IOptions<PasswordOptions> passwordOptions)
    {
        var passwordService = new PasswordService(passwordOptions);
        var jwtService = new JwtService(jwtSettings);

        return new AuthenticationService(
            unitOfWork,
            userWriteRepository,
            userReadRepository,
            refreshTokenWriteRepository,
            jwtService,
            passwordService,
            emailService,
            logger);
    }
}
