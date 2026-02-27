using NightMarket.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace NightMarket.WebApi.Infrastructure.Persistence;

internal static class Startup
{
    private static readonly ILogger _logger = Log.ForContext(typeof(Startup));

    internal static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // Database initialization
        services.AddScoped<NightMarket.WebApi.Infrastructure.Persistence.Initialization.CustomSeederRunner>();
        // TEMPORARY: Commenting out seeders because Identity services are missing until Phase 4.
        // services.AddScoped<NightMarket.WebApi.Infrastructure.Persistence.Initialization.ApplicationDbSeeder>();
        // services.AddScoped<NightMarket.WebApi.Infrastructure.Persistence.Initialization.DatabaseInitializer>();
        
        // Auto-register all custom seeders
        // Let's safe-scan the assemblies and register implementations, not the interface itself as an implementation.
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var seederType = typeof(NightMarket.WebApi.Infrastructure.Persistence.Initialization.ICustomSeeder);
        var seeders = assemblies.SelectMany(a => a.GetTypes())
                                .Where(t => seederType.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
        foreach (var type in seeders)
        {
            services.AddScoped(seederType, type);
        }

        // Register DatabaseSettings
        services.AddOptions<DatabaseSettings>()
            .BindConfiguration(nameof(DatabaseSettings))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register DbContext
        return services
            .AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                var databaseSettings = serviceProvider
                    .GetRequiredService<IOptions<DatabaseSettings>>().Value;

                _logger.Information("Current DB Provider: {dbProvider}",
                    databaseSettings.DBProvider);

                options.UseDatabase(
                    databaseSettings.DBProvider,
                    databaseSettings.ConnectionString);
            });
    }

    internal static DbContextOptionsBuilder UseDatabase(
        this DbContextOptionsBuilder builder,
        string dbProvider,
        string connectionString)
    {
        return dbProvider.ToLowerInvariant() switch
        {
            "mssql" => builder.UseSqlServer(
                connectionString,
                e => e.MigrationsAssembly("Migrators.MSSQL")),

            _ => throw new InvalidOperationException(
                $"Database Provider '{dbProvider}' is not supported.")
        };
    }
}
