# Auditing - Audit Trails và Change Tracking

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** [BUILD_19 - Soft Delete](BUILD_19_Soft_Delete.md) đã hoàn thành

Tài liệu này hướng dẫn xây dựng hệ thống **Auditing** - theo dõi tất cả thay đổi dữ liệu trong ứng dụng.

---

```yaml
---
ai_metadata:
  generated_by: "ai_assisted"
  reviewed_by: "vuongnv1206"
  last_updated: "2026-02-03"
  layer: "Domain + Infrastructure + Application"
  patterns_used:
    - "Audit Trail Pattern"
    - "Change Data Capture"
    - "Interceptor Pattern (SaveChangesAsync)"
    - "Event Sourcing (light)"
  dependencies:
    - "BUILD_05_Infrastructure_Layer"
    - "BUILD_09_Domain_Base_Entities"
    - "BUILD_12_Common_Services"
  - "BUILD_19_Soft_Delete"
  ai_instructions: |
  For Auditing implementation:
    - Trail entity in Domain layer
    - AuditTrail helper class in Infrastructure
  - Audit interceptor in BaseDbContext.SaveChangesAsync
    - Detect soft delete: DeletedOn changed from null → value
    - Serialize OldValues/NewValues as JSON
    - IAuditService for querying audit logs
---
```

---

## 1. Overview

**Làm gì:** Xây dựng audit trail system để tự động ghi lại tất cả thay đổi (Create, Update, Delete) của entities trong database.

**Tại sao cần:**
- **Compliance:** Đáp ứng yêu cầu tuân thủ (GDPR, SOX, HIPAA) - phải biết ai làm gì, khi nào
- **Security:** Phát hiện hành vi bất thường, điều tra security incidents
- **Debugging:** Debug production issues bằng cách xem lịch sử thay đổi
- **Business Intelligence:** Phân tích hành vi người dùng, cải thiện UX
- **Accountability:** Trách nhiệm giải trình - mỗi hành động có người chịu trách nhiệm
- **Regulatory Requirements:** Nhiều ngành (finance, healthcare) yêu cầu audit trails bắt buộc

**Trong bước này chúng ta sẽ:**
- ✅ Tạo `Trail` entity để lưu audit logs
- ✅ Tạo `TrailType` enum (Create, Update, Delete)
- ✅ Tạo `AuditTrail` helper class xử lý audit logic
- ✅ Implement audit interceptor trong `BaseDbContext.SaveChangesAsync()`
- ✅ Track soft delete events (DeletedOn changed from null → value)
- ✅ Tạo `IAuditService` và `AuditService` để query audit logs
- ✅ Tạo `GetMyAuditLogsRequest` query cho current user
- ✅ Expose audit logs qua `PersonalController`

**Real-world example:**
```csharp
// ===== SCENARIO 1: User updates profile =====
var user = await _userService.GetAsync(userId);
user.FirstName = "John Updated";
user.Email = "john.new@example.com";
await _context.SaveChangesAsync();

// Audit trail automatically created in database:
// {
//   "UserId": "user-123",
//   "Type": "Update",
//   "TableName": "ApplicationUser",
//   "DateTime": "2024-01-30T10:30:00Z",
//   "OldValues": { 
//     "FirstName": "John",
//   "Email": "john.old@example.com"
//   },
//   "NewValues": { 
//     "FirstName": "John Updated",
//     "Email": "john.new@example.com"
//   },
//   "AffectedColumns": ["FirstName", "Email"],
//   "PrimaryKey": "user-123"
// }

// ===== SCENARIO 2: User soft deletes product =====
var product = await _repository.GetByIdAsync(productId);
await _repository.DeleteAsync(product);
await _context.SaveChangesAsync();

// Audit trail detects soft delete:
// {
//   "UserId": "user-456",
//   "Type": "Delete",  // ← Detected from DeletedOn change!
//   "TableName": "Products",
//   "DateTime": "2024-01-30T11:00:00Z",
//   "OldValues": { 
//     "Name": "iPhone 15",
//     "DeletedOn": null,
//     "DeletedBy": null
//   },
//   "NewValues": { 
//     "DeletedOn": "2024-01-30T11:00:00Z",
//     "DeletedBy": "user-456"
//   },
//   "AffectedColumns": ["DeletedOn", "DeletedBy"],
//   "PrimaryKey": "product-789"
// }

// ===== SCENARIO 3: Query audit logs =====
// User views their audit history
var request = new GetMyAuditLogsRequest 
{ 
    PageNumber = 1, 
    PageSize = 20 
};
var auditLogs = await _auditService.GetMyAuditLogsAsync(request);

// Response:
// [
//   { "Type": "Update", "TableName": "ApplicationUser", "DateTime": "..." },
//   { "Type": "Create", "TableName": "Orders", "DateTime": "..." },
// { "Type": "Delete", "TableName": "Products", "DateTime": "..." }
// ]
```

---

## 2. Dependencies Check

### ✅ **Những gì ĐÃ CÓ từ BUILD_09 (Domain Base Entities):**

**IAuditableEntity interface:**
```csharp
public interface IAuditableEntity
{
    Guid CreatedBy { get; set; }
    DateTime CreatedOn { get; }
    Guid LastModifiedBy { get; set; }
    DateTime? LastModifiedOn { get; set; }
}
```

**AuditableEntity implements IAuditableEntity:**
```csharp
public abstract class AuditableEntity<T> : BaseEntity<T>, IAuditableEntity
{
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }
}
```

---

### ✅ **Những gì ĐÃ CÓ từ BUILD_19 (Soft Delete):**

**ISoftDelete interface:**
```csharp
public interface ISoftDelete
{
 DateTime? DeletedOn { get; set; }
  Guid? DeletedBy { get; set; }
}
```

**AuditableEntity implements ISoftDelete:**
```csharp
public abstract class AuditableEntity<T> : 
    BaseEntity<T>, 
    IAuditableEntity,  // BUILD_09
    ISoftDelete     // BUILD_19
{
    // IAuditableEntity properties
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }
    
    // ISoftDelete properties
    public DateTime? DeletedOn { get; set; }
    public Guid? DeletedBy { get; set; }
}
```

---

### ✅ **Những gì ĐÃ CÓ từ BUILD_12 (Common Services):**

**ISerializerService:**
```csharp
public interface ISerializerService : ITransientService
{
    string Serialize<T>(T obj);
    T Deserialize<T>(string payload);
}
```

---

### ✅ **Những gì ĐÃ CÓ từ BUILD_05 (Infrastructure Layer):**

**BaseDbContext với SaveChangesAsync:**
```csharp
public abstract class BaseDbContext : DbContext
{
    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // Handle audit trail và soft delete
        HandleAuditingBeforeSaveChanges();
        
     return await base.SaveChangesAsync(cancellationToken);
    }
}
```

---

✅ **Tất cả foundation đã có! BUILD_20 chỉ add audit trail tracking trên nền tảng này.**

---

## 3. Add Required Packages

