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
