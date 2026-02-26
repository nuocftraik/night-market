# Setup Logging với Serilog

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 6 (Host Layer) hoàn thành

Tài liệu này hướng dẫn setup Serilog - structured logging framework cho .NET application.

---

## 1. Overview

**Làm gì:** Setup Serilog để log application events, errors, và diagnostics.

**Tại sao cần Logging:**
- **Troubleshooting:** Debug issues trong production
- **Monitoring:** Track application health và performance
- **Auditing:** Record user actions và system events
- **Compliance:** Meet regulatory requirements

**Serilog vs ILogger:**
- **Structured logging:** Log data as structured objects, không chỉ strings
- **Multiple sinks:** Write logs đến nhiều destinations (Console, File, Seq, Elasticsearch...)
- **Enrichers:** Tự động thêm context (Thread, Process, Environment...)
- **Performance:** High-performance logging với async writes

**Trong bước này chúng ta sẽ:**
- ✅ Setup Serilog packages
- ✅ Tạo StaticLogger cho bootstrap logging
- ✅ Configure Serilog với appsettings
- ✅ Setup multiple sinks (Console, File)
- ✅ Add enrichers (Context, Thread, Process)
- ✅ Configure log levels

---

## 2. Add Serilog Packages

### Bước 2.1: Infrastructure Packages

**Update file:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<!-- Serilog Core -->
<ItemGroup>
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="5.0.1" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="3.4.0" />
    
    <!-- Serilog Sinks -->
    <PackageReference Include="Serilog.Sinks.Console" Version="4.1.0" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    <PackageReference Include="Serilog.Sinks.Async" Version="1.5.0" />
    
    <!-- Serilog Enrichers -->
    <PackageReference Include="Serilog.Enrichers.Environment" Version="2.2.0" />
    <PackageReference Include="Serilog.Enrichers.Process" Version="2.0.2" />
    <PackageReference Include="Serilog.Enrichers.Thread" Version="3.1.0" />
    <PackageReference Include="Serilog.Exceptions" Version="8.4.0" />
</ItemGroup>
```

### Bước 2.2: Host Packages

**Update file:** `src/Host/Host/Host.csproj`

```xml
<ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="6.1.0" />
</ItemGroup>
```

**Giải thích packages:**
- **Serilog** - Core library
- **Serilog.Extensions.Hosting** - Integration với .NET Generic Host
- **Serilog.Settings.Configuration** - Load config từ appsettings.json
- **Serilog.Sinks.Console** - Write logs to console
- **Serilog.Sinks.File** - Write logs to file
- **Serilog.Sinks.Async** - Async logging để không block application
- **Enrichers** - Tự động add context data vào logs

---

## 3. Tạo StaticLogger

### Bước 3.1: StaticLogger Implementation

**Làm gì:** Tạo logger tĩnh để log errors trước khi application fully configured.

**File:** `src/Infrastructure/Infrastructure/Logging/StaticLogger.cs`

```csharp
using Serilog;
using Serilog.Events;

namespace ECO.WebApi.Infrastructure.Logging;

