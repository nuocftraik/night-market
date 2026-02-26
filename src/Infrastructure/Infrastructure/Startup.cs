using NightMarket.WebApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NightMarket.WebApi.Infrastructure;

public static class Startup
{
    /// <summary>
    /// Đăng ký tất cả Infrastructure services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        return services
            // Phase 1: Database
            .AddPersistence()

            // Phase 2: Routing
            .AddRouting(options => options.LowercaseUrls = true);

        // TODO: Các modules khác sẽ thêm sau
        // .AddAuth(config)          - BUILD_15
        // .AddCaching(config)       - BUILD_21
        // .AddMailing(config)       - BUILD_23
        // .AddBackgroundJobs(config) - BUILD_25
    }

    /// <summary>
    /// Configure middleware pipeline.
    /// </summary>
    public static IApplicationBuilder UseInfrastructure(
        this IApplicationBuilder builder,
        IConfiguration config)
    {
        return builder
            .UseRouting()
            .UseHttpsRedirection();

        // TODO: Middleware khác sẽ thêm sau
        // .UseAuthentication()
        // .UseAuthorization()
        // .UseExceptionMiddleware()
    }

    /// <summary>
    /// Map endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapEndpoints(
        this IEndpointRouteBuilder builder)
    {
        builder.MapControllers();
        return builder;
    }
}
