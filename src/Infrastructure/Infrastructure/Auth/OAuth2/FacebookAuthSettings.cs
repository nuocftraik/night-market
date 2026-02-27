namespace NightMarket.WebApi.Infrastructure.Auth.OAuth2;

/// <summary>
/// Facebook OAuth2 authentication settings
/// </summary>
public class FacebookAuthSettings
{
    public const string SectionName = "Authentication:Facebook";
    public string AppId { get; set; } = default!;
    public string AppSecret { get; set; } = default!;
}
