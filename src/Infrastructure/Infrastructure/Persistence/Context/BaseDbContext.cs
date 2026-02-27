using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Domain.Common.Contracts;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Persistence.Context;

public abstract class BaseDbContext : IdentityDbContext<
    ApplicationUser,
    ApplicationRole,
    string,
    IdentityUserClaim<string>,
    IdentityUserRole<string>,
    IdentityUserLogin<string>,
    ApplicationRoleClaim,
    IdentityUserToken<string>>
{
    private readonly ICurrentUser _currentUser;

    protected BaseDbContext(
        DbContextOptions options,
        ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations từ assembly
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Soft delete global query filter: WHERE DeletedOn IS NULL
        modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(e => e.DeletedOn == null);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleAuditingBeforeSaveChanges();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Handle auditing (Created/Modified tracking) and soft delete.
    /// </summary>
    private void HandleAuditingBeforeSaveChanges()
    {
        var userId = _currentUser.GetUserId();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModifiedBy = userId;
                    entry.Entity.LastModifiedOn = DateTime.UtcNow;
                    break;

                case EntityState.Deleted:
                    // Soft delete: convert Deleted → Modified
                    if (entry.Entity is ISoftDelete softDelete)
                    {
                        softDelete.DeletedOn = DateTime.UtcNow;
                        softDelete.DeletedBy = userId;
                        entry.State = EntityState.Modified;
                    }

                    break;
            }
        }
    }
}
