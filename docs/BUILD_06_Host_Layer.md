# Xây dựng Host Layer

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 5 (Infrastructure Layer) hoàn thành

Tài liệu này hướng dẫn xây dựng Host Layer - ASP.NET Core Web API entry point cơ bản của application.

---

## 1. Overview

**Làm gì:** Tạo Host project (ASP.NET Core Web API) và setup application entry point cơ bản.

**Tại sao:**
- Host là entry point của application
- Chứa Program.cs, Controllers, Configuration files
- Kết nối tất cả layers với nhau
- Expose API endpoints ra ngoài

**Trong bước này chúng ta sẽ:**
- ✅ Setup Host project với dependencies
- ✅ Setup configuration loading system
- ✅ Tạo Program.cs minimal (chưa có database initialization)
- ✅ Configure Swagger/OpenAPI
- ✅ Tạo base controller structure
- ✅ Verify application chạy được

**Chưa implement:**
- ❌ Logging chi tiết (BUILD_07)
- ❌ Database initialization & migrations (BUILD_08)
- ❌ Database seeding (BUILD_08)

---

## 2. Setup Host Project

### Bước 2.1: Tạo Project File

**File:** `src/Host/Host/Host.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
		<RootNamespace>ECO.WebApi.Host</RootNamespace>
		<AssemblyName>ECO.WebApi.Host</AssemblyName>
	</PropertyGroup>

	<!-- Project References -->
	<ItemGroup>
		<ProjectReference Include="..\..\Core\Application\Application.csproj" />
		<ProjectReference Include="..\..\Infrastructure\Infrastructure\Infrastructure.csproj" />
	</ItemGroup>

	<!-- Web API Packages -->
	<ItemGroup>
		<PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.0" />
	</ItemGroup>

	<!-- Configuration Files -->
	<ItemGroup>
		<Content Update="Configurations\*.json">
			<CopyToOutputDirectory>Always</CopyToOutputDirectory>
		</Content>
	</ItemGroup>
</Project>
```

**Giải thích packages:**
- `Sdk="Microsoft.NET.Sdk.Web"` - Web application SDK
- `Swashbuckle.AspNetCore` - Swagger/OpenAPI documentation
- Application & Infrastructure references

**Lưu ý:** 
- Chưa reference Migrators project (sẽ thêm ở BUILD_08)
- Chưa có EF Core Design package (sẽ thêm ở BUILD_08)
- Chưa có Serilog (sẽ thêm ở BUILD_07)

---

## 3. Setup Configuration System

### Bước 3.1: Tạo Configuration Loader

**Làm gì:** Load configuration từ multiple JSON files.

**File:** `src/Host/Host/Configurations/Startup.cs`

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
     
         // Environment variables (override JSON)
     .AddEnvironmentVariables();
     
  return builder;
    }
}
```

**Tại sao pattern này:**
- **Separation:** Mỗi module có config file riêng
- **Environment-specific:** Support Development/Staging/Production configs
- **Hot reload:** `reloadOnChange: true` cho development
- **Override hierarchy:** JSON → Environment-specific JSON → Environment Variables

**Ví dụ override:**
```
database.json (base) 
  → database.Development.json (override for Dev)
    → Environment Variables (highest priority)
```

---

### Bước 3.2: Tạo Configuration Files

**File:** `src/Host/Host/Configurations/database.json`

```json
{
  "DatabaseSettings": {
    "DBProvider": "mssql",
    "ConnectionString": "Server=localhost;Database=ECODb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

**File:** `src/Host/Host/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
  "Microsoft.AspNetCore": "Warning",
  "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

**File:** `src/Host/Host/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```



## 4. Tạo Program.cs (Minimal Version)

### Bước 4.1: Application Entry Point

**File:** `src/Host/Host/Program.cs`

```csharp
using ECO.WebApi.Application;
using ECO.WebApi.Host.Configurations;
using ECO.WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Load configurations
builder.AddConfigurations();

// 2. Add services to DI container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

    // 4. Add layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // 5. Add Swagger
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "ECO.WebApi",
            Version = "v1",
            Description = "E-Commerce API built with Clean Architecture"
        });
    });

    // 6. Build application
    var app = builder.Build();

    // 7. Configure middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

// 7. Use Infrastructure middleware
app.UseInfrastructure(builder.Configuration);

// 8. Map endpoints
app.MapEndpoints();

