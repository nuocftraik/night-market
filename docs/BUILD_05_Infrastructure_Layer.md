# Xây dựng Infrastructure Layer

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 4 (Application Layer) hoàn thành

Tài liệu này hướng dẫn xây dựng Infrastructure Layer - chứa implementations của các interfaces được định nghĩa trong Application Layer.

---

## 1. Overview

**Làm gì:** Tạo Infrastructure project và setup cấu trúc cơ bản.

**Tại sao:**
- Infrastructure chứa implementations cụ thể (EF Core, Identity, External Services)
- Tách biệt technical concerns khỏi business logic
- Dễ thay đổi implementations mà không ảnh hưởng domain/application

**Trong bước này chúng ta sẽ:**
- ✅ Setup Infrastructure project với dependencies
- ✅ Tạo DbContext cơ bản
- ✅ Setup modular startup pattern
- ✅ Configure EF Core Identity

**Chưa implement:**
- ❌ Caching, Mailing, BackgroundJobs (sẽ có ở các bước sau)
- ❌ Authentication/Authorization chi tiết (sẽ có ở BUILD_13, BUILD_14)
- ❌ Common Services implementations (sẽ có ở BUILD_11)

---

## 2. Setup Infrastructure Project

### Bước 2.1: Tạo Project File

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<RootNamespace>ECO.WebApi.Infrastructure</RootNamespace>
		<AssemblyName>ECO.WebApi.Infrastructure</AssemblyName>
	</PropertyGroup>

	<!-- Project References -->
	<ItemGroup>
		<ProjectReference Include="..\..\Core\Application\Application.csproj" />
		<ProjectReference Include="..\..\Core\Domain\Domain.csproj" />
	</ItemGroup>

	<!-- EF Core Packages -->
	<ItemGroup>
		<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Ardalis.Specification.EntityFrameworkCore" Version="8.0.0" />
	</ItemGroup>

		<!-- Other Core Packages -->
		<FrameworkReference Include="Microsoft.AspNetCore.App" />
		<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
	</ItemGroup>
</Project>
```

**Giải thích packages chính:**
- `Microsoft.AspNetCore.App` - Cần thiết cho các ASP.NET Core types (như `ILoggingBuilder`) dùng trong Infrastructure.
- `EntityFrameworkCore.SqlServer` - Database provider cho SQL Server
- `EntityFrameworkCore.Tools` - Cho migrations (Add-Migration, Update-Database)

**Lưu ý:** Các packages khác (Hangfire, MailKit...) sẽ được thêm ở các bước sau khi implement từng module cụ thể.

---

## 3. Tạo Database Context

### Bước 3.1: Tạo DatabaseSettings

**Làm gì:** Tạo class để load database configuration từ appsettings.json.

**File:** `src/Infrastructure/Infrastructure/Persistence/DatabaseSettings.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace ECO.WebApi.Infrastructure.Persistence;

public class DatabaseSettings
{
    [Required]
    public string DBProvider { get; set; } = string.Empty;

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
```

**Tại sao:**
- Support multiple database providers (SQL Server, PostgreSQL, MySQL)
- Validate configuration khi startup
- Type-safe configuration

---

### Bước 3.2: Tạo BaseDbContext

**Làm gì:** Tạo base DbContext với basic Identity integration.

**⚠️ Lưu ý quan trọng:** BaseDbContext cần 3 interfaces (`ICurrentUser`, `ISerializerService`, `IEventPublisher`) nhưng chúng ta sẽ implement đầy đủ ở BUILD_11. Hiện tại tạo version đơn giản trước.

**File:** `src/Infrastructure/Infrastructure/Persistence/Context/BaseDbContext.cs`

```csharp
using ECO.WebApi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Infrastructure.Persistence.Context;

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
    protected BaseDbContext(DbContextOptions options)
      : base(options)
    {
    }

protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    
  // Apply configurations từ assembly
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
        // Enable sensitive data logging cho development
        optionsBuilder.EnableSensitiveDataLogging();
    }

    // SaveChangesAsync sẽ được override với Auditing, Domain Events ở BUILD_09
}
```

**Giải thích:**
- Version đơn giản chỉ có Identity integration
- Chưa có Auditing, Domain Events (sẽ thêm ở BUILD_09)
- Chưa inject ICurrentUser, ISerializerService (sẽ thêm ở BUILD_11)

---

### Bước 3.3: Tạo ApplicationDbContext

**Làm gì:** Tạo concrete DbContext cho application.

**File:** `src/Infrastructure/Infrastructure/Persistence/Context/ApplicationDbContext.cs`

```csharp
using ECO.WebApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Action = ECO.WebApi.Domain.Identity.Action;

