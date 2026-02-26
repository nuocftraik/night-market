# Database Initialization và Migrations

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 7 (Logging Setup) hoàn thành

Tài liệu này hướng dẫn setup database initialization, migrations, và seeding data.

---

## 1. Overview

**Làm gì:** Setup auto-migration và seed initial data khi application khởi động.

**Tại sao cần:**
- **Auto-migration:** Tự động apply migrations khi deploy
- **Initial data:** Seed Actions, Functions, Roles, Admin User
- **Idempotent:** Chạy nhiều lần không gây duplicate
- **Zero-config:** Không cần chạy commands thủ công

**Trong bước này chúng ta sẽ:**
- ✅ Setup Migrators.MSSQL project
- ✅ Tạo MigrationDbContextFactory (Design-time)
- ✅ Tạo Custom Seeder Pattern
- ✅ Tạo ApplicationDbSeeder
- ✅ Tạo DatabaseInitializer
- ✅ Run first migration

---

## 2. Setup Migrators Project

### Bước 2.1: Tạo Migrators Project

**File:** `src/Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<RootNamespace>ECO.WebApi.Migrators.MSSQL</RootNamespace>
	</PropertyGroup>

	<!-- Reference Infrastructure (chứa DbContext) -->
	<ItemGroup>
		<ProjectReference Include="..\..\Infrastructure\Infrastructure\Infrastructure.csproj" />
	</ItemGroup>

	<!-- EF Core Packages -->
	<ItemGroup>
		<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
		</PackageReference>
	</ItemGroup>
</Project>
```

---

### Bước 2.2: Design-Time DbContext Factory

**File:** `src/Migrators/Migrators.MSSQL/MigrationDbContextFactory.cs`

```csharp
using ECO.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECO.WebApi.Migrators.MSSQL;

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
```

**Giải thích:**
- EF Core tìm `IDesignTimeDbContextFactory` khi chạy migrations
- Load connection string từ Host/database.json
- Chỉ định migrations assembly là `Migrators.MSSQL`

---

### Bước 2.3: Update Host Project

**File:** `src/Host/Host/Host.csproj`

```xml
<!-- Add Migrators reference -->
<ItemGroup>
    <ProjectReference Include="..\..\Migrators\Migrators.MSSQL\Migrators.MSSQL.csproj" />
</ItemGroup>

<!-- Add EF Core Design tools -->
<ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
</ItemGroup>
```

---

## 3. Tạo Custom Seeder Pattern

### Bước 3.1: ICustomSeeder Interface

**File:** `src/Infrastructure/Infrastructure/Persistence/Initialization/ICustomSeeder.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Persistence.Initialization;

public interface ICustomSeeder
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
```

**Mục đích:** Cho phép các module khác tự seed data của mình (optional).

---

### Bước 3.2: CustomSeederRunner

**File:** `src/Infrastructure/Infrastructure/Persistence/Initialization/CustomSeederRunner.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Persistence.Initialization;

internal class CustomSeederRunner
{
    private readonly ICustomSeeder[] _seeders;

    public CustomSeederRunner(IServiceProvider serviceProvider) =>
      _seeders = serviceProvider.GetServices<ICustomSeeder>().ToArray();

    public async Task RunSeedersAsync(CancellationToken cancellationToken)
    {
        foreach (var seeder in _seeders)
    {
            await seeder.InitializeAsync(cancellationToken);
    }
    }
}
```

**Giải thích:**
- Tự động tìm tất cả implementations của `ICustomSeeder`
- Chạy tuần tự từng seeder
- Custom seeders sẽ được auto-register trong DI

---

## 4. Tạo ApplicationDbSeeder

### Bước 4.1: ApplicationDbSeeder Implementation

**File:** `src/Infrastructure/Infrastructure/Persistence/Initialization/ApplicationDbSeeder.cs`