**Không cần thêm packages mới** - sử dụng packages đã có:
- `Microsoft.EntityFrameworkCore` (đã có từ BUILD_05)
- `Newtonsoft.Json` (đã có từ BUILD_12 - ISerializerService)

⚠️ **Lưu ý:** Auditing hoàn toàn dựa trên EF Core ChangeTracker và serialization, không cần external packages.

---

## 4. Domain Layer - Trail Entity

### Bước 4.1: TrailType Enum

**Làm gì:** Tạo enum để phân loại audit trail types.

**Tại sao:** Type-safe, dễ query, clear semantics (Create/Update/Delete).

**File:** `src/Core/Domain/Auditing/TrailType.cs`

```csharp
namespace NightMarket.WebApi.Domain.Auditing;

/// <summary>
/// Type of audit trail entry.
/// Represents the type of change that occurred to an entity.
/// </summary>
public enum TrailType : byte
{
    /// <summary>
    /// Entity was created (INSERT operation)
    /// </summary>
    Create = 1,

    /// <summary>
    /// Entity was updated (UPDATE operation)
    /// </summary>
  Update = 2,

    /// <summary>
    /// Entity was deleted (DELETE or soft delete operation)
  /// </summary>
    Delete = 3
}
```

**Giải thích:**

**Why enum instead of string:**
```csharp
// ❌ BAD: String-based
public string Type { get; set; } = "Create"; 
// Typos, case sensitivity, no IntelliSense

// ✅ GOOD: Enum-based
public TrailType Type { get; set; } = TrailType.Create;
// Type-safe, IntelliSense, compiler checks
```

**Why byte:**
- Chỉ có 3 values (Create/Update/Delete)
- Tiết kiệm space (byte vs int)
- Database: TINYINT (1 byte) vs INT (4 bytes)

**Explicit values (1, 2, 3):**
- Rõ ràng trong database
- Dễ debug (không bị offset nếu thêm/xóa values)

---

### Bước 4.2: Trail Entity

**Làm gì:** Tạo entity để lưu audit logs trong database.

**Tại sao:** Persistent storage cho audit trails, có thể query, report, compliance.

**File:** `src/Core/Domain/Auditing/Trail.cs`

```csharp
using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Domain.Auditing;

/// <summary>
/// Audit trail entity - lưu trữ tất cả thay đổi trong hệ thống.
/// Mỗi record represent một operation (Create/Update/Delete) trên một entity.
/// </summary>
public class Trail : BaseEntity, IAggregateRoot
{
    /// <summary>
    /// User ID của người thực hiện action (from ICurrentUser)
/// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of operation (Create/Update/Delete)
    /// </summary>
    public TrailType Type { get; set; }

    /// <summary>
    /// Table name của entity bị modify (e.g., "Products", "ApplicationUser")
    /// </summary>
    public string TableName { get; set; } = default!;

    /// <summary>
    /// Thời điểm thay đổi (UTC)
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Old values (before change) - serialized as JSON
    /// NULL for Create operations
  /// </summary>
public string? OldValues { get; set; }

    /// <summary>
    /// New values (after change) - serialized as JSON
    /// NULL for Delete operations
  /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Affected columns (changed properties) - comma-separated
    /// e.g., "FirstName,Email,PhoneNumber"
    /// </summary>
    public string? AffectedColumns { get; set; }

    /// <summary>
    /// Primary key của entity bị modify - serialized as JSON
    /// Hỗ trợ composite keys: { "Id": "guid", "TenantId": "guid" }
    /// </summary>
    public string PrimaryKey { get; set; } = default!;

    /// <summary>
    /// Constructor - Initialize DateTime
    /// </summary>
    public Trail()
  {
        DateTime = DateTime.UtcNow;
    }
}
```

**Giải thích chi tiết:**

**1. Kế thừa BaseEntity (không phải AuditableEntity):**
```csharp
public class Trail : BaseEntity, IAggregateRoot
// NOT: public class Trail : AuditableEntity, IAggregateRoot
```

**Why:**
- Trail entity chính là audit record, không cần audit chính nó
- Tránh infinite loop: Audit the audit trail
- BaseEntity đủ (Id + DomainEvents)

---

**2. UserId - Guid:**
```csharp
public Guid UserId { get; set; }
```

**Why:**
- Track "ai" thực hiện thay đổi
- Consistent với ApplicationUser.Id
- Nullable không cần vì luôn có user (system/admin if no user)

---

**3. TableName - string:**
```csharp
public string TableName { get; set; } = default!;
```

**Why string instead of Type:**
- Simple, human-readable trong database
- Easy query: `WHERE TableName = 'Products'`
- No dependency on entity types (decoupled)

**Example values:**
```
"Products"
"ApplicationUser"
"Orders"
"OrderItems"
```

---

**4. DateTime - DateTime (UTC):**
```csharp
public DateTime DateTime { get; set; }
```

**Why:**
- Track "khi nào" thay đổi
- UTC for consistency across timezones
- Set in constructor for automatic timestamp

---

**5. OldValues / NewValues - string (JSON):**
```csharp
public string? OldValues { get; set; }
public string? NewValues { get; set; }
```

**Why JSON string:**
```csharp
// Flexible - store any entity properties
// Example OldValues:
// { "FirstName": "John", "Email": "john@old.com", "Age": 25 }

// Example NewValues:
// { "FirstName": "John Updated", "Email": "john@new.com", "Age": 26 }
```

**Nullable:**
- `OldValues` NULL for Create (no previous values)
- `NewValues` NULL for Delete (no new values)

---

**6. AffectedColumns - string (comma-separated):**
```csharp
public string? AffectedColumns { get; set; }
```

**Example:**
```csharp
// Update changed 3 properties:
AffectedColumns = "FirstName,Email,PhoneNumber"

// Benefits:
// - Quick filter: WHERE AffectedColumns LIKE '%Email%'
// - Human-readable
// - Space-efficient
```

---

**7. PrimaryKey - string (JSON for composite keys):**
```csharp
public string PrimaryKey { get; set; } = default!;
```

**Why JSON:**
```csharp
// Simple key:
PrimaryKey = "{ \"Id\": \"123e4567-...\" }"

// Composite key:
PrimaryKey = "{ \"OrderId\": \"...\", \"ProductId\": \"...\" }"
```

**Benefits:**
- Support composite keys
- Extensible
- Type-agnostic

---

**Database Schema:**
```sql
CREATE TABLE Trails (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Type TINYINT NOT NULL,
    TableName NVARCHAR(100) NOT NULL,
    DateTime DATETIME2 NOT NULL,
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    AffectedColumns NVARCHAR(MAX) NULL,
    PrimaryKey NVARCHAR(500) NOT NULL
);

-- Indexes for query performance
CREATE INDEX IX_Trails_UserId ON Trails(UserId);
CREATE INDEX IX_Trails_TableName ON Trails(TableName);
CREATE INDEX IX_Trails_DateTime ON Trails(DateTime);
CREATE INDEX IX_Trails_Type ON Trails(Type);
```

---

