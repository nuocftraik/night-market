namespace NightMarket.WebApi.Infrastructure.Auth;

/// <summary>
/// Security configuration settings
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Authentication provider (e.g., "Jwt", "AzureAd")
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Require email confirmation before login
    /// </summary>
    public bool RequireConfirmedAccount { get; set; }
}