// 9. Run application
app.Run();
```

**Giải thích thứ tự quan trọng:**

**Phase 1: Setup (Before Build)**
```
1. Load Configurations       → JSON files
2. Add Controllers/Swagger   → ASP.NET Core services
3. AddApplication()          → MediatR, FluentValidation
4. AddInfrastructure()       → DbContext, Repositories
5. Add Swagger               → API documentation
```
**Phase 2: Build**
```
6. Build application         → Create IServiceProvider
```
**Phase 3: Runtime (After Build)**
```
7. Configure middleware      → Request pipeline
8. UseInfrastructure()       → Routing, HTTPS redirection
9. MapEndpoints()           → Map controllers
10. Run()                    → Start listening
```


---

## 5. Tạo Controllers Structure

### Bước 5.1: Base Controller

**File:** `src/Host/Host/Controllers/BaseApiController.cs`

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
```

**Tại sao:**
- Lazy-load MediatR sender
- Consistent route pattern (`api/[controller]`)
- Base class cho tất cả controllers

---

### Bước 5.2: Health Check Controller

**File:** `src/Host/Host/Controllers/HealthController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
{
        return Ok(new
        {
       Status = "Healthy",
        Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        });
    }
}
```

**Giải thích:**
- Simple health check endpoint
- Không cần database connection
- Return application status

---

## 6. Verify Setup

### Bước 6.1: Build Solution

```bash
# Build solution
dotnet build

# Expected: Build succeeded
```

---

### Bước 6.2: Run Application

```bash
# Run application
dotnet run --project src/Host/Host/Host.csproj

# Hoặc từ Visual Studio: F5
```

**Console output expected:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

### Bước 6.3: Test Swagger UI

**URL:** `https://localhost:7001/swagger`

**Expected:** Swagger UI hiển thị với Health endpoint

**Screenshot expected:**
```
ECO.WebApi v1
E-Commerce API built with Clean Architecture

GET /api/Health
  Returns health status
```

---

### Bước 6.4: Test Health Endpoint

```bash
curl https://localhost:7001/api/health
```

**Expected response:**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-01T12:00:00.000Z",
  "environment": "Development"
}
```

## 7. Summary

### ✅ Đã hoàn thành trong bước này:

**Host Setup:**
- ✅ Host project với Web API SDK
- ✅ Configuration system (JSON files + Environment Variables)
- ✅ Program.cs minimal version
- ✅ Base controller structure
- ✅ Health check endpoint
- ✅ Swagger/OpenAPI configuration

**Verification:**
- ✅ Application builds successfully
- ✅ Application runs successfully
- ✅ Swagger UI accessible
- ✅ Health endpoint working

### ❌ Chưa implement (sẽ có ở bước sau):

- ❌ Serilog structured logging (BUILD_07)
- ❌ Migrators project (BUILD_08)
- ❌ Database initialization (BUILD_08)
- ❌ Database seeding (BUILD_08)
- ❌ Repository pattern (BUILD_09)

### 📊 Project Structure hiện tại:

```
src/Host/Host/
├── Configurations/
│   ├── Startup.cs
│   └── database.json
├── Controllers/
│   ├── BaseApiController.cs
│   └── HealthController.cs
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
└── Host.csproj
```

---

## 9. Understanding the Setup

### 9.1. Configuration Loading Flow

```
1. appsettings.json (base)
   ↓
2. appsettings.Development.json (environment override)
   ↓
3. database.json (module config)
   ↓
4. database.Development.json (module environment override)
   ↓
5. Environment Variables (highest priority)
```

---

### 9.2. Service Registration Order

```
AddConfigurations()
  ↓
AddControllers()
  ↓
AddApplication()      → MediatR, FluentValidation
  ↓
AddInfrastructure()       → DbContext, Services
  ↓
AddSwaggerGen()
  ↓
Build()             → Create service provider
```

**⚠️ Quan trọng:** Infrastructure phải đăng ký sau Application vì nó phụ thuộc vào Application interfaces.

---

### 9.3. Middleware Pipeline Order

```
UseSwagger()
  ↓
UseSwaggerUI()
  ↓
UseInfrastructure()
  ├── UseRouting()
  ├── UseHttpsRedirection()
  └── (more middleware later)
  ↓
MapEndpoints()      → Map controllers
  ↓
Run()        → Start server
```

---

## 10. Next Steps

**Tiếp theo:** [BUILD_07 - Setup Logging với Serilog](BUILD_07_Logging_Setup.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Install Serilog packages
2. ✅ Tạo StaticLogger cho bootstrap
3. ✅ Configure Serilog với logger.json
4. ✅ Setup multiple sinks (Console, File, Async)
5. ✅ Add enrichers
6. ✅ Environment-specific configs
7. ✅ Request logging middleware
8. ✅ Enhanced exception handling

**Sau đó:** [BUILD_08 - Database Initialization](BUILD_08_Database_Initialization.md)
- Setup Migrators project
- Database initialization
- Run first migration
- Database seeding

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