namespace ECO.WebApi.Infrastructure.Persistence.Context;

public class ApplicationDbContext : BaseDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
  : base(options)
    {
 }

    // Identity tables (từ BaseDbContext/IdentityDbContext)
    // - Users
    // - Roles
    // - UserRoles
    // - UserClaims
  // - RoleClaims
    // - UserLogins
    // - UserTokens

    // Custom Identity tables
 public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Function> Functions => Set<Function>();
    public DbSet<Action> Actions => Set<Action>();
    public DbSet<ActionInFunction> ActionInFunctions => Set<ActionInFunction>();

    // Các DbSets khác sẽ được thêm khi implement features
    // TODO: Products, Categories, Orders... (sẽ thêm sau)
}
```

**Giải thích:**
- Kế thừa `BaseDbContext` để có Identity support
- Expose các DbSets cho custom entities
- Identity tables tự động được tạo bởi `IdentityDbContext`

---

### Bước 3.4: Tạo EF Core Configurations

**Làm gì:** Configure EF Core entities với Fluent API.

**File:** `src/Infrastructure/Infrastructure/Persistence/Configuration/SchemaNames.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Persistence.Configuration;

internal static class SchemaNames
{
    public const string Identity = nameof(Identity);      // Schema cho Identity tables
    public const string Catalog = nameof(Catalog);        // Schema cho Products, Categories
    public const string Ordering = nameof(Ordering);      // Schema cho Orders
    public const string Payment = nameof(Payment);    // Schema cho Payments
    public const string Auditing = nameof(Auditing);      // Schema cho Audit trails
  public const string Notification = nameof(Notification); // Schema cho Notifications
}
```

**File:** `src/Infrastructure/Infrastructure/Persistence/Configuration/Identity.cs`

```csharp
using ECO.WebApi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Action = ECO.WebApi.Domain.Identity.Action;

namespace ECO.WebApi.Infrastructure.Persistence.Configuration;

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

// Custom Identity entities configurations
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
```

**Tại sao dùng Fluent API:**
- Type-safe configuration
- Tách biệt configuration khỏi entities
- Organize theo modules (Identity, Catalog, Ordering...)

---

## 4. Setup Persistence Module

### Bước 4.1: Tạo Persistence Startup

**Làm gì:** Đăng ký DbContext và database services.

**File:** `src/Infrastructure/Infrastructure/Persistence/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace ECO.WebApi.Infrastructure.Persistence;

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

                // Configure database provider
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

            // Có thể thêm providers khác sau
// "postgresql" => builder.UseNpgsql(connectionString, ...),
   // "mysql" => builder.UseMySql(connectionString, ...),
     
         _ => throw new InvalidOperationException(
     $"Database Provider '{dbProvider}' is not supported.")
        };
    }
}
```

**Giải thích:**
- `ValidateDataAnnotations()` - Validate `[Required]` attributes khi startup
- `ValidateOnStart()` - Fail fast nếu config invalid
- `MigrationsAssembly` - Migrations nằm ở project riêng (Migrators.MSSQL)

**Tại sao tách Migrations Assembly:**
- Support multiple database providers (SQL Server, PostgreSQL, MySQL)
- Keep infrastructure clean
- Easy CI/CD deployment

---

## 5. Setup Infrastructure Startup (Modular Pattern)

### Bước 5.1: Tạo Main Infrastructure Startup

**Làm gì:** Tạo entry point để đăng ký tất cả infrastructure services.

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure;

public static class Startup
{
    /// <summary>
    /// Đăng ký tất cả Infrastructure services
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
   IConfiguration config)
    {
        return services
            // Phase 1: Database
            .AddPersistence()
        
     // Phase 2: Routing
            .AddRouting(options => options.LowercaseUrls = true);
        
        // TODO: Các modules khác sẽ thêm sau
   // .AddAuth(config)       - BUILD_14
    // .AddCaching(config)        - BUILD_16
        // .AddMailing(config)   - BUILD_17
 // .AddBackgroundJobs(config) - BUILD_19
 // .AddServices()      - BUILD_08
    }

    /// <summary>
    /// Configure middleware pipeline
    /// </summary>
    public static IApplicationBuilder UseInfrastructure(
        this IApplicationBuilder builder, 
      IConfiguration config)
    {
        return builder
      .UseRouting()
            .UseHttpsRedirection();

 // TODO: Middleware khác sẽ thêm sau
     // .UseAuthentication()
        // .UseAuthorization()
        // .UseExceptionMiddleware()
    }

 /// <summary>
    /// Map endpoints
    /// </summary>
  public static IEndpointRouteBuilder MapEndpoints(
        this IEndpointRouteBuilder builder)
    {
      builder.MapControllers();
 return builder;
  }
}
```

