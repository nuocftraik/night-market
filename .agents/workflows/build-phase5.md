---
description: "Phase 5: Data Integrity — Soft Delete, Auditing"
---

# Phase 5: Data Integrity Patterns

**Mục tiêu**: Soft delete + Audit trail hoàn chỉnh
**Prerequisites**: Phase 4 hoàn thành (Auth OK)

---

## Bước 19: Soft Delete

```
Đọc file: docs/BUILD_19_Soft_Delete.md
```

**Việc cần làm:**
- `ISoftDelete` Interface — Marker với `DeletedOn`, `DeletedBy` properties
- Update `AuditableEntity` — Implement `ISoftDelete`
- Global Query Filter — Tự động exclude deleted entities (`WHERE DeletedOn IS NULL`)
- `AppendGlobalQueryFilter` — Extension method apply filter cho interfaces
- `SaveChangesAsync` Enhancement — Convert `EntityState.Deleted → EntityState.Modified`
- Restore Methods — Restore deleted entities
- Soft Delete Specifications — `OnlyDeletedSpec`, `IncludeDeletedSpec`
- API Endpoints — Restore, permanent delete, get deleted

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: Delete entity → entity vẫn tồn tại trong DB với `DeletedOn` populated

---

## Bước 20: Auditing

```
Đọc file: docs/BUILD_20_Auditing.md
```

**Việc cần làm:**
- `Trail` Entity — Lưu audit logs trong database
- `TrailType` Enum — Type-safe audit types (Create, Update, Delete)
- `AuditTrail` Helper — Build audit trails từ `EntityEntry`
- Audit Interceptor — Tự động capture changes trong `SaveChangesAsync`
- Soft Delete Detection — Detect khi `DeletedOn` changed
- `IAuditService` — Query audit logs
- `GetMyAuditLogsRequest` — Current user audit logs endpoint
- `PersonalController` — Expose audit logs via API

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: Changes tracked trong Trails table khi CRUD entities

---

## ✅ Phase 5 Checkpoint

**Kiểm tra:**
- [ ] `dotnet build` — 0 errors
- [ ] Soft delete: entity không bị xóa thật, `DeletedOn` được set
- [ ] Audit trail: `Trails` table ghi nhận Create/Update/Delete
- [ ] `PersonalController` expose audit logs
- [ ] Thực hiện chỉnh sửa docs sau khi đã hoàn thiện phase này cho phù hợp (cập nhật docs nếu trong quá trình implement có thay đổi/tối ưu so với docs gốc).

**⏸️ DỪNG: Notify user review Phase 5 trước khi tiếp tục Phase 6 (`/build-phase6`)**
