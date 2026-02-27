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
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../../Host/Host"))
            .AddJsonFile("Configurations/database.json", optional: false)
            .Build();

        var connectionString = configuration.GetSection("DatabaseSettings:ConnectionString").Value;

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            x => x.MigrationsAssembly("Migrators.MSSQL"));

        return new ApplicationDbContext(
            optionsBuilder.Options,
            new DesignTimeCurrentUser(),
            new DesignTimeSerializer());
    }
}

internal class DesignTimeCurrentUser : ICurrentUser
{
    public string? Name => null;
    public Guid GetUserId() => Guid.Empty;
    public string? GetUserEmail() => null;
    public bool IsAuthenticated() => false;
    public bool IsInRole(string role) => false;
    public IEnumerable<Claim>? GetUserClaims() => null;
}

internal class DesignTimeSerializer : ISerializerService
{
    public string Serialize<T>(T obj) => System.Text.Json.JsonSerializer.Serialize(obj);
    public string Serialize<T>(T obj, Type type) => System.Text.Json.JsonSerializer.Serialize(obj, type);
    public T Deserialize<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
}

