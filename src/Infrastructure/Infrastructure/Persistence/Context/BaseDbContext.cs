using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Domain.Auditing;
using NightMarket.WebApi.Domain.Common.Contracts;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Auditing;
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
    private readonly ISerializerService _serializer;

    protected BaseDbContext(
        DbContextOptions options,
        ICurrentUser currentUser,
        ISerializerService serializer)
        : base(options)
    {
        _currentUser = currentUser;
        _serializer = serializer;
    }

    // Audit trails table
    public DbSet<Trail> AuditTrails => Set<Trail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Soft delete global query filter
        modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(e => e.DeletedOn == null);

        // Configure Trail entity
        modelBuilder.Entity<Trail>(entity =>
        {
            entity.ToTable("AuditTrails", "Auditing");
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TableName);
            entity.HasIndex(e => e.DateTime);
            entity.HasIndex(e => e.Type);
            entity.Property(e => e.TableName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AffectedColumns).HasColumnType("nvarchar(max)");
            entity.Property(e => e.PrimaryKey).HasMaxLength(500).IsRequired();
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: Capture audit trails BEFORE SaveChanges
        var auditEntries = CaptureAuditTrails();

        // Step 2: Handle auditing (Created/Modified) and soft delete
        HandleAuditingBeforeSaveChanges();

        // Step 3: Save changes
        var result = await base.SaveChangesAsync(cancellationToken);

        // Step 4: Save audit trails (after successful save)
        if (auditEntries.Count > 0)
        {
            await AuditTrails.AddRangeAsync(auditEntries, cancellationToken);
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private List<Trail> CaptureAuditTrails()
    {
        var userId = _currentUser.GetUserId();
        var auditEntries = new List<Trail>();

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added ||
                        e.State == EntityState.Modified ||
                        e.State == EntityState.Deleted)
            .Where(e => e.Entity is not Trail) // Don't audit audit trails
            .ToList())
        {
            var trail = AuditTrail.TransformEntry(entry, userId, _serializer);
            if (trail != null)
                auditEntries.Add(trail);
        }

        return auditEntries;
    }

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