```csharp
using System.Reflection;
using ECO.WebApi.Domain.Identity;
using ECO.WebApi.Infrastructure.Persistence.Context;
using ECO.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Infrastructure.Persistence.Initialization;

internal class ApplicationDbSeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CustomSeederRunner _seederRunner;
    private readonly ILogger<ApplicationDbSeeder> _logger;

    public ApplicationDbSeeder(
  RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        CustomSeederRunner seederRunner,
        ILogger<ApplicationDbSeeder> logger)
  {
  _roleManager = roleManager;
        _userManager = userManager;
      _seederRunner = seederRunner;
        _logger = logger;
    }

    public async Task SeedDatabaseAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Seed theo thứ tự phụ thuộc
        await SeedActionsAndFunctionsAsync(dbContext);
        await SeedRolesAsync(dbContext);
        await SeedAdminUserAsync();
        await _seederRunner.RunSeedersAsync(cancellationToken);
    }

    private async Task SeedActionsAndFunctionsAsync(ApplicationDbContext dbContext)
    {
 // 1. Seed Actions
      var actions = typeof(ECOAction)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
.Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
            .ToList();

        foreach (var action in actions)
    {
     if (!await dbContext.Actions.AnyAsync(x => x.Name == action))
            {
           dbContext.Actions.Add(new Domain.Identity.Action { Name = action });
        }
        }
        await dbContext.SaveChangesAsync();

        // 2. Seed Functions
        var functions = typeof(ECOFunction)
  .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
 .Where(f => f.IsLiteral && !f.IsInitOnly)
 .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
       .ToList();

        foreach (var functionName in functions)
     {
            if (!await dbContext.Functions.AnyAsync(f => f.Name == functionName))
    {
     dbContext.Functions.Add(new Function { Name = functionName });
            }
        }
        await dbContext.SaveChangesAsync();

        // 3. Link Actions with Functions
  foreach (var functionName in functions)
        {
    var function = await dbContext.Functions.FirstAsync(f => f.Name == functionName);
    foreach (var actionName in actions)
      {
        var action = await dbContext.Actions.FirstAsync(a => a.Name == actionName);
 if (!await dbContext.ActionInFunctions.AnyAsync(
        aif => aif.FunctionId == function.Id && aif.ActionId == action.Id))
         {
   dbContext.ActionInFunctions.Add(new ActionInFunction(action.Id, function.Id));
     }
            }
      }
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedRolesAsync(ApplicationDbContext dbContext)
    {
        foreach (string roleName in ECORoles.DefaultRoles)
        {
            // Tạo role nếu chưa có
            if (await _roleManager.FindByNameAsync(roleName) is not ApplicationRole role)
            {
     _logger.LogInformation("Seeding {role} Role.", roleName);
        role = new ApplicationRole(roleName, $"{roleName} Role");
        await _roleManager.CreateAsync(role);
            }

         // Assign permissions
        if (roleName == ECORoles.Admin)
     {
    await AssignAllPermissionsAsync(dbContext, role);
            }
      else if (roleName == ECORoles.Basic)
            {
   await AssignBasicPermissionsAsync(dbContext, role);
   }
        }
    }

    private async Task AssignAllPermissionsAsync(ApplicationDbContext dbContext, ApplicationRole role)
    {
        var functions = await dbContext.Functions.ToListAsync();
  foreach (var function in functions)
        {
         var actions = await dbContext.ActionInFunctions
          .Where(aif => aif.FunctionId == function.Id)
    .ToListAsync();

         foreach (var actionInFunction in actions)
     {
              if (!await dbContext.Permissions.AnyAsync(p =>
      p.RoleId == role.Id &&
     p.FunctionId == function.Id &&
         p.ActionId == actionInFunction.ActionId))
  {
   dbContext.Permissions.Add(new Permission(role.Id, function.Id, actionInFunction.ActionId));
             }
        }
  }
     await dbContext.SaveChangesAsync();
    }

    private async Task AssignBasicPermissionsAsync(ApplicationDbContext dbContext, ApplicationRole role)
    {
  // Basic role chỉ có View và Search permissions
  var basicActions = new[] { ECOAction.View, ECOAction.Search };
  var functions = await dbContext.Functions.ToListAsync();

        foreach (var function in functions)
        {
            var actions = await dbContext.ActionInFunctions
      .Include(x => x.Action)
       .Where(aif => aif.FunctionId == function.Id && basicActions.Contains(aif.Action.Name))
    .ToListAsync();

            foreach (var actionInFunction in actions)
     {
       if (!await dbContext.Permissions.AnyAsync(p =>
  p.RoleId == role.Id &&
        p.FunctionId == function.Id &&
           p.ActionId == actionInFunction.ActionId))
         {
          dbContext.Permissions.Add(new Permission(role.Id, function.Id, actionInFunction.ActionId));
  }
            }
        }
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedAdminUserAsync()
    {
        const string adminEmail = "admin@gmail.com";
        const string adminPassword = "Abcd@1234";

        if (await _userManager.FindByEmailAsync(adminEmail) is not ApplicationUser adminUser)
        {
      _logger.LogInformation("Seeding default admin user.");

            adminUser = new ApplicationUser
         {
                FirstName = "System",
                LastName = "Admin",
    Email = adminEmail,
 UserName = "system.admin",
       EmailConfirmed = true,
          PhoneNumberConfirmed = true,
  IsActive = true
        };

       await _userManager.CreateAsync(adminUser, adminPassword);
        }

        // Assign Admin role
      if (!await _userManager.IsInRoleAsync(adminUser, ECORoles.Admin))
      {
            await _userManager.AddToRoleAsync(adminUser, ECORoles.Admin);
        }
    }
}
```

