using NightMarket.WebApi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Action = NightMarket.WebApi.Domain.Identity.Action;

namespace NightMarket.WebApi.Infrastructure.Persistence.Configuration;

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", SchemaNames.Identity);

        builder.Property(u => u.ObjectId)
            .HasMaxLength(256);
    }
}

public class ApplicationRoleConfig : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles", SchemaNames.Identity);
    }
}

public class ApplicationRoleClaimConfig : IEntityTypeConfiguration<ApplicationRoleClaim>
{
    public void Configure(EntityTypeBuilder<ApplicationRoleClaim> builder)
    {
        builder.ToTable("RoleClaims", SchemaNames.Identity);
    }
}

public class IdentityUserRoleConfig : IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> builder)
    {
        builder.ToTable("UserRoles", SchemaNames.Identity);
    }
}

public class IdentityUserClaimConfig : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> builder)
    {
        builder.ToTable("UserClaims", SchemaNames.Identity);
    }
}

public class IdentityUserLoginConfig : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder)
    {
        builder.ToTable("UserLogins", SchemaNames.Identity);
    }
}

public class IdentityUserTokenConfig : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> builder)
    {
        builder.ToTable("UserTokens", SchemaNames.Identity);
    }
}

public class ActionConfiguration : IEntityTypeConfiguration<Action>
{
    public void Configure(EntityTypeBuilder<Action> builder)
    {
        builder.ToTable("Actions", SchemaNames.Identity);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

public class FunctionConfiguration : IEntityTypeConfiguration<Function>
{
    public void Configure(EntityTypeBuilder<Function> builder)
    {
        builder.ToTable("Functions", SchemaNames.Identity);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

public class ActionInFunctionConfiguration : IEntityTypeConfiguration<ActionInFunction>
{
    public void Configure(EntityTypeBuilder<ActionInFunction> builder)
    {
        builder.ToTable("ActionInFunctions", SchemaNames.Identity);

        // Composite key
        builder.HasKey(x => new { x.ActionId, x.FunctionId });
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", SchemaNames.Identity);

        // Composite key
        builder.HasKey(x => new { x.RoleId, x.FunctionId, x.ActionId });
    }
}
