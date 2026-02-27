using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Infrastructure.Auth.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace NightMarket.WebApi.Infrastructure.Auth;

/// <summary>
/// Auth module startup configuration
/// </summary>
internal static class Startup
{
    internal static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services
            .AddCurrentUser()
            // JWT Authentication
            .AddJwtAuth();
            
        return services;
    }

    internal static IApplicationBuilder UseAuth(this IApplicationBuilder app)
    {
        return app
            .UseCurrentUserMiddleware()
            .UseAuthentication()
            .UseAuthorization();
    }

    /// <summary>
    /// Register CurrentUser services
    /// </summary>
    private static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        // Register middleware as Scoped (per request)
        services.AddScoped<CurrentUserMiddleware>();
        
        // Register CurrentUser as Scoped - mỗi request một instance
        // Cả 2 interfaces đều resolve về cùng instance
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentUserInitializer, CurrentUser>();

        return services;
    }

    /// <summary>
    /// Use CurrentUser middleware
    /// </summary>
    private static IApplicationBuilder UseCurrentUserMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<CurrentUserMiddleware>();
}