## 5. Infrastructure Layer - Audit Helper

### Bước 5.1: AuditTrail Helper Class

**Làm gì:** Tạo helper class để build audit trail entries từ EF Core EntityEntry.

**Tại sao:** Encapsulate complex logic, reusable, testable, separate concerns.

**File:** `src/Infrastructure/Infrastructure/Auditing/AuditTrail.cs`

```csharp
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Domain.Auditing;
using NightMarket.WebApi.Domain.Common.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NightMarket.WebApi.Infrastructure.Auditing;

/// <summary>
/// Helper class để build audit trail entries từ EF Core EntityEntry
/// </summary>
public static class AuditTrail
{
    /// <summary>
    /// Transform EF Core EntityEntry thành Trail entity.
    /// Captures old values, new values, affected columns, và detects operation type.
    /// </summary>
    /// <param name="entry">EF Core EntityEntry từ ChangeTracker</param>
    /// <param name="userId">Current user ID thực hiện action</param>
    /// <param name="serializer">JSON serializer service</param>
    /// <returns>Trail entity ready to save</returns>
    public static Trail? TransformEntry(
        EntityEntry entry,
        Guid userId,
        ISerializerService serializer)
    {
        // Skip nếu không phải IAuditableEntity
        if (entry.Entity is not IAuditableEntity)
       return null;

  var trail = new Trail
        {
            TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
  UserId = userId,
   DateTime = DateTime.UtcNow
        };

        // Get modified properties (chỉ lấy properties thực sự changed)
        var modifiedProperties = entry.Properties
            .Where(p => p.IsModified || 
      entry.State == EntityState.Added || 
       entry.State == EntityState.Deleted)
         .ToList();

        // Build OldValues, NewValues, AffectedColumns
  var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        var affectedColumns = new List<string>();

        foreach (var property in modifiedProperties)
        {
       var propertyName = property.Metadata.Name;

       // Skip navigation properties và shadow properties
     if (property.Metadata.IsForeignKey() || 
            property.Metadata.IsShadowProperty())
     continue;

      switch (entry.State)
            {
                case EntityState.Added:
        // Create: Chỉ có NewValues
           newValues[propertyName] = property.CurrentValue;
      affectedColumns.Add(propertyName);
     trail.Type = TrailType.Create;
   break;

  case EntityState.Modified:
  // ⭐ SOFT DELETE DETECTION ⭐
        // Detect khi DeletedOn changed from null → value
     if (property.IsModified && 
     entry.Entity is ISoftDelete && 
             propertyName == nameof(ISoftDelete.DeletedOn) &&
       property.OriginalValue == null && 
           property.CurrentValue != null)
  {
      // This is a soft delete, not an update!
          trail.Type = TrailType.Delete;
       
      // Log as delete operation
        oldValues[propertyName] = property.OriginalValue;
        newValues[propertyName] = property.CurrentValue;
        affectedColumns.Add(propertyName);
      affectedColumns.Add(nameof(ISoftDelete.DeletedBy));
     break;
       }

  // Regular update: Log old and new values
     if (property.IsModified)
 {
              oldValues[propertyName] = property.OriginalValue;
         newValues[propertyName] = property.CurrentValue;
     affectedColumns.Add(propertyName);
      }
           
         trail.Type = TrailType.Update;
          break;

        case EntityState.Deleted:
           // Physical delete (rare): Chỉ có OldValues
           oldValues[propertyName] = property.OriginalValue;
     affectedColumns.Add(propertyName);
            trail.Type = TrailType.Delete;
   break;
            }
        }

     // Serialize values to JSON
        trail.OldValues = oldValues.Count > 0 
            ? serializer.Serialize(oldValues) 
       : null;
   
        trail.NewValues = newValues.Count > 0 
    ? serializer.Serialize(newValues) 
 : null;
     
  trail.AffectedColumns = affectedColumns.Count > 0 
      ? string.Join(",", affectedColumns) 
    : null;

     // Build PrimaryKey JSON
        var keyValues = new Dictionary<string, object?>();
        foreach (var keyProperty in entry.Properties.Where(p => p.Metadata.IsPrimaryKey()))
        {
            keyValues[keyProperty.Metadata.Name] = keyProperty.CurrentValue;
        }
        trail.PrimaryKey = serializer.Serialize(keyValues);

        return trail;
    }
}
```

**Giải thích chi tiết:**

**1. Static helper pattern:**
```csharp
public static class AuditTrail
{
    public static Trail? TransformEntry(...)
}
```

**Why static:**
- No state, pure function
- Easy to use: `AuditTrail.TransformEntry(...)`
- Testable without DI

---

**2. Skip non-auditable entities:**
```csharp
if (entry.Entity is not IAuditableEntity)
    return null;
```

**Why:**
- Chỉ audit entities có IAuditableEntity (have Created/Modified tracking)
- Trail entity itself is NOT auditable (avoid infinite loop)

---

**3. Get table name:**
```csharp
TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name
```

**Fallback logic:**
- Try EF Core table name first
- Fallback to C# class name if no table mapping

---

**4. Filter modified properties:**
```csharp
var modifiedProperties = entry.Properties
    .Where(p => p.IsModified || 
    entry.State == EntityState.Added || 
       entry.State == EntityState.Deleted)
    .ToList();
```

**Why filter:**
- Only log properties that actually changed
- Skip unchanged properties (reduce noise)
- Include all properties for Add/Delete

---

**5. Skip navigation and shadow properties:**
```csharp
if (property.Metadata.IsForeignKey() || 
    property.Metadata.IsShadowProperty())
    continue;
```

**Why skip:**
- Foreign keys: Redundant (captured in entity itself)
- Shadow properties: Internal EF Core properties

---

**6. ⭐ SOFT DELETE DETECTION ⭐:**
```csharp
if (property.IsModified && 
    entry.Entity is ISoftDelete && 
    propertyName == nameof(ISoftDelete.DeletedOn) &&
    property.OriginalValue == null && 
  property.CurrentValue != null)
{
    trail.Type = TrailType.Delete;
}
```

**Critical logic:**
- Detect when `DeletedOn` changes from `null` → `DateTime`
- This is a soft delete, not regular update!
- Log as `TrailType.Delete` (not Update)

**Example:**
```csharp
// User calls: DeleteAsync(product)
// EF Core: State = Modified (because of soft delete interception)
// DeletedOn: null → '2024-01-30T10:00:00Z'
// AuditTrail detects: This is a DELETE operation!
// Result: Trail.Type = TrailType.Delete ✅
```

---

**7. Serialize to JSON:**
```csharp
trail.OldValues = oldValues.Count > 0 
    ? serializer.Serialize(oldValues) 
    : null;
```

**Result:**
```json
{
  "FirstName": "John",
  "Email": "john@old.com",
  "DeletedOn": null
}
```

---

**8. Primary key serialization:**
```csharp
foreach (var keyProperty in entry.Properties.Where(p => p.Metadata.IsPrimaryKey()))
{
    keyValues[keyProperty.Metadata.Name] = keyProperty.CurrentValue;
}
trail.PrimaryKey = serializer.Serialize(keyValues);
```

