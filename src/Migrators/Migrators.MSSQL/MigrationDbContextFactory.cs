using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using System.Security.Claims;

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

        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }
}

/// <summary>
/// Stub ICurrentUser for design-time migrations (no real user context available).
/// </summary>
internal class DesignTimeCurrentUser : ICurrentUser
{
    public string? Name => null;
    public Guid GetUserId() => Guid.Empty;
    public string? GetUserEmail() => null;
    public bool IsAuthenticated() => false;
    public bool IsInRole(string role) => false;
    public IEnumerable<Claim>? GetUserClaims() => null;
}
