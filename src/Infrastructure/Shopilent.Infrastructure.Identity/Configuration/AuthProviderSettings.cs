namespace Shopilent.Infrastructure.Identity.Configuration;

/// <summary>
/// Configuration for authentication provider selection.
/// Allows switching between different authentication implementations.
/// </summary>
public class AuthProviderSettings
{
    /// <summary>
    /// The authentication provider to use.
    /// Supported values: "Custom", "AspNetIdentity"
    /// </summary>
    public string Provider { get; set; } = "Custom";

    /// <summary>
    /// Validates the provider configuration.
    /// </summary>
    public bool IsValid(out string errorMessage)
    {
        var validProviders = new[] { "Custom", "AspNetIdentity" };

        if (string.IsNullOrWhiteSpace(Provider))
        {
            errorMessage = "Authentication provider not specified.";
            return false;
        }

        if (!validProviders.Contains(Provider, StringComparer.OrdinalIgnoreCase))
        {
            errorMessage = $"Invalid authentication provider '{Provider}'. " +
                          $"Valid options are: {string.Join(", ", validProviders)}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Checks if the Custom JWT provider is selected.
    /// </summary>
    public bool IsCustomProvider =>
        Provider.Equals("Custom", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the ASP.NET Core Identity provider is selected.
    /// </summary>
    public bool IsAspNetIdentityProvider =>
        Provider.Equals("AspNetIdentity", StringComparison.OrdinalIgnoreCase);
}