**Supports composite keys:**
```json
// Simple key:
{ "Id": "123e4567-..." }

// Composite key:
{ "OrderId": "...", "ProductId": "..." }
```

---

## 6. Infrastructure Layer - Audit Interceptor

### Bước 6.1: Update BaseDbContext.SaveChangesAsync

**Làm gì:** Add audit trail capturing logic trong SaveChangesAsync.

**Tại sao:** Intercept tất cả database changes, automatic audit logging, transparent to application code.

**File:** `src/Infrastructure/Infrastructure/Persistence/Context/BaseDbContext.cs`

```csharp
using NightMarket.WebApi.Application.Common.Events;
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Domain.Auditing;
using NightMarket.WebApi.Domain.Common.Contracts;
using NightMarket.WebApi.Infrastructure.Auditing;
using NightMarket.WebApi.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Persistence.Context;

/// <summary>
/// Base DbContext với audit trail, soft delete, và domain events support
/// </summary>
public abstract class BaseDbContext : DbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly ISerializerService _serializer;
    private readonly IEventPublisher _events;

    protected BaseDbContext(
        DbContextOptions options,
        ICurrentUser currentUser,
        ISerializerService serializer,
        IEventPublisher events)
        : base(options)
    {
        _currentUser = currentUser;
        _serializer = serializer;
        _events = events;
    }

    // ⭐ NEW: DbSet for audit trails
    public DbSet<Trail> AuditTrails => Set<Trail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    // Soft delete global query filter (from BUILD_22)
   modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(e => e.DeletedOn == null);
 
    // ⭐ NEW: Configure Trail entity
        modelBuilder.Entity<Trail>(entity =>
     {
        entity.ToTable("AuditTrails", "Auditing");
            
       entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.TableName);
        entity.HasIndex(e => e.DateTime);
   entity.HasIndex(e => e.Type);
            
        entity.Property(e => e.TableName).HasMaxLength(100).IsRequired();
     entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
        entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
        entity.Property(e => e.AffectedColumns).HasColumnType("nvarchar(max)");
     entity.Property(e => e.PrimaryKey).HasMaxLength(500).IsRequired();
        });
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // ⭐ STEP 1: Capture audit trails BEFORE SaveChanges
        var auditEntries = CaptureAuditTrails();

        // STEP 2: Handle auditing (Created/Modified/Deleted tracking)
        HandleAuditingBeforeSaveChanges();

    // STEP 3: Publish domain events
        await PublishDomainEventsAsync(cancellationToken);

        // STEP 4: Save changes to database
        var result = await base.SaveChangesAsync(cancellationToken);

        // ⭐ STEP 5: Save audit trails (after successful save)
        if (auditEntries.Count > 0)
        {
   await AuditTrails.AddRangeAsync(auditEntries, cancellationToken);
     await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

  /// <summary>
    /// ⭐ NEW: Capture audit trails from ChangeTracker
    /// </summary>
    private List<Trail> CaptureAuditTrails()
{
        var userId = _currentUser.GetUserId();
        var auditEntries = new List<Trail>();

        // Get all entries that are Added, Modified, or Deleted
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>()
       .Where(e => e.State == EntityState.Added || 
   e.State == EntityState.Modified || 
           e.State == EntityState.Deleted)
   .ToList())
        {
            // Use AuditTrail helper to transform entry
            var trail = AuditTrail.TransformEntry(entry, userId, _serializer);
        
            if (trail != null)
            {
              auditEntries.Add(trail);
            }
        }

        return auditEntries;
    }

    /// <summary>
 /// Handle auditing (Created/Modified/Deleted tracking) và soft delete
    /// FROM BUILD_22 + BUILD_09
    /// </summary>
    private void HandleAuditingBeforeSaveChanges()
    {
        var userId = _currentUser.GetUserId();

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>().ToList())
   {
switch (entry.State)
      {
        case EntityState.Added:
           entry.Entity.CreatedBy = userId;
          entry.Entity.CreatedOn = DateTime.UtcNow;
        break;

   case EntityState.Modified:
  entry.Entity.LastModifiedBy = userId;
   entry.Entity.LastModifiedOn = DateTime.UtcNow;
   break;

  case EntityState.Deleted:
          // Soft delete logic (from BUILD_22)
       if (entry.Entity is ISoftDelete softDelete)
       {
        softDelete.DeletedOn = DateTime.UtcNow;
          softDelete.DeletedBy = userId;
  entry.State = EntityState.Modified;
     }
        break;
  }
   }
    }

  /// <summary>
    /// Publish domain events qua MediatR
    /// FROM BUILD_09
    /// </summary>
    private async Task PublishDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = ChangeTracker
            .Entries<IEntity>()
   .Where(e => e.Entity.DomainEvents.Any())
     .ToList();

      var domainEvents = entitiesWithEvents
 .SelectMany(e => e.Entity.DomainEvents)
     .ToList();

        entitiesWithEvents.ForEach(e => e.Entity.DomainEvents.Clear());

        foreach (var domainEvent in domainEvents)
        {
         await _events.PublishAsync(domainEvent);
        }
    }
}
```

**Giải thích chi tiết:**

**1. DbSet for audit trails:**
```csharp
public DbSet<Trail> AuditTrails => Set<Trail>();
```

**Why public:**
- Allow querying audit trails: `_context.AuditTrails.Where(...)`
- IAuditService needs access

---

**2. Entity configuration:**
```csharp
modelBuilder.Entity<Trail>(entity =>
{
    entity.ToTable("AuditTrails", "Auditing");
    // ...
});
```

**Schema:**
- Table: `Auditing.AuditTrails` (separate schema)
- Indexes: UserId, TableName, DateTime, Type (for query performance)
- Column types: nvarchar(max) for JSON, nvarchar(100) for TableName

---

**3. SaveChangesAsync flow:**
```csharp
// STEP 1: Capture audit trails BEFORE SaveChanges
var auditEntries = CaptureAuditTrails();

// STEP 2: Handle auditing (set Created/Modified/Deleted fields)
HandleAuditingBeforeSaveChanges();

// STEP 3: Publish domain events
await PublishDomainEventsAsync(cancellationToken);

// STEP 4: Save changes to database
var result = await base.SaveChangesAsync(cancellationToken);

// STEP 5: Save audit trails (after successful save)
await AuditTrails.AddRangeAsync(auditEntries, cancellationToken);
await base.SaveChangesAsync(cancellationToken);
```

**Why this order:**

**BEFORE SaveChanges (Step 1):**
- Capture audit entries while ChangeTracker has full information
- After SaveChanges, ChangeTracker is cleared

**AFTER SaveChanges (Step 5):**
- Only save audit trails if main transaction succeeds
- Avoid audit logs for failed transactions
- Separate SaveChanges call for audit trails

---

