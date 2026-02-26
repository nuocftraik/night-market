# Agent Project Brief — Clean Architecture .NET 8 WebApi

> **Mục đích:** Copy folder `docs/` (chứa file này + BUILD_XX docs) vào project mới. Agent đọc file này để hiểu toàn bộ yêu cầu.
>
> **Quy ước:** `{ProjectName}` = tên project thực tế (VD: ECO, MyApp...). Agent tự thay thế.

---

## 1. Architecture

```
Host (API) → Infrastructure (EF Core, Services) → Application (Use Cases) → Domain (Entities) → Shared (Constants)
```

- Shared: không phụ thuộc layer nào
- Domain: chỉ phụ thuộc Shared
- Application: phụ thuộc Domain + Shared
- Infrastructure: phụ thuộc Application + Domain
- Host: phụ thuộc Infrastructure + Application
- Migrators.MSSQL: phụ thuộc Infrastructure + Domain

---

## 2. Tech Stack

| Category | Technology | Purpose |
|---|---|---|
| Runtime | .NET 8 | Framework |
| DB | SQL Server + EF Core 8 | Data access |
| Identity | ASP.NET Core Identity | Auth system |
| CQRS | MediatR | Command/Query |
| Validation | FluentValidation | Pipeline validation |
| Mapping | Mapster | Object mapping |
| Specification | Ardalis.Specification | Query pattern |
| Logging | Serilog | Structured logging |
| Jobs | Hangfire | Background tasks |
| Email | MailKit + Razor | SMTP + templates |
| Cache | Redis / IMemoryCache | Distributed + local |
| API Docs | Swashbuckle | Swagger |
| Code Quality | StyleCop + SonarAnalyzer | Static analysis |
| GUID | NewId | Sequential GUIDs |

---

## 3. Project Structure

```
{ProjectRoot}/
├── {ProjectName}.WebApi.sln
├── Directory.Build.props / .targets / stylecop.json / .editorconfig
├── docs/                          ← BUILD docs (folder này)
└── src/
    ├── Core/
    │   ├── Shared/                # Layer 1
    │   ├── Domain/                # Layer 2
    │   └── Application/           # Layer 3
    ├── Infrastructure/Infrastructure/  # Layer 4
    ├── Host/Host/                 # Layer 5
    └── Migrators/Migrators.MSSQL/
```

---

## 4. Conventions

### Naming
- PascalCase classes, `_camelCase` private fields
- Namespace: `{ProjectName}.WebApi.{Layer}.{Module}`
- File-scoped namespaces, always use braces

### Patterns
- **Repository + Specification** — Data access abstraction
- **Decorator** — EventAddingRepositoryDecorator
- **MediatR Pipeline** — ValidationBehavior
- **Modular Startup** — Mỗi module có `Startup.cs` riêng
- **Marker Interfaces** — `ITransientService`, `IScopedService`, `ISingletonService`
- **Options Pattern** — `IOptions<T>` cho typed config

### Entity
- `BaseEntity` — Sequential GUID, DomainEvents
- `AuditableEntity` — Created/Modified + ISoftDelete
- Fluent API cho EF Core config (không Data Annotations)
- Schema separation: Identity, Catalog, Ordering, Payment, Auditing

---

## 5. Build Phases

> Mỗi phase có BUILD docs chi tiết trong `docs/`. Agent đọc BUILD doc tương ứng trước khi implement.

| Phase | Docs | Nội dung | Checkpoint |
|---|---|---|---|
| 1 | BUILD_01→06 | Solution + 5 Layers | `dotnet build` OK, Swagger accessible |
| 2 | BUILD_07→11 | Logging, DB, Base Entities, Repository | DB migrate + seed OK |
| 3 | BUILD_12→14 | CurrentUser, Exceptions, Validation | Proper error responses |
| 4 | BUILD_15→18 | JWT, Identity Services, Permissions, OAuth2 | Login + CRUD + permissions OK |
| 5 | BUILD_19→20 | Soft Delete, Auditing | Soft delete + audit trail OK |
| 6 | BUILD_21→25 | Caching, Storage, Email, Jobs | All services compile OK |
| 7 | BUILD_26→38 | Export, Catalog, Notifications, Payments | Business features OK |

### Quy trình mỗi bước
1. Đọc BUILD doc tương ứng trong `docs/`
2. Hiểu WHAT, WHY, HOW
3. Implement code → `dotnet build` → fix errors
4. Verify theo hướng dẫn trong doc
5. Dừng sau mỗi phase để user review

---

## 6. Agent Decision Guide: Skill vs Workflow vs Neither

> **Quan trọng:** Không phải cái gì cũng cần workflow hay skill. Agent phải phân tích trước khi tạo.

### Khi nào tạo **Workflow** (`.agents/workflows/`)
| Điều kiện | Ví dụ |
|---|---|
| Quy trình **lặp đi lặp lại** nhiều lần | Build project, deploy, run migrations |
| Có **thứ tự rõ ràng** phải tuân theo | Phase 1 → Phase 2 → ... |
| Nhiều bước **tuần tự**, mỗi bước ngắn | Setup DB → Seed → Verify |
| User muốn **gọi lại** bằng slash command | `/build-phase1`, `/deploy` |

### Khi nào tạo **Skill** (`.agents/skills/`)
| Điều kiện | Ví dụ |
|---|---|
| **Kiến thức chuyên sâu** cần tham khảo | Cách viết Specification, cách config Serilog |
| **Pattern/template** tái sử dụng | Tạo CRUD module mới, viết unit test |
| **Phức tạp về logic**, không phải về thứ tự | Expression trees, query optimization |
| Cần **scripts hỗ trợ** | Code generators, scaffolding |

### Khi nào **KHÔNG tạo** gì cả
| Điều kiện | Ví dụ |
|---|---|
| Task **đơn giản** (1-2 bước) | Fix bug, thêm property |
| Task **chỉ làm 1 lần** | Setup ban đầu, cấu hình 1 lần |
| Đã có **docs đủ rõ** để follow | BUILD docs đã chi tiết rồi |
| Agent **đã biết** làm (kiến thức phổ biến) | CRUD controller, add NuGet package |

### Decision Flowchart
```
Task mới → Có lặp lại không?
  ├─ Có → Có thứ tự rõ ràng?
  │        ├─ Có → TẠO WORKFLOW
  │        └─ Không → Cần kiến thức chuyên sâu?
  │                    ├─ Có → TẠO SKILL
  │                    └─ Không → KHÔNG TẠO GÌ
  └─ Không → Phức tạp + cần tham khảo lại?
              ├─ Có → TẠO SKILL
              └─ Không → KHÔNG TẠO GÌ
```

---

## 7. Default Config Templates

```json
// database.json
{ "DatabaseSettings": { "DBProvider": "mssql", "ConnectionString": "Server=localhost;Database={ProjectName}Db;..." } }

// security.json
{ "SecuritySettings": { "Key": "secret-key-32-chars-minimum!", "TokenExpirationInMinutes": 60 } }
```

**Admin seed:** `admin@root.com` / `123Pa$$word!`

---

## 8. Cách sử dụng

1. Copy folder `docs/` (chứa file này + tất cả BUILD_XX docs) vào project mới
2. Agent đọc file này → hiểu architecture + tech stack + conventions
3. Follow Build Phases tuần tự, đọc BUILD doc chi tiết cho mỗi bước
4. Tạo workflow/skill **chỉ khi** đáp ứng điều kiện ở Section 6
