---
description: "Phase 2: Core Infrastructure — Logging, Database, Base Entities, Repository"
---

# Phase 2: Core Infrastructure

**Mục tiêu**: Logging hoạt động, Database ready, Repository pattern complete
**Prerequisites**: Phase 1 hoàn thành (`dotnet build` OK)

---

## Bước 7: Logging Setup

```
Đọc file: docs/BUILD_07_Logging_Setup.md
```

**Việc cần làm:**
- Serilog setup (Console, File, Seq)
- `LoggerSettings` configuration
- Structured logging
- Request logging middleware
- Exception logging integration

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: Logs xuất hiện trong console với structured format

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 7** trước khi sang bước tiếp theo.

---

## Bước 8: Database Initialization

```
Đọc file: docs/BUILD_08_Database_Initialization.md
```

**Việc cần làm (QUAN TRỌNG — thứ tự dependency):**

1. Tạo Interfaces: `IDatabaseInitializer`, `ICustomSeeder`
2. Tạo Implementations theo thứ tự:
   - `DatabaseInitializer` (implement `IDatabaseInitializer`)
   - `ApplicationDbInitializer` (kế thừa `DatabaseInitializer`)
   - `ApplicationDbSeeder` (seed Actions → Functions → Roles → Admin User)
   - `CustomSeederRunner`
   - `NotificationSeeder` (implement `ICustomSeeder`)
3. Register và Run trong `Program.cs`

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: Database tự động migrate và seed data

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 8** trước khi sang bước tiếp theo.

---

## Bước 9: Domain Base Entities

```
Đọc file: docs/BUILD_09_Domain_Base_Entities.md
```

**Việc cần làm:**
- `IEvent` interface — Domain event marker
- `DomainEvent` base class — With TriggeredOn timestamp
- `IEntity` interface — Base entity contract với DomainEvents collection
- `IAuditableEntity` interface — Created/Modified tracking
- `BaseEntity` — Sequential GUID generation, DomainEvents
- `AuditableEntity` — Implement `IAuditableEntity`
- `IAggregateRoot` — Marker for aggregate roots
- Entity Lifecycle Events — Created, Updated, Deleted events

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 10: Service Registration

```
Đọc file: docs/BUILD_10_Service_Registration.md
```

**Việc cần làm:**
- Marker interfaces: `ITransientService`, `IScopedService`, `ISingletonService`
- Auto-registration với reflection
- Convention-based service discovery

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 11: Repository Pattern

```
Đọc file: docs/BUILD_11_Repository_Pattern.md
```

**Việc cần làm:**
- Tạo Search/Filter models (Search, Filter, BaseFilter, PaginationFilter)
- Tạo `IRepository<T>`, `IReadRepository<T>`, `IRepositoryWithEvents<T>`
- Implement `ApplicationDbRepository<T>`
- Tạo `EventAddingRepositoryDecorator<T>` (decorator pattern)
- Tạo base specifications: `EntitiesByBaseFilterSpec`, `EntitiesByPaginationFilterSpec`

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 11.1: Specification Pattern

```
Đọc file: docs/BUILD_11_Specification.md
```

**Việc cần làm:**
- `SpecificationBuilderExtensions` (full implementation)
- Advanced query building
- Complex filtering và sorting
- Paging support

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 11.2: Property Expressions

```
Đọc file: docs/BUILD_11_1_PropertyExpressions.md
```

**Việc cần làm:**
- Expression tree helpers
- Dynamic property access
- Type-safe property expressions

**Verify:**
// turbo
```bash
dotnet build
```

---

## ✅ Phase 2 Checkpoint

**Kiểm tra:**
- [ ] `dotnet build` — 0 errors
- [ ] Logging hoạt động (console + file output)
- [ ] Database tự động migrate khi chạy app
- [ ] Seed data: Actions, Functions, Roles, Admin User tồn tại trong DB
- [ ] Repository interfaces + implementations compile OK
- [ ] Thực hiện chỉnh sửa docs sau khi đã hoàn thiện phase này cho phù hợp (cập nhật docs nếu trong quá trình implement có thay đổi/tối ưu so với docs gốc).

**⏸️ DỪNG: Notify user review Phase 2 trước khi tiếp tục Phase 3 (`/build-phase3`)**