**4. CaptureAuditTrails method:**
```csharp
private List<Trail> CaptureAuditTrails()
{
    var userId = _currentUser.GetUserId();
    var auditEntries = new List<Trail>();

    foreach (var entry in ChangeTracker.Entries<IAuditableEntity>()
 .Where(e => e.State == EntityState.Added || 
     e.State == EntityState.Modified || 
        e.State == EntityState.Deleted)
    .ToList())
    {
        var trail = AuditTrail.TransformEntry(entry, userId, _serializer);
    
        if (trail != null)
        {
 auditEntries.Add(trail);
     }
    }

    return auditEntries;
}
```

**Logic:**
- Query ChangeTracker for Added/Modified/Deleted entries
- Use `AuditTrail.TransformEntry` helper
- Filter `IAuditableEntity` only
- Return list of Trail entities ready to save

---

**Benefits:**
- ✅ **Automatic** - No manual audit code needed
- ✅ **Transparent** - Application code doesn't know about auditing
- ✅ **Consistent** - All changes audited same way
- ✅ **Soft delete aware** - Detects soft delete as Delete event
- ✅ **Transactional** - Audit trails saved with main transaction

---

## 7. Application Layer - Audit Service

### Bước 7.1: IAuditService Interface

**Làm gì:** Tạo service interface để query audit logs.

**Tại sao:** Abstraction, testable, follow Clean Architecture.

**File:** `src/Core/Application/Auditing/IAuditService.cs`

```csharp
using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Auditing;

/// <summary>
/// Service interface for querying audit trails
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Get audit logs for current user (my audit history)
    /// </summary>
    Task<PaginationResponse<AuditDto>> GetMyAuditLogsAsync(
        GetMyAuditLogsRequest request, 
    CancellationToken cancellationToken = default);
}
```

---

### Bước 7.2: AuditDto

**Làm gì:** Tạo DTO để return audit log data.

**Tại sao:** Không expose entity trực tiếp, control data shape, versioning.

**File:** `src/Core/Application/Auditing/AuditDto.cs`

```csharp
using NightMarket.WebApi.Domain.Auditing;

namespace NightMarket.WebApi.Application.Auditing;

/// <summary>
/// DTO for audit trail entry
/// </summary>
public class AuditDto
{
    /// <summary>
    /// Audit trail ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID performed the action
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of operation (Create/Update/Delete)
    /// </summary>
    public string Type { get; set; } = default!;

    /// <summary>
    /// Table name (entity type)
    /// </summary>
    public string TableName { get; set; } = default!;

    /// <summary>
    /// When the change occurred (UTC)
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Old values (JSON string)
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values (JSON string)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Affected columns (comma-separated)
    /// </summary>
    public string? AffectedColumns { get; set; }

    /// <summary>
    /// Primary key (JSON string)
    /// </summary>
    public string PrimaryKey { get; set; } = default!;
}
```

**Mapping from Trail entity:**
```csharp
// Using Mapster (auto-mapping)
var dto = trail.Adapt<AuditDto>();

// Type enum → string
dto.Type = trail.Type.ToString(); // "Create", "Update", "Delete"
```

---

### Bước 7.3: GetMyAuditLogsRequest

**Làm gì:** Tạo request DTO với pagination.

**Tại sao:** Audit logs có thể rất nhiều, cần pagination.

**File:** `src/Core/Application/Auditing/GetMyAuditLogsRequest.cs`

```csharp
using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Auditing;

/// <summary>
/// Request to get current user's audit logs
/// </summary>
public class GetMyAuditLogsRequest : PaginationFilter
{
    /// <summary>
 /// Filter by table name (optional)
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
  /// Filter by trail type (optional)
 /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Filter by date from (optional)
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter by date to (optional)
    /// </summary>
    public DateTime? ToDate { get; set; }
}
```

**PaginationFilter base class (from BUILD_11):**
```csharp
public class PaginationFilter
{
    public int PageNumber { get; set; } = 1;
 public int PageSize { get; set; } = 10;
}
```

---

### Bước 7.4: AuditService Implementation

**Làm gì:** Implement service để query audit logs từ database.

**Tại sao:** Business logic for querying, filtering, pagination.

**File:** `src/Infrastructure/Infrastructure/Auditing/AuditService.cs`

```csharp
using NightMarket.WebApi.Application.Auditing;
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Application.Common.Models;
using NightMarket.WebApi.Domain.Auditing;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Auditing;

/// <summary>
/// Service implementation for querying audit trails
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

  public AuditService(
      ApplicationDbContext context,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get audit logs for current user with filters and pagination
    /// </summary>
    public async Task<PaginationResponse<AuditDto>> GetMyAuditLogsAsync(
        GetMyAuditLogsRequest request,
    CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetUserId();

        // Build query
        var query = _context.AuditTrails
       .Where(a => a.UserId == userId)
            .AsQueryable();

    // Apply filters
        if (!string.IsNullOrWhiteSpace(request.TableName))
        {
        query = query.Where(a => a.TableName == request.TableName);
        }

     if (!string.IsNullOrWhiteSpace(request.Type))
        {
      if (Enum.TryParse<TrailType>(request.Type, out var trailType))
   {
           query = query.Where(a => a.Type == trailType);
            }
 }

        if (request.FromDate.HasValue)
        {
query = query.Where(a => a.DateTime >= request.FromDate.Value);
     }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.DateTime <= request.ToDate.Value);
        }

    // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination and ordering
        var auditLogs = await query
 .OrderByDescending(a => a.DateTime)  // Latest first
            .Skip((request.PageNumber - 1) * request.PageSize)
        .Take(request.PageSize)
      .ToListAsync(cancellationToken);

        // Map to DTOs
        var dtos = auditLogs.Adapt<List<AuditDto>>();

        return new PaginationResponse<AuditDto>(
         dtos,
  totalCount,
     request.PageNumber,
request.PageSize);
    }
}
```

**Giải thích:**

**1. Filter by current user:**
```csharp
var query = _context.AuditTrails
  .Where(a => a.UserId == userId);
```

**Security:**
- Users chỉ xem audit logs của chính mình
- Admin có thể implement `GetAllAuditLogsAsync` để xem tất cả

---

**2. Optional filters:**
```csharp
if (!string.IsNullOrWhiteSpace(request.TableName))
{
    query = query.Where(a => a.TableName == request.TableName);
}
```

**Flexibility:**
- Filter by table (e.g., only "Products" changes)
- Filter by type (e.g., only "Delete" operations)
- Filter by date range

---

**3. Pagination:**
```csharp
// Always paginate
var logs = await _context.AuditTrails
    .Where(a => a.UserId == userId)
    .OrderByDescending(a => a.DateTime)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

**Example:**
```
PageNumber = 1, PageSize = 10: Skip 0, Take 10 (records 1-10)
PageNumber = 2, PageSize = 10: Skip 10, Take 10 (records 11-20)
```

---

**4. Order by DateTime descending:**
```csharp
.OrderByDescending(a => a.DateTime)
```

**Why:**
- Show latest changes first
- Most relevant for users

---

## 8. Host Layer - Personal Controller

### Bước 8.1: Add Audit Logs Endpoint

**Làm gì:** Expose audit logs qua REST API trong PersonalController.

**Tại sao:** Users có thể xem lịch sử thay đổi của chính họ.

**File:** `src/Host/Host/Controllers/Identity/PersonalController.cs` (UPDATE)

```csharp
using NightMarket.WebApi.Application.Auditing;
using NightMarket.WebApi.Application.Common.Models;
using NightMarket.WebApi.Infrastructure.Auth.Permissions;
using NightMarket.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NightMarket.WebApi.Host.Controllers.Identity;