public static class StaticLogger
{
    public static void EnsureInitialized()
    {
      if (Log.Logger is not Serilog.Core.Logger)
        {
         Log.Logger = new LoggerConfiguration()
          .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
                .CreateBootstrapLogger();
        }
    }
}
```

**Tại sao cần StaticLogger:**
- **Early logging:** Log errors ngay từ Program.cs startup
- **Bootstrap logging:** Trước khi full Serilog configuration được load
- **Fail-safe:** Đảm bảo có logger ngay cả khi configuration fails

**`CreateBootstrapLogger()` vs `CreateLogger()`:**
- `CreateBootstrapLogger()` - Minimal logger cho bootstrap phase
- `CreateLogger()` - Full logger với complete configuration

---

## 4. Configure Serilog

### Bước 4.1: Serilog Configuration File

**File:** `src/Host/Host/Configurations/logger.json`

```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.File",
      "Serilog.Sinks.Async"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
 "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
   "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
     "Args": {
        "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
}
      },
      {
  "Name": "Async",
        "Args": {
          "configure": [
   {
        "Name": "File",
         "Args": {
           "path": "Logs/log-.txt",
        "rollingInterval": "Day",
       "rollOnFileSizeLimit": true,
  "fileSizeLimitBytes": 10485760,
 "retainedFileCountLimit": 7,
           "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
         }
          }
     ]
        }
      }
 ],
 "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithThreadId",
      "WithExceptionDetails"
    ],
    "Properties": {
      "Application": "ECO.WebApi"
    }
  }
}
```

**Giải thích cấu hình:**

**MinimumLevel:**
- `Default: Information` - Log level mặc định
- `Override` - Override cho specific namespaces (giảm noise từ Microsoft logs)

**WriteTo:**
- **Console:** Hiển thị logs trong console (development)
- **File (Async):** Write logs to file với rolling (production)
  - `rollingInterval: Day` - Mỗi ngày một file mới
  - `fileSizeLimitBytes: 10MB` - Max file size
  - `retainedFileCountLimit: 7` - Keep 7 ngày logs

**Enrich:**
- `FromLogContext` - Add contextual properties
- `WithMachineName` - Add machine name
- `WithThreadId` - Add thread ID
- `WithExceptionDetails` - Add exception details

---

### Bước 4.2: Load Logger Configuration

**Update file:** `src/Host/Host/Configurations/Startup.cs`

```csharp
namespace ECO.WebApi.Host.Configurations;