**Seeding flow:**
```
Actions & Functions (từ constants)
    ↓
Roles (Admin, Basic)
    ↓
Admin User (system.admin)
    ↓
Custom Seeders (optional)
```

---

## 5. Tạo DatabaseInitializer

### Bước 5.1: DatabaseInitializer Implementation

**File:** `src/Infrastructure/Infrastructure/Persistence/Initialization/DatabaseInitializer.cs`

```csharp
using ECO.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Infrastructure.Persistence.Initialization;

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
```

**Flow:**
```
Check migrations → Apply pending → Seed data
```

**Dependencies:**
- `ApplicationDbContext` - Database context
- `ApplicationDbSeeder` - Seeding logic (đã tạo ở bước 4)
- `ILogger` - Logging

---

## 6. Register Services

### Bước 6.1: Update Persistence Startup

**File:** `src/Infrastructure/Infrastructure/Persistence/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Persistence.Initialization;
// ... existing usings ...

internal static IServiceCollection AddPersistence(this IServiceCollection services)
{
  // ... existing code (DbContext setup) ...

    // Database initialization
    services.AddScoped<CustomSeederRunner>();
    services.AddScoped<ApplicationDbSeeder>();
    services.AddScoped<DatabaseInitializer>();
    
    // Auto-register all custom seeders
    services.AddServices(typeof(ICustomSeeder), ServiceLifetime.Scoped);

    return services;
}
```

**Thứ tự registration:**
1. `CustomSeederRunner` - Chạy custom seeders
2. `ApplicationDbSeeder` - Seeding chính (depends on CustomSeederRunner)
3. `DatabaseInitializer` - Orchestrator (depends on ApplicationDbSeeder)

---

### Bước 6.2: Add Extension Method

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Persistence.Initialization;
// ... existing usings ...

public static class Startup
{
    // ... existing methods ...

    /// <summary>
    /// Initialize databases (apply migrations + seed data)
    /// </summary>
    public static async Task InitializeDatabasesAsync(
    this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
  await initializer.InitializeAsync(cancellationToken);
    }
}
```

---

## 7. Create First Migration

### Bước 7.1: Run Migration Command

```bash
# Navigate to Host directory
cd src/Host/Host/

# Create initial migration
dotnet ef migrations add InitialCreate \
--project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj \
    --context ApplicationDbContext

# Output: Migration files created in Migrators.MSSQL/Migrations/
```

---

### Bước 7.2: Verify Migration Files

```
src/Migrators/Migrators.MSSQL/Migrations/
├── 20240101120000_InitialCreate.cs
├── 20240101120000_InitialCreate.Designer.cs
└── ApplicationDbContextModelSnapshot.cs
```

---

## 8. Update Program.cs

### Bước 8.1: Call InitializeDatabasesAsync

**File:** `src/Host/Host/Program.cs`

```csharp
using ECO.WebApi.Application;
using ECO.WebApi.Host.Configurations;
using ECO.WebApi.Infrastructure;
using ECO.WebApi.Infrastructure.Logging;
using Serilog;

// Initialize logging
StaticLogger.EnsureInitialized();
Log.Information("Server Booting Up...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Load configurations
    builder.AddConfigurations();
    builder.RegisterSerilog();

    // Add services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add Swagger
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
  Title = "ECO.WebApi",
       Version = "v1",
Description = "E-Commerce API built with Clean Architecture"
   });
    });

    // Build
    var app = builder.Build();

    Log.Information("Application built successfully");

    // Configure middleware
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
    app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    // ⭐ Initialize database (apply migrations + seed data)
    await app.Services.InitializeDatabasesAsync();

    // Use Infrastructure middleware
    app.UseInfrastructure(builder.Configuration);
    app.MapEndpoints();

    // Run
 Log.Information("Application Starting...");
    Log.Information("Listening on: {Addresses}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    StaticLogger.EnsureInitialized();
    Log.Fatal(ex, "Unhandled exception occurred during application startup");
}
finally
{
    StaticLogger.EnsureInitialized();
    Log.Information("Server Shutting down...");
    Log.CloseAndFlush();
}
```

**Thứ tự quan trọng:**
```
Build app
    ↓
