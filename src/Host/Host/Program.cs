using NightMarket.WebApi.Application;
using NightMarket.WebApi.Host.Configurations;
using NightMarket.WebApi.Infrastructure;
using NightMarket.WebApi.Infrastructure.Logging;
using Serilog;

// 1. Initialize static logger (bootstrap)
StaticLogger.EnsureInitialized();
Log.Information("Server Booting Up...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 2. Load configurations (includes logger.json)
    builder.AddConfigurations();

    // 3. Register Serilog (full configuration)
    builder.RegisterSerilog();

    // 4. Add services to DI container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // 5. Add layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // 6. Add Swagger
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "NightMarket.WebApi",
            Version = "v1",
            Description = "Night Market E-Commerce API built with Clean Architecture"
        });
    });

    // 7. Build application
    var app = builder.Build();

    // 8. Log application built
    Log.Information("Application built successfully");

    // 9. Configure middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 10. Request logging middleware
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // ⭐ Initialize database (apply migrations + seed data)
    // TEMPORARY: Commenting out until Identity services are added (Phase 4)
    // Otherwise EF Core design-time tools fail to build the service provider.
    // await app.Services.InitializeDatabasesAsync();

    // 11. Use Infrastructure middleware
    app.UseInfrastructure(builder.Configuration);

    // 12. Map endpoints
    app.MapEndpoints();

    // 13. Run application
    Log.Information("Application Starting...");
    app.Run();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    StaticLogger.EnsureInitialized();
    Log.Fatal(ex, "Unhandled exception occurred during application startup");
}
finally
{
    StaticLogger.EnsureInitialized();
    Log.Information("Server Shutting down...");
    Log.CloseAndFlush();
}
