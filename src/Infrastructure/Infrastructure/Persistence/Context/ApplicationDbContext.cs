using NightMarket.WebApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Action = NightMarket.WebApi.Domain.Identity.Action;

namespace NightMarket.WebApi.Infrastructure.Persistence.Context;

public class ApplicationDbContext : BaseDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Custom Identity tables
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Function> Functions => Set<Function>();
    public DbSet<Action> Actions => Set<Action>();
    public DbSet<ActionInFunction> ActionInFunctions => Set<ActionInFunction>();

    // Các DbSets khác sẽ được thêm khi implement features
}