/// <summary>
/// Controller for current user's personal information and audit logs
/// </summary>
[Route("api/personal")]
[Authorize]
public class PersonalController : BaseApiController
{
    // ... existing endpoints (GetProfile, UpdateProfile, ChangePassword) ...

    /// <summary>
/// Get current user's audit logs (change history)
/// </summary>
/// <param name="request">Filter và pagination parameters</param>
/// <param name="auditService">Audit service dependency</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Paginated list of audit logs</returns>
    [HttpGet("audit-logs")]
    [MustHavePermission(AppAction.View, AppFunction.Users)]
    public async Task<ActionResult<PaginationResponse<AuditDto>>> GetMyAuditLogs(
        [FromQuery] GetMyAuditLogsRequest request,
     [FromServices] IAuditService auditService,
        CancellationToken cancellationToken)
    {
   var result = await auditService.GetMyAuditLogsAsync(request, cancellationToken);
        return Ok(result);
    }
}
```

**API Examples:**

```bash
# Get first page of audit logs
GET /api/personal/audit-logs?PageNumber=1&PageSize=20

# Filter by table name
GET /api/personal/audit-logs?TableName=ApplicationUser&PageNumber=1&PageSize=10

# Filter by type (only deletes)
GET /api/personal/audit-logs?Type=Delete&PageNumber=1&PageSize=10

# Filter by date range
GET /api/personal/audit-logs?FromDate=2024-01-01&ToDate=2024-01-31&PageNumber=1&PageSize=10
```

**Expected Response:**
```json
{
  "data": [
    {
      "id": "trail-123",
      "userId": "user-456",
      "type": "Update",
      "tableName": "ApplicationUser",
      "dateTime": "2024-01-30T10:30:00Z",
   "oldValues": "{\"FirstName\":\"John\",\"Email\":\"john@old.com\"}",
    "newValues": "{\"FirstName\":\"John Updated\",\"Email\":\"john@new.com\"}",
    "affectedColumns": "FirstName,Email",
      "primaryKey": "{\"Id\":\"user-456\"}"
    },
    {
      "id": "trail-789",
      "userId": "user-456",
  "type": "Delete",
      "tableName": "Products",
"dateTime": "2024-01-30T09:15:00Z",
    "oldValues": "{\"Name\":\"iPhone 15\",\"DeletedOn\":null}",
      "newValues": "{\"DeletedOn\":\"2024-01-30T09:15:00Z\",\"DeletedBy\":\"user-456\"}",
   "affectedColumns": "DeletedOn,DeletedBy",
      "primaryKey": "{\"Id\":\"product-789\"}"
    }
  ],
  "currentPage": 1,
  "totalPages": 5,
  "totalCount": 87,
  "pageSize": 20,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

---

## 9. Database Migration

### Bước 9.1: Add Migration

**Commands:**

```powershell
# Navigate to Migrators.MSSQL project
cd src/Migrators/Migrators.MSSQL

# Add migration
dotnet ef migrations add Add_AuditTrails_Table `
    --startup-project ../../Host/Host `
    --context ApplicationDbContext `
    --output-dir Migrations

# Apply migration
dotnet ef database update `
    --startup-project ../../Host/Host `
    --context ApplicationDbContext
```

**Generated Migration:**

```csharp
public partial class Add_AuditTrails_Table : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create Auditing schema
        migrationBuilder.EnsureSchema(name: "Auditing");

        // Create AuditTrails table
     migrationBuilder.CreateTable(
name: "AuditTrails",
            schema: "Auditing",
    columns: table => new
            {
    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
         Type = table.Column<byte>(type: "tinyint", nullable: false),
    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
      OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
         NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
      AffectedColumns = table.Column<string>(type: "nvarchar(max)", nullable: true),
 PrimaryKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
 },
       constraints: table =>
            {
    table.PrimaryKey("PK_AuditTrails", x => x.Id);
});

  // Create indexes for query performance
        migrationBuilder.CreateIndex(
   name: "IX_AuditTrails_UserId",
     schema: "Auditing",
          table: "AuditTrails",
  column: "UserId");

        migrationBuilder.CreateIndex(
     name: "IX_AuditTrails_TableName",
    schema: "Auditing",
            table: "AuditTrails",
       column: "TableName");

   migrationBuilder.CreateIndex(
   name: "IX_AuditTrails_DateTime",
  schema: "Auditing",
  table: "AuditTrails",
            column: "DateTime");

 migrationBuilder.CreateIndex(
            name: "IX_AuditTrails_Type",
schema: "Auditing",
          table: "AuditTrails",
            column: "Type");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
   migrationBuilder.DropTable(
            name: "AuditTrails",
   schema: "Auditing");
    }
}
```

---

## 10. Complete Usage Examples

### Bước 10.1: Example 1 - User Updates Profile

**Scenario:** User updates their profile information

```csharp
// User calls API: PUT /api/personal/profile
var request = new UpdateProfileRequest
{
FirstName = "John Updated",
    Email = "john.new@example.com",
    PhoneNumber = "+84123456789"
};

// In UserService
var user = await _userManager.FindByIdAsync(userId);
user.FirstName = request.FirstName;
user.Email = request.Email;
user.PhoneNumber = request.PhoneNumber;

await _context.SaveChangesAsync();

// ⭐ Audit trail automatically created:
// INSERT INTO Auditing.AuditTrails VALUES (
//   Id = 'new-guid',
//   UserId = 'user-123',
//   Type = 2,  -- Update
//   TableName = 'ApplicationUser',
//   DateTime = '2024-01-30T10:30:00Z',
//   OldValues = '{"FirstName":"John","Email":"john@old.com","PhoneNumber":"+84987654321"}',
// NewValues = '{"FirstName":"John Updated","Email":"john.new@example.com","PhoneNumber":"+84123456789"}',
//   AffectedColumns = 'FirstName,Email,PhoneNumber',
//   PrimaryKey = '{"Id":"user-123"}'
// )
```

---

### Bước 10.2: Example 2 - User Soft Deletes Product

**Scenario:** User deletes a product (soft delete from BUILD_22)

```csharp
// User calls API: DELETE /api/catalog/products/{id}
await _repository.DeleteAsync(product);
await _context.SaveChangesAsync();

// ⭐ What happens:

// 1. Soft delete interceptor (BUILD_22):
//    - Set DeletedOn = DateTime.UtcNow
//    - Set DeletedBy = userId
// - Change State to Modified

