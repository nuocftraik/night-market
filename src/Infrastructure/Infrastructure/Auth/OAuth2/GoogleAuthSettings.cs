namespace NightMarket.WebApi.Infrastructure.Auth.OAuth2;

/// <summary>
/// Google OAuth2 authentication settings
/// </summary>
public class GoogleAuthSettings
{
    public const string SectionName = "Authentication:Google";
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
}