Configure middleware (Swagger, Logging)
    ↓
Initialize database ⭐
    ↓
Use Infrastructure middleware
    ↓
Run app
```

---

## 9. Run Application

### Bước 9.1: First Run

```bash
dotnet run --project src/Host/Host/Host.csproj
```

**Expected logs:**
```
[12:00:00 INF] Server Booting Up...
[12:00:01 INF] Application built successfully
[12:00:01 INF] Applying 1 pending migrations...
[12:00:02 INF] Migrations applied successfully.
[12:00:02 INF] Seeding Admin Role.
[12:00:02 INF] Seeding Basic Role.
[12:00:02 INF] Seeding default admin user.
[12:00:02 INF] Application Starting...
[12:00:02 INF] Listening on: https://localhost:7001
```

---

### Bước 9.2: Verify Database

```sql
USE ECODb;

-- Check tables
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME;

-- Check seeded data
SELECT * FROM Identity.Actions;
SELECT * FROM Identity.Functions;
SELECT * FROM Identity.Roles;
SELECT * FROM Identity.Users;
SELECT * FROM Identity.Permissions;
```

**Expected:**
- 8 Actions (View, Search, Create, Update, Delete, Export, Generate, Clean)
- Multiple Functions (Dashboard, Users, Roles, Products, Categories, Orders, etc.)
- 2 Roles (Admin, Basic)
- 1 User (system.admin)
- Multiple Permissions (Admin có tất cả, Basic chỉ View/Search)

---

## 10. Testing

### Bước 10.1: Test Health Endpoint

```bash
curl https://localhost:7001/api/health
```

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-01T12:00:00.000Z",
  "environment": "Development"
}
```

---

### Bước 10.2: Test Swagger

**URL:** `https://localhost:7001/swagger`

**Expected:** Swagger UI với Health endpoint

---

## 11. Common Issues

### Issue 1: "No migrations found"

**Nguyên nhân:** Chưa chạy `dotnet ef migrations add`

**Giải pháp:**
```bash
cd src/Host/Host/
dotnet ef migrations add InitialCreate --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj
```

---

### Issue 2: "Unable to create DbContext"

**Nguyên nhân:** `MigrationDbContextFactory` không tìm thấy database.json

**Giải pháp:**
- Verify `database.json` exists trong `Host/Configurations/`
- Check path trong `MigrationDbContextFactory.cs`

---

### Issue 3: "Duplicate key error when seeding"

**Nguyên nhân:** Seeder chạy nhiều lần

**Giải pháp:** Code đã có `AnyAsync()` checks → idempotent. Nếu vẫn lỗi, check database constraints.

---

## 12. Summary

### ✅ Đã hoàn thành trong bước này:

**Migrators Setup:**
- ✅ Migrators.MSSQL project
- ✅ MigrationDbContextFactory (design-time)
- ✅ First migration created

**Seeding Infrastructure:**
- ✅ ICustomSeeder interface (optional pattern)
- ✅ CustomSeederRunner (chạy custom seeders)
- ✅ ApplicationDbSeeder (seeding chính)

**Database Initialization:**
- ✅ DatabaseInitializer (orchestrator)
- ✅ Auto-migration on startup

**Data Seeded:**
- ✅ Actions & Functions (từ constants)
- ✅ Roles (Admin, Basic)
- ✅ Admin User (system.admin / Abcd@1234)
- ✅ Permissions (Admin full access, Basic read-only)

**Program.cs:**
- ✅ Call `InitializeDatabasesAsync()` on startup

### 📊 Data Seeded:

```
Actions: View, Search, Create, Update, Delete, Export, Generate, Clean
Functions: Dashboard, Users, Roles, Products, Categories, Orders, ...
Roles: Admin (full access), Basic (read-only)
Admin User: system.admin / Abcd@1234
```

### 📝 Notes:

- Thông tin đăng nhập Admin mặc định: `system.admin / Abcd@1234`
- Đường dẫn file cấu hình: `Host/Configurations/database.json`
- Các migration file được tạo ra trong thư mục: `Migrators.MSSQL/Migrations/`

---

## 13. Next Steps

**Tiếp theo:** [BUILD_09 - Service Registration Pattern](BUILD_09_Service_Registration.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo service registration pattern
2. ✅ Auto-scan và register services
3. ✅ Lifetime management (Transient, Scoped, Singleton)
4. ✅ Service decorators
5. ✅ Modular service registration

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