// 2. Audit trail interceptor (BUILD_23):
//    - Detect DeletedOn changed from null → value
//    - Log as TrailType.Delete (not Update!)

// Result in database:
// INSERT INTO Auditing.AuditTrails VALUES (
//   Id = 'new-guid',
//   UserId = 'user-456',
//   Type = 3,  -- Delete ✅ (detected!)
//   TableName = 'Products',
//   DateTime = '2024-01-30T11:00:00Z',
//   OldValues = '{"Name":"iPhone 15","Price":999,"DeletedOn":null,"DeletedBy":null}',
//   NewValues = '{"DeletedOn":"2024-01-30T11:00:00Z","DeletedBy":"user-456"}',
//   AffectedColumns = 'DeletedOn,DeletedBy',
//   PrimaryKey = '{"Id":"product-789"}'
// )
```

---

### Bước 10.3: Example 3 - Query Audit Logs

**Scenario:** User views their change history

```csharp
// User calls API: GET /api/personal/audit-logs?PageNumber=1&PageSize=10
var request = new GetMyAuditLogsRequest
{
    PageNumber = 1,
    PageSize = 10
};

var result = await _auditService.GetMyAuditLogsAsync(request, ct);

// Response:
// {
//   "data": [
//     {
//       "id": "trail-1",
//       "type": "Update",
//"tableName": "ApplicationUser",
//       "dateTime": "2024-01-30T10:30:00Z",
//       "affectedColumns": "FirstName,Email"
//     },
//     {
//    "id": "trail-2",
//       "type": "Delete",
//       "tableName": "Products",
//       "dateTime": "2024-01-30T09:15:00Z",
//       "affectedColumns": "DeletedOn,DeletedBy"
//     }
//   ],
//   "totalCount": 87,
//   "currentPage": 1,
//   "totalPages": 9,
//   "pageSize": 10
// }
```

---

## 11. Best Practices

### ✅ DO

**1. Always audit business-critical entities:**
```csharp
// ✅ GOOD: AuditableEntity cho business entities
public class User : AuditableEntity, IAggregateRoot { }
public class Order : AuditableEntity, IAggregateRoot { }
public class Payment : AuditableEntity, IAggregateRoot { }
```

**2. Use separate schema for audit trails:**
```csharp
// ✅ GOOD: Separate schema
entity.ToTable("AuditTrails", "Auditing");

// Benefits:
// - Logical separation
// - Easier backup/restore
// - Different retention policies
```

**3. Add indexes for common queries:**
```csharp
// ✅ GOOD: Indexes for performance
entity.HasIndex(e => e.UserId);
entity.HasIndex(e => e.TableName);
entity.HasIndex(e => e.DateTime);
entity.HasIndex(e => e.Type);
```

**4. Archive old audit logs:**
```csharp
// ✅ GOOD: Retention policy (e.g., keep 1 year)
// Background job to archive old records
public async Task ArchiveOldAuditLogsAsync()
{
    var cutoffDate = DateTime.UtcNow.AddYears(-1);
    
    var oldLogs = await _context.AuditTrails
        .Where(a => a.DateTime < cutoffDate)
 .ToListAsync();
    
    // Archive to cold storage (Azure Blob, S3, etc.)
    await _archiveService.ArchiveAsync(oldLogs);
    
    // Delete from database
    _context.AuditTrails.RemoveRange(oldLogs);
    await _context.SaveChangesAsync();
}
```

**5. Provide filtering for audit logs:**
```csharp
// ✅ GOOD: Multiple filters
public class GetMyAuditLogsRequest
{
    public string? TableName { get; set; }  // Filter by entity
    public string? Type { get; set; }        // Filter by operation
    public DateTime? FromDate { get; set; }  // Filter by date range
    public DateTime? ToDate { get; set; }
}
```

---

### ❌ DON'T

**1. Don't audit the audit trails:**
```csharp
// ❌ BAD: Trail entity with AuditableEntity
public class Trail : AuditableEntity, IAggregateRoot { }
// Creates infinite loop!

// ✅ GOOD: Trail entity with BaseEntity
public class Trail : BaseEntity, IAggregateRoot { }
```

**2. Don't store sensitive data in audit logs:**
```csharp
// ❌ BAD: Log passwords
public class User
{
 public string Password { get; set; }  // Will be logged!
}

// ✅ GOOD: Exclude sensitive properties
var trail = AuditTrail.TransformEntry(entry, userId, serializer);

// In TransformEntry, skip sensitive properties:
if (propertyName == "Password" || 
    propertyName == "PasswordHash" ||
    propertyName == "SecurityStamp")
    continue;  // Don't log!
```

**3. Don't query all audit logs without pagination:**
```csharp
// ❌ BAD: Load all audit logs
var allLogs = await _context.AuditTrails.ToListAsync();
// Millions of records = OutOfMemoryException

// ✅ GOOD: Always use pagination
var logs = await _context.AuditTrails
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

**4. Don't forget indexes:**
```csharp
// ❌ BAD: No indexes on AuditTrails table
// Query slow: SELECT * FROM AuditTrails WHERE UserId = '...'

// ✅ GOOD: Add indexes for common queries
entity.HasIndex(e => e.UserId);
entity.HasIndex(e => e.DateTime);
```

---

## 12. Troubleshooting

### Issue 1: Audit trails not created

**Problem:**
```csharp
// User updates entity
user.FirstName = "Updated";
await _context.SaveChangesAsync();

// No audit trail in database!
```

**Solutions:**

**1. Check entity implements IAuditableEntity:**
```csharp
// ✅ MUST implement IAuditableEntity
public class User : AuditableEntity, IAggregateRoot { }

// ❌ WITHOUT IAuditableEntity, no audit
public class User : BaseEntity, IAggregateRoot { }
```

**2. Check CaptureAuditTrails is called:**
```csharp
public override async Task<int> SaveChangesAsync(...)
{
    // ⚠️ MUST call before base.SaveChangesAsync()
    var auditEntries = CaptureAuditTrails();
    
    var result = await base.SaveChangesAsync(cancellationToken);
    
    // Save audit trails after
    await AuditTrails.AddRangeAsync(auditEntries, cancellationToken);
    await base.SaveChangesAsync(cancellationToken);
    
    return result;
}
```

---

### Issue 2: Soft delete not detected as Delete

**Problem:**
```csharp
// User soft deletes product
await _repository.DeleteAsync(product);
await _context.SaveChangesAsync();

// Audit trail shows Type = "Update" instead of "Delete"
```

**Solution:**

Check soft delete detection logic:
```csharp
// ⭐ MUST have this check in AuditTrail.TransformEntry
if (property.IsModified && 
 entry.Entity is ISoftDelete && 
    propertyName == nameof(ISoftDelete.DeletedOn) &&
    property.OriginalValue == null &&  // ⚠️ NULL before
    property.CurrentValue != null)     // ⚠️ NOT NULL after
{
    trail.Type = TrailType.Delete;  // ✅ Log as Delete
}
```

---

### Issue 3: Performance issues with large audit logs

