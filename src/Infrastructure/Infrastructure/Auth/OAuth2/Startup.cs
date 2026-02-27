using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NightMarket.WebApi.Infrastructure.Auth.OAuth2;

internal static class Startup
{
    /// <summary>
    /// Add OAuth2 authentication providers (Google, Facebook)
    /// </summary>
    internal static IServiceCollection AddOAuth2Authentication(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register settings from configuration
        services.Configure<GoogleAuthSettings>(
            configuration.GetSection(GoogleAuthSettings.SectionName));

        services.Configure<FacebookAuthSettings>(
            configuration.GetSection(FacebookAuthSettings.SectionName));

        return services;
    }
}
