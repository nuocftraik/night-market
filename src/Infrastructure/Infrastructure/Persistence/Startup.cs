using NightMarket.WebApi.Application.Common.Persistence;
using NightMarket.WebApi.Domain.Common.Contracts;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using NightMarket.WebApi.Infrastructure.Persistence.Repository;
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
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var databaseSettings = serviceProvider
                .GetRequiredService<IOptions<DatabaseSettings>>().Value;

            _logger.Information("Current DB Provider: {dbProvider}",
                databaseSettings.DBProvider);

            options.UseDatabase(
                databaseSettings.DBProvider,
                databaseSettings.ConnectionString);
        });

        return services.AddRepositories();
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Register base repositories
        services.AddScoped(typeof(IRepository<>), typeof(ApplicationDbRepository<>));
        
        // Auto-discover all Aggregate Roots và register repositories
        foreach (var aggregateRootType in
            typeof(IAggregateRoot).Assembly.GetExportedTypes()
                .Where(t => typeof(IAggregateRoot).IsAssignableFrom(t) && t.IsClass)
                .ToList())
        {
            // IReadRepository<T> → alias của IRepository<T>
            services.AddScoped(
                typeof(IReadRepository<>).MakeGenericType(aggregateRootType),
                sp => sp.GetRequiredService(typeof(IRepository<>).MakeGenericType(aggregateRootType)));

            // IRepositoryWithEvents<T> → EventAddingRepositoryDecorator wrapping IRepository
            services.AddScoped(
                typeof(IRepositoryWithEvents<>).MakeGenericType(aggregateRootType),
                sp => Activator.CreateInstance(
                    typeof(EventAddingRepositoryDecorator<>).MakeGenericType(aggregateRootType),
                    sp.GetRequiredService(typeof(IRepository<>).MakeGenericType(aggregateRootType)))
                ?? throw new InvalidOperationException($"Could not create EventAddingRepositoryDecorator for {aggregateRootType.Name}"));
        }

        return services;
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