**Problem:**
```csharp
// Query audit logs slow
var logs = await _context.AuditTrails
    .Where(a => a.UserId == userId)
    .ToListAsync();
// Takes 10+ seconds with millions of records
```

**Solutions:**

**1. Add indexes:**
```sql
CREATE INDEX IX_AuditTrails_UserId ON Auditing.AuditTrails(UserId);
CREATE INDEX IX_AuditTrails_DateTime ON Auditing.AuditTrails(DateTime);
```

**2. Use pagination:**
```csharp
// Always paginate
var logs = await _context.AuditTrails
    .Where(a => a.UserId == userId)
    .OrderByDescending(a => a.DateTime)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

**3. Archive old logs:**
```csharp
// Retention policy: Keep 1 year, archive older
var cutoffDate = DateTime.UtcNow.AddYears(-1);
var oldLogs = await _context.AuditTrails
    .Where(a => a.DateTime < cutoffDate)
    .ToListAsync();

// Move to cold storage
await _archiveService.ArchiveAsync(oldLogs);
_context.AuditTrails.RemoveRange(oldLogs);
await _context.SaveChangesAsync();
```

---

## 13. Summary

### ✅ Đã hoàn thành trong bước này:

**Domain Layer:**
- ✅ TrailType enum (Create, Update, Delete)
- ✅ Trail entity (audit log storage)

**Infrastructure Layer:**
- ✅ AuditTrail helper class (transform EntityEntry → Trail)
- ✅ Audit interceptor trong BaseDbContext.SaveChangesAsync
- ✅ Soft delete detection (DeletedOn changed from null → value)
- ✅ AuditService implementation (query audit logs)

**Application Layer:**
- ✅ IAuditService interface
- ✅ AuditDto (response model)
- ✅ GetMyAuditLogsRequest (query with filters)

**Host Layer:**
- ✅ PersonalController.GetMyAuditLogs endpoint

**Database:**
- ✅ Auditing.AuditTrails table với indexes

**Integration:**
- ✅ IAuditableEntity integration (BUILD_09)
- ✅ ISoftDelete integration (BUILD_19)
- ✅ ISerializerService integration (BUILD_12)

---

### 📊 Architecture Diagram:

```
┌─────────────────────────────────────────────────────────────┐
│   API Layer   │
│   PersonalController     │
│   GET /api/personal/audit-logs │
│   - Filter by table, type, date        │
│   - Pagination support              │
└────────────────────┬────────────────────────────────────────┘
                     ↓
┌────────────────────┴────────────────────────────────────────┐
│              Application Layer               │
│   IAuditService          │
│   - GetMyAuditLogsAsync(request)        │
│        │
│ GetMyAuditLogsRequest│
│   - TableName, Type, FromDate, ToDate      │
│   - Pagination (PageNumber, PageSize)    │
└────────────────────┬────────────────────────────────────────┘
           ↓
┌────────────────────┴────────────────────────────────────────┐
│   Infrastructure Layer   │
│   AuditService              │
│   - Query AuditTrails table         │
│   - Apply filters and pagination      │
│   - Map to DTOs │
│            │
│   BaseDbContext.SaveChangesAsync            │
│   - CaptureAuditTrails() BEFORE save    │
│   - HandleAuditingBeforeSaveChanges()            │
│   - base.SaveChangesAsync()                │
│   - Save audit trails AFTER save │
│         │
│   AuditTrail Helper        │
│   - TransformEntry(entry, userId, serializer)     │
│   - Detect soft delete (DeletedOn: null → value)           │
│   - Serialize OldValues/NewValues to JSON      │
└────────────────────┬────────────────────────────────────────┘
  ↓
┌────────────────────┴────────────────────────────────────────┐
│     Domain Layer          │
│   Trail Entity   │
│   - UserId, Type, TableName, DateTime      │
│   - OldValues, NewValues (JSON)          │
│   - AffectedColumns, PrimaryKey     │
│                  │
│   TrailType Enum         │
│   - Create, Update, Delete            │
└─────────────────────────────────────────────────────────────┘
```

---

### 📌 Key Concepts:

**Audit Trail Pattern:**
- Automatic tracking of all entity changes
- No manual logging code needed
- Transparent to application layer

**Change Data Capture:**
- Capture old and new values
- Track affected columns
- Serialize to JSON for flexibility

**Soft Delete Detection:**
- Detect when DeletedOn changes from null → value
- Log as Delete operation (not Update)
- Integration với BUILD_19 Soft Delete

**Interceptor Pattern:**
- Intercept SaveChangesAsync
- Capture before save, store after save
- Transactional consistency

**Security & Compliance:**
- Track who did what, when
- Meet regulatory requirements (GDPR, SOX)
- Accountability and traceability

---

### 📁 File Structure:

```
src/Core/Domain/
├── Auditing/
│   ├── Trail.cs  ⭐ NEW (audit log entity)
│   └── TrailType.cs   ⭐ NEW (enum)
│
src/Core/Application/
├── Auditing/
│ ├── IAuditService.cs    ⭐ NEW (service interface)
│   ├── AuditDto.cs           ⭐ NEW (response DTO)
│   └── GetMyAuditLogsRequest.cs  ⭐ NEW (query request)
│
src/Infrastructure/Infrastructure/
├── Auditing/
│   ├── AuditTrail.cs       ⭐ NEW (helper class)
│   └── AuditService.cs     ⭐ NEW (service implementation)
├── Persistence/
│   └── Context/
│    └── BaseDbContext.cs  ⭐ UPDATED (audit interceptor)
│
src/Host/Host/
└── Controllers/
    └── Identity/
        └── PersonalController.cs  ⭐ UPDATED (audit logs endpoint)
```

---

### 🎯 Benefits Summary:

**Compliance & Security:**
- ✅ Meet regulatory requirements (GDPR, SOX, HIPAA)
- ✅ Security incident investigation
- ✅ Accountability and traceability

**Debugging & Troubleshooting:**
- ✅ Track down production issues
- ✅ See history of entity changes
- ✅ Understand user behavior

**Business Intelligence:**
- ✅ Analyze user activity patterns
- ✅ Improve UX based on data
- ✅ Generate reports on system usage

**Developer Experience:**
- ✅ Automatic - no manual logging code
- ✅ Transparent - application code unchanged
- ✅ Flexible - query and filter audit logs easily

**Performance:**
- ✅ Efficient - only changed properties logged
- ✅ Indexed - fast queries on UserId, DateTime, TableName
- ✅ Archivable - old logs can be moved to cold storage

---

## 14. Next Steps

**Tiếp theo:** [BUILD_21 - Caching Services](BUILD_21_Caching_Services.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Implement ICacheService interface
2. ✅ LocalCacheService với IMemoryCache
3. ✅ DistributedCacheService với Redis
4. ✅ Cache patterns (Cache-Aside, Write-Through)
5. ✅ Cache invalidation strategies

**⚠️ Lưu ý:** BUILD_21 sẽ sử dụng audit trail để track cache operations (optional advanced feature).