internal static class Startup
{
    internal static WebApplicationBuilder AddConfigurations(
        this WebApplicationBuilder builder)
    {
        const string configurationsDirectory = "Configurations";
        var env = builder.Environment;
   
        builder.Configuration
     // Base configurations
     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
         .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
       
            // Module configurations
      .AddJsonFile($"{configurationsDirectory}/database.json", optional: false, reloadOnChange: true)
.AddJsonFile($"{configurationsDirectory}/database.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
         
      // ✅ Thêm logger configuration
      .AddJsonFile($"{configurationsDirectory}/logger.json", optional: false, reloadOnChange: true)
         .AddJsonFile($"{configurationsDirectory}/logger.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
    
   // Environment variables (override JSON)
            .AddEnvironmentVariables();
      
 return builder;
    }
}
```

---

### Bước 4.3: Register Serilog Extension

**File:** `src/Infrastructure/Infrastructure/Logging/Extensions.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Serilog;

namespace ECO.WebApi.Infrastructure.Logging;

public static class Extensions
{
    public static WebApplicationBuilder RegisterSerilog(this WebApplicationBuilder builder)
    {
        // Remove default logging providers
        builder.Logging.ClearProviders();
        
     // Register Serilog
    builder.Host.UseSerilog((context, services, loggerConfig) =>
        {
    loggerConfig
    .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
    .Enrich.FromLogContext()
  .Enrich.WithProperty("Application", "ECO.WebApi")
.Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);
 });

      return builder;
    }
}
```

**Giải thích:**
- `ClearProviders()` - Xóa default ASP.NET Core logging providers
- `ReadFrom.Configuration()` - Load config từ logger.json
- `ReadFrom.Services()` - Allow enrichers access to DI services
- `Enrich.WithProperty()` - Add static properties to all logs

---

## 5. Update Program.cs

### Bước 5.1: Integrate Serilog in Program.cs

**Update file:** `src/Host/Host/Program.cs`

```csharp
using ECO.WebApi.Application;
using ECO.WebApi.Host.Configurations;
using ECO.WebApi.Infrastructure;
using ECO.WebApi.Infrastructure.Logging;
using Serilog;

// 1. Initialize static logger (bootstrap)
StaticLogger.EnsureInitialized();
Log.Information("Server Booting Up...");

try
{
    var builder = WebApplication.CreateBuilder(args);

 // 2. Load configurations (includes logger.json)
    builder.AddConfigurations();
    
    // 3. Register Serilog (full configuration)
    builder.RegisterSerilog();

    // 4. Add services to DI container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    
    // 5. Add layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    
    // 6. Add Swagger
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
    Title = "ECO.WebApi",
            Version = "v1",
            Description = "E-Commerce API built with Clean Architecture"
    });
    });

    // 7. Build application
    var app = builder.Build();

    // 8. Log application built
    Log.Information("Application built successfully");

    // 9. Configure middleware pipeline
    if (app.Environment.IsDevelopment())
    {
app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 10. Request logging middleware
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // 11. Use Infrastructure middleware
    app.UseInfrastructure(builder.Configuration);
    
    // 12. Map endpoints
    app.MapEndpoints();

    // 13. Run application
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

**Thêm features:**
- `UseSerilogRequestLogging()` - Log HTTP requests/responses
- `EnrichDiagnosticContext` - Add extra properties to request logs
- Better error handling với detailed error messages

**Lưu ý:** Đã remove dòng `await app.Services.InitializeDatabasesAsync()` vì phần này sẽ có ở BUILD_08.

---

## 6. Logging Best Practices

### Bước 6.1: Log Levels Usage

```csharp
// Verbose - Quá chi tiết, chỉ dùng khi debug sâu
Log.Verbose("Processing item {ItemId}", itemId);

// Debug - Thông tin development/troubleshooting
Log.Debug("Cache miss for key {CacheKey}", key);

// Information - General flow của application
Log.Information("User {UserId} logged in successfully", userId);

// Warning - Unexpected nhưng không critical
Log.Warning("Rate limit approaching for IP {IpAddress}", ipAddress);

// Error - Lỗi cần attention
Log.Error(ex, "Failed to process order {OrderId}", orderId);

// Fatal - Application không thể tiếp tục
Log.Fatal(ex, "Database connection failed. Application cannot start.");
```

---

### Bước 6.2: Structured Logging Example

```csharp
// ❌ BAD - String concatenation
Log.Information("User " + userId + " placed order " + orderId);

// ✅ GOOD - Structured logging
Log.Information("User {UserId} placed order {OrderId}", userId, orderId);

// ✅ BETTER - With object
Log.Information("Order placed: {@Order}", new
{
    UserId = userId,
    OrderId = orderId,
    Total = total,
    Items = items.Count
});
```

**Benefits:**
- Searchable: Có thể query `UserId = "123"`
- Analyzable: Aggregate và analytics
- Machine-readable: Parse và process logs

---

### Bước 6.3: Using LogContext

**File:** `src/Infrastructure/Infrastructure/Identity/UserService.cs` (example)

```csharp
using Serilog.Context;

public class UserService : IUserService
{
    public async Task<UserDto> GetByIdAsync(string userId)
{
        // Push context property
        using (LogContext.PushProperty("UserId", userId))
        {
            Log.Information("Fetching user details");
   
            var user = await _db.Users.FindAsync(userId);
      
            if (user == null)
            {
                Log.Warning("User not found");
                throw new NotFoundException("User not found");
}
      
            Log.Information("User details fetched successfully");
            return user.Adapt<UserDto>();
        }
    }
}
```

**Output log:**
```
[Information] Fetching user details | UserId: "abc123"
[Information] User details fetched successfully | UserId: "abc123"
```

---

## 7. Environment-Specific Configurations

### Bước 7.1: Development Configuration

**File:** `src/Host/Host/Configurations/logger.Development.json`

```json
{
  "Serilog": {
  "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.EntityFrameworkCore.Database.Command": "Information"
      }
  },
 "WriteTo": [
      {
        "Name": "Console",
        "Args": {
"outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
  ]
  }
}
```

**Features:**
- `Default: Debug` - More verbose logging
- EF Core SQL queries visible
- Prettier console output

---

### Bước 7.2: Production Configuration

**File:** `src/Host/Host/Configurations/logger.Production.json`

```json
{
  "Serilog": {
  "MinimumLevel": {
      "Default": "Information",
      "Override": {
   "Microsoft": "Warning",
      "System": "Warning"
      }
    },
    "WriteTo": [
{
        "Name": "Async",
  "Args": {
          "configure": [
          {
              "Name": "File",
       "Args": {
                "path": "/var/log/eco-webapi/log-.txt",
              "rollingInterval": "Day",
        "rollOnFileSizeLimit": true,
                "fileSizeLimitBytes": 52428800,
     "retainedFileCountLimit": 30,
      "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
    }
    }
          ]
        }
      }
 ]
  }
}
```

**Features:**
- `Default: Information` - Less noise
- Larger files (50MB)
- Keep logs 30 days
- Production log path

---

## 8. Advanced Sinks (Optional)

### Bước 8.1: Seq (Centralized Logging)

**Add package:**
```xml
<PackageReference Include="Serilog.Sinks.Seq" Version="5.2.2" />
```

**Update logger.json:**
```json
{
  "Serilog": {
    "WriteTo": [
    {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341",
  "apiKey": "your-api-key"
        }
      }
    ]
  }
}
```

---

### Bước 8.2: Elasticsearch

**Add package:**
```xml
<PackageReference Include="Serilog.Sinks.Elasticsearch" Version="9.0.0" />
```

**Update logger.json:**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Elasticsearch",
  "Args": {
   "nodeUris": "http://localhost:9200",
          "indexFormat": "eco-webapi-{0:yyyy.MM}",
       "autoRegisterTemplate": true
        }
      }
    ]
  }
}
```

---

## 9. Testing Logging

### Bước 9.1: Run Application

```bash
dotnet run --project src/Host/Host/Host.csproj
```

**Expected console output:**
```
[12:00:00 INF] Server Booting Up...
[12:00:01 INF] Application built successfully
[12:00:02 INF] Application Starting...
[12:00:02 INF] Listening on: https://localhost:7001, http://localhost:5001
[12:00:02 INF] Application started. Press Ctrl+C to shut down.
```

---

### Bước 9.2: Test Request Logging

```bash
curl https://localhost:7001/api/health
```

**Expected log:**
```
[12:00:05 INF] HTTP GET /api/health responded 200 in 12.3456 ms
```

---

### Bước 9.3: Check Log Files

**Log file location:**
```
src/Host/Host/Logs/
└── log-20240101.txt
```

**Sample log content:**
```
2024-01-01 12:00:00.000 +00:00 [INF] Server Booting Up...
2024-01-01 12:00:01.234 +00:00 [INF] Application built successfully
2024-01-01 12:00:05.678 +00:00 [INF] HTTP GET /api/health responded 200 in 12.3456 ms {"RequestHost":"localhost:7001","RequestScheme":"https","UserAgent":"curl/7.68.0"}
```

---

## 10. Common Issues

### Issue 1: "Logs not appearing in console"

**Nguyên nhân:** Serilog not properly registered.

**Giải pháp:**
```csharp
// Verify in Program.cs
builder.RegisterSerilog(); // Must be called before Build()
```

---

### Issue 2: "Log files not created"

**Nguyên nhân:** Permissions issue hoặc path không tồn tại.

**Giải pháp:**
```json
// Use relative path
"path": "Logs/log-.txt"