**Tại sao dùng Modular Startup Pattern:**
- **Separation of Concerns:** Mỗi module tự quản lý registration của mình
- **Maintainability:** Dễ tìm và sửa code
- **Scalability:** Dễ thêm/xóa modules
- **Testing:** Có thể test từng module độc lập

**Ví dụ sau này:**
```csharp
// Mỗi module có Startup.cs riêng
Auth/Startup.cs     → .AddAuth(config)
Caching/Startup.cs         → .AddCaching(config)
Mailing/Startup.cs         → .AddMailing(config)
BackgroundJobs/Startup.cs  → .AddBackgroundJobs(config)
```

---

## 6. Testing Setup

### Bước 6.1: Verify Build

```bash
# Build solution
dotnet build

# Expected: Build succeeded
```

### Bước 6.2: Verify Project References

```bash
# Check project dependencies
dotnet list src/Infrastructure/Infrastructure/Infrastructure.csproj reference

# Expected output:
# Project reference(s)
# --------------------
# ..\..\Core\Application\Application.csproj
# ..\..\Core\Domain\Domain.csproj
```

---

## 7. Common Issues

### Issue 2: "DbContext requires ICurrentUser"

**Nguyên nhân:** Đang dùng BaseDbContext version cũ (có inject ICurrentUser).

**Giải pháp:** Sử dụng simplified version ở Bước 3.2 (không inject dependencies).

---

### Issue 3: "Migration assembly not found"

**Nguyên nhân:** Chưa tạo Migrators project.

**Giải pháp:** Sẽ tạo ở BUILD_06 (Host Layer setup). Hiện tại chưa run migrations.

---

## 8. Summary

### ✅ Đã hoàn thành trong bước này:

**Infrastructure Setup:**
- ✅ Infrastructure project với dependencies
- ✅ DatabaseSettings configuration class
- ✅ BaseDbContext với Identity integration (simplified version)
- ✅ ApplicationDbContext
- ✅ EF Core entity configurations
- ✅ Persistence module startup
- ✅ Main Infrastructure startup với modular pattern

### ❌ Chưa implement (sẽ có ở bước sau):

- ❌ ICurrentUser, ISerializerService, IEventPublisher (BUILD_11)
- ❌ Authentication & Authorization (BUILD_13, BUILD_14)
- ❌ Auditing trails (BUILD_09)
- ❌ Domain events handling (BUILD_09)
- ❌ Repository pattern (BUILD_10)
- ❌ Caching (BUILD_16)
- ❌ Mailing (BUILD_17)
- ❌ Background jobs (BUILD_19)

### 📊 Project Structure hiện tại:

```
src/Infrastructure/Infrastructure/
├── Persistence/
│ ├── Context/
│   │   ├── BaseDbContext.cs       (simplified)
│   │   └── ApplicationDbContext.cs
│   ├── Configuration/
│   │   ├── SchemaNames.cs
│   │   └── Identity.cs
│   ├── DatabaseSettings.cs
│   └── Startup.cs
├── Infrastructure.csproj
└── Startup.cs
```

---

## 9. Next Steps

**Tiếp theo:** [BUILD_06 - Host Layer](BUILD_06_Host_Layer.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo Host project (ASP.NET Core Web API)
2. ✅ Setup Program.cs với configuration loading
3. ✅ Tạo Controllers structure
4. ✅ Configure Swagger
5. ✅ Setup configuration files (database.json, appsettings.json)
6. ✅ Tạo Migrators.MSSQL project
7. ✅ Run migrations lần đầu tiên
8. ✅ Verify application chạy thành công

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
