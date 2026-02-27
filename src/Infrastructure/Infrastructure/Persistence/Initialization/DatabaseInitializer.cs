using NightMarket.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NightMarket.WebApi.Infrastructure.Persistence.Initialization;

internal class DatabaseInitializer
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationDbSeeder _dbSeeder;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ApplicationDbContext dbContext,
        ApplicationDbSeeder dbSeeder,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _dbSeeder = dbSeeder;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 1. Check migrations exist
        if (!_dbContext.Database.GetMigrations().Any())
        {
            _logger.LogWarning("No migrations found. Skipping database initialization.");
            return;
        }

        // 2. Apply pending migrations
        var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            _logger.LogInformation("Applying {count} pending migrations...", pendingMigrations.Count());
            await _dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Migrations applied successfully.");
        }

        // 3. Seed data
        if (await _dbContext.Database.CanConnectAsync(cancellationToken))
        {
            await _dbSeeder.SeedDatabaseAsync(_dbContext, cancellationToken);
        }
    }
}