// Or absolute path với write permissions
"path": "C:/Logs/ECO.WebApi/log-.txt"
```

---

### Issue 3: "Too many log files"

**Nguyên nhân:** `retainedFileCountLimit` quá cao.

**Giải pháp:**
```json
{
  "retainedFileCountLimit": 7  // Keep only 7 days
}
```

---

## 11. Summary

### ✅ Đã hoàn thành trong bước này:

**Serilog Setup:**
- ✅ Serilog packages installed
- ✅ StaticLogger for bootstrap logging
- ✅ Serilog configuration trong logger.json
- ✅ Multiple sinks (Console, File, Async)
- ✅ Enrichers (Context, Thread, Machine, Exception)
- ✅ Environment-specific configurations
- ✅ Request logging middleware

**Best Practices:**
- ✅ Structured logging examples
- ✅ Log levels usage
- ✅ LogContext for scoped properties
- ✅ Production-ready configuration

### 📊 Log Output Structure:

**Console (Development):**
```
[HH:mm:ss LEVEL] Message with {Properties}
```

**File (Production):**
```
yyyy-MM-dd HH:mm:ss.fff +00:00 [LEVEL] Message {Properties}
Exception details...
```

---

## 12. Next Steps

**Tiếp theo:** [BUILD_08 - Database Initialization và Migrations](BUILD_08_Database_Initialization.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Setup Migrators.MSSQL project
2. ✅ Tạo MigrationDbContextFactory
3. ✅ Tạo DatabaseInitializer
4. ✅ Run first migration
5. ✅ Setup database seeding structure
6. ✅ Seed Actions, Functions, Roles
7. ✅ Seed Admin User
8. ✅ Seed Permissions

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
