# ECO.WebApi - Setup Guide (Hướng dẫn Setup từ Source Code)

> 📘 **Phân biệt:** 
> - **BUILD_INDEX.md**: Hướng dẫn **xây dựng solution từ đầu** (for learning)
> - **SETUP_GUIDE.md**: Hướng dẫn **setup và chạy project có sẵn** (for deployment/development)

---

## 📋 Mục lục

1. [Prerequisites (Yêu cầu hệ thống)](#1-prerequisites)
2. [Clone và Setup Solution](#2-clone-và-setup-solution)
3. [Database Configuration](#3-database-configuration)
4. [Configuration Files](#4-configuration-files)
5. [Run Application](#5-run-application)
6. [Verify Setup](#6-verify-setup)
7. [Development Workflow](#7-development-workflow)
8. [Production Deployment](#8-production-deployment)
9. [Troubleshooting](#9-troubleshooting)
10. [Architecture Overview](#10-architecture-overview)

---

## 1. Prerequisites

### 1.1. Required Software

| Software | Version | Download | Purpose |
|----------|---------|----------|---------|
| .NET SDK | 8.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) | Runtime và build tools |
| SQL Server | 2019+ | [Download](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) | Database engine |
| Visual Studio 2022 | 17.8+ | [Download](https://visualstudio.microsoft.com/) | IDE (optional) |
| VS Code | Latest | [Download](https://code.visualstudio.com/) | IDE alternative |
| Git | Latest | [Download](https://git-scm.com/) | Version control |

### 1.2. Optional Software (for full features)

| Software | Purpose | Download |
|----------|---------|----------|
| Redis | Distributed caching | [Download](https://redis.io/download) |
| Seq | Centralized logging | [Download](https://datalust.co/seq) |
| MailHog | Email testing | [Download](https://github.com/mailhog/MailHog) |
| Postman | API testing | [Download](https://www.postman.com/) |

### 1.3. Check Installed Versions

```bash
# Kiểm tra .NET SDK
dotnet --version
# Output should be: 8.0.x

# Kiểm tra Git
git --version

# Kiểm tra SQL Server
sqlcmd -S localhost -Q "SELECT @@VERSION"
```

---

## 2. Clone và Setup Solution

### 2.1. Clone Repository

```bash
# Clone từ GitHub
git clone https://github.com/vuongnv1206/eco.git
cd eco

# Hoặc nếu đã có source code
cd D:\MyCode\eco
```

### 2.2. Restore Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Verify restoration thành công
dotnet build
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 3. Database Configuration

### 3.1. Tạo Database

**Option 1: SQL Server Management Studio (SSMS)**
```sql
CREATE DATABASE ECODb;
GO
```

### 3.2. Update Connection String

**File:** `src/Host/Host/Configurations/database.json`

```json
{
  "DatabaseSettings": {
    "DBProvider": "mssql",
    "ConnectionString": "Server=localhost;Database=ECODb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

**Connection String Options:**

**SQL Server Authentication:**
```json
"ConnectionString": "Server=localhost;Database=ECODb;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

**Azure SQL Database:**
```json
"ConnectionString": "Server=tcp:yourserver.database.windows.net,1433;Database=ECODb;User ID=yourusername;Password=yourpassword;Encrypt=True;Connection Timeout=30;"
```

### 3.3. Run Migrations

```bash
# Di chuyển vào thư mục Host
cd src/Host/Host/

# Tạo migration (nếu chưa có)
dotnet ef migrations add InitialCreate --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj

# Apply migrations
dotnet ef database update --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj
```

**Expected Output:**
```
Applying migration '20240101120000_InitialCreate'.
Done.
```

### 3.4. Seed Data

Seed data tự động chạy khi application start lần đầu. Bao gồm:

- ✅ Actions (Create, Update, Delete, View, Search, Export, Import, Clean)
- ✅ Functions (User, Role, Product, Category, Dashboard, Hangfire)
- ✅ Roles (Admin, Basic)
- ✅ Admin User (email: admin@root.com, password: 123Pa$$word!)
- ✅ Permissions (auto-generated từ Actions × Functions)

---

## 4. Configuration Files

Tất cả configuration files nằm trong `src/Host/Host/Configurations/`:

### 4.1. database.json (✅ Required)

```json
{
  "DatabaseSettings": {
    "DBProvider": "mssql",
 "ConnectionString": "Server=localhost;Database=ECODb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

### 4.2. security.json (✅ Required)

```json
{
  "SecuritySettings": {
  "Key": "your-super-secret-key-at-least-32-characters-long!",
    "Issuer": "ECO.WebApi",
    "Audience": "ECO.WebApi",
    "TokenExpirationInMinutes": 60,
    "RefreshTokenExpirationInDays": 7
  }
}
```

**⚠️ QUAN TRỌNG:** 
- `Key` phải ít nhất 32 ký tự
- Đổi `Key` trong production
- Không commit `Key` vào Git

### 4.3. cache.json (Optional - for Redis)

```json
{
  "CacheSettings": {
    "UseDistributedCache": true,
  "PreferRedis": true,
    "RedisURL": "localhost:6379",
    "UseInMemory": false
  }
}
```

**Nếu không có Redis:**
```json
{
  "CacheSettings": {
    "UseDistributedCache": false,
    "PreferRedis": false,
    "UseInMemory": true
  }
}
```

### 4.4. mail.json (Optional - for Email)

```json
{
  "SMTPEmailSettings": {
    "From": "noreply@eco.com",
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UserName": "your-email@gmail.com",
    "Password": "your-app-password",
    "DisplayName": "ECO System",
    "EnableVerification": true,
    "EnableSsl": true
  }
}
```

**For Gmail:** Phải enable "App Password" trong Google Account settings.

**For MailHog (Development):**
```json
{
  "SMTPEmailSettings": {
    "From": "noreply@eco.com",
    "Host": "localhost",
    "Port": 1025,
    "UserName": "",
    "Password": "",
    "DisplayName": "ECO System",
    "EnableSsl": false
  }
}
```

### 4.5. hangfire.json (Optional - for Background Jobs)

```json
{
  "HangfireStorageSettings": {
    "StorageProvider": "mssql",
    "ConnectionString": "Server=localhost;Database=ECODb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### 4.6. googledrive.json (Optional - for Google Drive Integration)

```json
{
  "GoogleDriveSettings": {
    "ApplicationName": "ECO WebApi",
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret",
    "PathCredentials": "credentials.json"
  }
}
```

### 4.7. cors.json (Optional - for CORS)

```json
{
  "CorsSettings": {
    "Angular": "https://localhost:4200",
    "Blazor": "https://localhost:5001",
    "React": "https://localhost:3000"
  }
}
```

---

## 5. Run Application

### 5.1. Development Mode

**Option 1: Visual Studio**
1. Open `ECO.WebApi.sln`
2. Set `Host` as startup project
3. Press F5 hoặc Ctrl+F5

**Option 2: Command Line**
```bash
cd src/Host/Host/
dotnet run
```

**Option 3: VS Code**
1. Open workspace
2. Press F5
3. Select "Host" configuration

### 5.2. Watch Mode (Auto-reload)

```bash
cd src/Host/Host/
dotnet watch run
```

### 5.3. Verify Application Started

**Console Output:**
```
info: ECO.WebApi.Host[0]
      Application Starting...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Open in Browser:**
- Swagger UI: https://localhost:7001/swagger
- Hangfire Dashboard: https://localhost:7001/hangfire

---

## 6. Verify Setup

### 6.1. Test API Endpoints

**1. Health Check**
```bash
curl https://localhost:7001/health
```
Expected: `Healthy`

**2. Login với Admin Account**
```bash
curl -X POST https://localhost:7001/api/tokens \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@root.com",
    "password": "123Pa$$word!"
  }'
```

Expected Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "xxx-xxx-xxx",
  "refreshTokenExpiryTime": "2024-01-08T00:00:00Z"
}
```

**3. Test Authenticated Endpoint**
```bash
curl -X GET https://localhost:7001/api/users \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### 6.2. Verify Database

```sql
-- Check tables created
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

-- Check seed data
SELECT * FROM Actions;
SELECT * FROM Functions;
SELECT * FROM Roles;
SELECT * FROM AspNetUsers;
```

### 6.3. Verify Hangfire Dashboard

1. Navigate to https://localhost:7001/hangfire
2. Login: `admin` / `SecurePwd1!`
3. Check "Recurring Jobs" tab

---

## 7. Development Workflow

### 7.1. Tạo Migration mới

```bash
cd src/Host/Host/

# Tạo migration
dotnet ef migrations add YourMigrationName \
  --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj

# Apply migration
dotnet ef database update \
  --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj
```

### 7.2. Rollback Migration

```bash
# Rollback to specific migration
dotnet ef database update PreviousMigrationName \
  --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj

# Remove last migration
dotnet ef migrations remove \
  --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj
```

### 7.3. Add New Feature

**Ví dụ: Thêm "Order" feature**

**1. Tạo Domain Entity**
```csharp
// src/Core/Domain/Ordering/Order.cs
public class Order : AuditableEntity, IAggregateRoot
{
    public string OrderNumber { get; private set; }
  public decimal TotalAmount { get; private set; }
    // ...
}
```

**2. Tạo Application DTOs**
```csharp
// src/Core/Application/Ordering/OrderDto.cs
public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; }
    // ...
}
```

**3. Tạo Application Interfaces**
```csharp
// src/Core/Application/Ordering/IOrderService.cs
public interface IOrderService : ITransientService
{
    Task<OrderDto> GetByIdAsync(Guid id);
    // ...
}
```

**4. Implement Service**
```csharp
// src/Infrastructure/Infrastructure/Ordering/OrderService.cs
public class OrderService : IOrderService
{
    // Implementation
}
```

**5. Add Controller**
```csharp
// src/Host/Host/Controllers/Ordering/OrderController.cs
public class OrderController : BaseApiController
{
    // Endpoints
}
```

**6. Create Migration**
```bash
dotnet ef migrations add AddOrderEntity \
  --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj
```

---

## 8. Production Deployment

### 8.1. Build for Production

```bash
# Build Release
dotnet publish src/Host/Host/Host.csproj \
  -c Release \
  -o ./publish

# Output: ./publish folder
```

### 8.2. Environment Variables

**appsettings.Production.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning"
    }
  },
  "AllowedHosts": "yourdomain.com"
}
```

**Environment Variables:**
```bash
export ASPNETCORE_ENVIRONMENT=Production
export DatabaseSettings__ConnectionString="Server=prod-server;Database=ECODb;User=sa;Password=xxx"
export SecuritySettings__Key="production-secret-key-super-long-and-secure-123456789"
```

### 8.3. IIS Deployment

**1. Install .NET 8 Hosting Bundle**
- Download: https://dotnet.microsoft.com/download/dotnet/8.0

**2. Create IIS Site**
- Site name: ECO.WebApi
- Physical path: C:\inetpub\wwwroot\eco
- Binding: https://yourdomain.com

**3. Application Pool Settings**
- .NET CLR Version: No Managed Code
- Managed pipeline mode: Integrated

**4. Deploy Files**
```bash
# Copy publish folder to IIS
xcopy /E /Y .\publish\* C:\inetpub\wwwroot\eco\
```

### 8.4. Docker Deployment

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECO.WebApi.Host.dll"]
```

**Build và Run:**
```bash
# Build image
docker build -t eco-webapi:latest .

# Run container
docker run -d \
  -p 8080:80 \
  -p 8443:443 \
  -e DatabaseSettings__ConnectionString="Server=host.docker.internal;Database=ECODb;User=sa;Password=xxx" \
  --name eco-api \
  eco-webapi:latest
```

---

## 9. Troubleshooting

### 9.1. Build Errors

**Error:** `CS0234: The type or namespace name 'X' does not exist`
```bash
# Solution: Restore packages
dotnet restore
dotnet clean
dotnet build
```

**Error:** `StyleCop.Analyzers warnings`
```bash
# Solution: Fix code style hoặc disable warnings
# Trong .csproj:
<PropertyGroup>
  <NoWarn>SA1600;SA1601</NoWarn>
</PropertyGroup>
```

### 9.2. Database Errors

**Error:** `Cannot open database "ECODb"`
```sql
-- Solution: Create database
CREATE DATABASE ECODb;
GO
```

**Error:** `Login failed for user 'sa'`
```bash
# Solution: Check connection string
# Enable SQL Server authentication
# Reset SA password
```

**Error:** `The instance of entity type 'X' cannot be tracked`
```csharp
// Solution: Use AsNoTracking() for read queries
var entity = await _db.Products
    .AsNoTracking()
    .FirstOrDefaultAsync(x => x.Id == id);
```

### 9.3. JWT Token Errors

**Error:** `IDX10503: Signature validation failed`
```json
// Solution: Check security.json key
{
  "SecuritySettings": {
    "Key": "must-be-at-least-32-characters-long!"
  }
}
```

**Error:** `Token expired`
```json
// Solution: Increase token expiration
{
  "SecuritySettings": {
    "TokenExpirationInMinutes": 120
  }
}
```

### 9.4. Hangfire Errors

**Error:** `Hangfire dashboard not accessible`
```csharp
// Solution: Check Startup.cs
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireCustomBasicAuthenticationFilter() }
});
```

**Error:** `Job failed`
```csharp
// Solution: Check Hangfire Console logs
// Add try-catch in job methods
```

### 9.5. Email Sending Errors

**Error:** `SMTP authentication failed`
```json
// Solution: Use app password for Gmail
// Or check mail.json settings
{
  "SMTPEmailSettings": {
    "EnableSsl": true,
    "Port": 587
  }
}
```

---

## 10. Architecture Overview

### 10.1. Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────┐
│                 Host Layer   │
│  - Controllers (API endpoints) │
│  - Middleware pipeline        │
│  - Program.cs (startup configuration)      │
│  - appsettings.json, Configurations/*.json        │
└────────────────────┬──────────────────────────────────┘
     │ depends on
┌────────────────────▼──────────────────────────────────┐
│   Infrastructure Layer         │
│  - DbContext, Repositories           │
│  - Identity, Authentication, Authorization    │
│  - Caching (Redis), Mailing (SMTP)            │
│  - BackgroundJobs (Hangfire)           │
│  - FileStorage, BlobStorage, GoogleDrive │
│  - Logging (Serilog), Auditing   │
└────────────────────┬──────────────────────────────────┘
         │ depends on
┌────────────────────▼──────────────────────────────────┐
│        Application Layer    │
│  - Use Cases (CQRS with MediatR)  │
│  - DTOs, Mappers (Mapster)│
│  - Validators (FluentValidation)   │
│  - Interfaces (IUserService, IProductService)    │
│  - Specifications (Ardalis.Specification)   │
└────────────────────┬──────────────────────────────────┘
           │ depends on
┌────────────────────▼──────────────────────────────────┐
│     Domain Layer    │
│  - Entities (ApplicationUser, Product, Order)           │
│  - Value Objects │
│  - Domain Events   │
│  - Enums       │
│  - Domain Exceptions      │
└────────────────────┬──────────────────────────────────┘
    │ depends on
┌────────────────────▼──────────────────────────────────┐
│      Shared Layer  │
│  - Authorization Constants       │
│  - Common Contracts      │
│  - Event Abstractions      │
└────────────────────────────────────────────────────────┘
```

### 10.2. Key Features

**✅ Authentication & Authorization**
- JWT Token-based authentication
- Refresh token support
- Permission-based authorization
- OAuth2 (Google, Facebook)

**✅ Database**
- EF Core 8.0 với SQL Server
- Repository pattern
- Specification pattern
- Automatic migrations và seeding

**✅ Caching**
- In-Memory cache (development)
- Distributed cache với Redis (production)
- Cache invalidation strategies

**✅ Background Jobs**
- Hangfire dashboard
- Recurring jobs
- Fire-and-forget jobs
- Delayed jobs

**✅ Email**
- SMTP integration
- Razor template engine
- Email verification
- Password reset emails

**✅ File Storage**
- Local file storage
- Azure Blob Storage
- Google Drive integration

**✅ Logging**
- Serilog structured logging
- Multiple sinks: Console, File, Seq, Elasticsearch
- Request/response logging
- Exception logging

**✅ API Documentation**
- Swagger/OpenAPI
- XML documentation comments
- Request/response examples

---

## 📞 Support & Resources

### Official Documentation
- [BUILD_INDEX.md](BUILD_INDEX.md) - Xây dựng solution từ đầu
- [GitHub Repository](https://github.com/vuongnv1206/eco)

### Learning Resources
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)

### Community
- GitHub Issues: [Report bugs](https://github.com/vuongnv1206/eco/issues)
- Email: support@eco.com

---

## ✅ Quick Checklist

### Initial Setup
- [ ] .NET 8 SDK installed
- [ ] SQL Server running
- [ ] Repository cloned
- [ ] Packages restored (`dotnet restore`)
- [ ] Connection string configured
- [ ] Migrations applied
- [ ] Application runs successfully

### Development
- [ ] Swagger UI accessible
- [ ] Can login với admin account
- [ ] Database seed data complete
- [ ] Hangfire dashboard accessible
- [ ] API endpoints working

### Production Ready
- [ ] appsettings.Production.json configured
- [ ] Secure JWT key set
- [ ] HTTPS enabled
- [ ] CORS configured
- [ ] Logging configured
- [ ] Health checks enabled
- [ ] Performance tested
- [ ] Security audited

---

**🎉 Chúc bạn setup thành công!**

*Nếu gặp vấn đề, tham khảo section [Troubleshooting](#9-troubleshooting) hoặc mở issue trên GitHub.*

---

*Last Updated: 2024 | Version: 1.0 | .NET 8.0*
