---
description: "Phase 1: Foundation Setup — Solution, Build Config, 5 Layers cơ bản"
---

# Phase 1: Foundation Setup

**Mục tiêu**: Solution build thành công, có thể chạy API (chưa có database)
**Prerequisites**: .NET 8 SDK installed

---

## Bước 1: Solution Setup

// turbo
```
Đọc file: docs/BUILD_01_Solution_Setup.md
```

**Việc cần làm:**
- Tạo solution file (`ECO.WebApi.sln`)
- Tạo 6 projects theo thứ tự dependency
- Setup `Directory.Build.props` (StyleCop, SonarAnalyzer)
- Setup `Directory.Build.targets` (XML documentation)
- Tạo `stylecop.json` (code style rules)
- Tạo `.editorconfig` (editor formatting)

**Verify:**
// turbo
```bash
dotnet build
```
Expect: 0 errors

---

## Bước 2: Shared Layer

```
Đọc file: docs/BUILD_02_Shared_Layer.md
```

**Việc cần làm:**
- Setup `Shared.csproj` (không dependency nào)
- Tạo `ECOAction` constants (View, Create, Update, Delete...)
- Tạo `ECOFunction` constants (Dashboard, User, Role...)
- Tạo `ECORoles` constants (Admin, Basic)
- Tạo `ECOClaims` constants (Fullname, Permission...)
- Tạo `ECOPermission` record (generate permissions động)

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 3: Domain Layer

```
Đọc file: docs/BUILD_03_Domain_Layer.md
```

**Việc cần làm:**
- Setup `Domain.csproj` (phụ thuộc Shared)
- Add packages: `Microsoft.AspNetCore.Identity`, `NewId`
- Tạo `ApplicationUser` entity (kế thừa IdentityUser)
- Tạo `ApplicationRole` entity (kế thừa IdentityRole)
- Tạo `ApplicationRoleClaim` entity
- Tạo Custom Identity entities (Action, Function, Permission)

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 4: Application Layer

```
Đọc file: docs/BUILD_04_Application_Layer.md
```

**Việc cần làm:**
- Setup `Application.csproj` (phụ thuộc Domain + Shared)
- Add packages: `MediatR`, `FluentValidation`, `Mapster`, `Ardalis.Specification`
- Tạo `Startup.cs` (register MediatR, FluentValidation)
- Tạo Common interfaces (`ICurrentUser`, `ISerializerService`, `IRepository`...)
- Tạo Common models (`BaseFilter`, `PaginationFilter`, `Search`, `Filter`)
- Setup GlobalUsings

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 5: Infrastructure Layer

```
Đọc file: docs/BUILD_05_Infrastructure_Layer.md
```

**Việc cần làm:**
- Setup `Infrastructure.csproj` (phụ thuộc Application + Domain)
- Add packages: `EF Core`, `Hangfire`, `Serilog`, `MailKit`...
- Tạo `ApplicationDbContext` (kế thừa `BaseDbContext`)
- Tạo modular `Startup.cs` pattern
- Setup Persistence module (DbContext, Repository)

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 6: Host Layer

```
Đọc file: docs/BUILD_06_Host_Layer.md
```

**Việc cần làm:**
- Setup `Host.csproj` (phụ thuộc Infrastructure + Application)
- Add packages: `Swashbuckle`, `FluentValidation.AspNetCore`
- Tạo `Program.cs` (configure middleware pipeline)
- Tạo `BaseApiController`
- Setup Swagger documentation
- Tạo configuration files structure

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```

---

## ✅ Phase 1 Checkpoint

**Kiểm tra:**
- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] `dotnet run` — API khởi động thành công
- [ ] Swagger UI accessible tại https://localhost:7001/swagger
- [ ] 6 projects tồn tại: Shared, Domain, Application, Infrastructure, Host, Migrators.MSSQL

**⏸️ DỪNG: Notify user review Phase 1 trước khi tiếp tục Phase 2 (`/build-phase2`)**
