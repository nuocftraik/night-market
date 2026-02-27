using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Serilog;

namespace NightMarket.WebApi.Infrastructure.Logging;

public static class Extensions
{
    public static WebApplicationBuilder RegisterSerilog(this WebApplicationBuilder builder)
    {
        // Remove default logging providers
        builder.Logging.ClearProviders();

        // Register Serilog
        builder.Services.AddSerilog((services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "NightMarket.WebApi")
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);
        });

        return builder;
    }
}
