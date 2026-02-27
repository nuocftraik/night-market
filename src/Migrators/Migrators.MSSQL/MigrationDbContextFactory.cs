using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using NightMarket.WebApi.Infrastructure.Persistence.Context;

namespace Migrators.MSSQL;

public class MigrationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Load configuration từ Host project
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../../Host/Host"))
            .AddJsonFile("Configurations/database.json", optional: false)
            .Build();

        var connectionString = configuration.GetSection("DatabaseSettings:ConnectionString").Value;

        // Create DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            x => x.MigrationsAssembly("Migrators.MSSQL"));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
