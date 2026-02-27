using NightMarket.WebApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace NightMarket.WebApi.Infrastructure.Auth;

internal static class Startup
{
    /// <summary>
    /// Register CurrentUser services
    /// </summary>
    internal static IServiceCollection AddCurrentUser(this IServiceCollection services)
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
    internal static IApplicationBuilder UseCurrentUserMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<CurrentUserMiddleware>();
}
