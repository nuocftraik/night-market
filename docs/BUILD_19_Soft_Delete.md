# Soft Delete - Xóa Mềm và Global Query Filters

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** [BUILD_09 - Domain Base Entities](BUILD_09_Domain_Base_Entities.md) đã hoàn thành

Tài liệu này hướng dẫn xây dựng hệ thống **Soft Delete** - xóa mềm entities thay vì xóa vĩnh viễn khỏi database.

---

```yaml
---
ai_metadata:
  generated_by: "ai_assisted"
  reviewed_by: "vuongnv1206"
  last_updated: "2026-02-03"
  layer: "Domain + Infrastructure"
  patterns_used:
    - "Soft Delete Pattern"
    - "Global Query Filters"
    - "Decorator Pattern (EventAddingRepositoryDecorator)"
  dependencies:
    - "BUILD_05_Infrastructure_Layer"
    - "BUILD_09_Domain_Base_Entities"
  - "BUILD_11_Repository_Pattern"
  ai_instructions: |
    For Soft Delete implementation:
    - ISoftDelete interface in Domain layer
    - AuditableEntity implements ISoftDelete
    - Global query filters in BaseDbContext.OnModelCreating
  - Convert EntityState.Deleted to Modified in SaveChangesAsync
    - Soft delete detection in BUILD_20 Auditing
---
```

---

## 1. Overview

**Làm gì:** Implement soft delete pattern để đánh dấu entities là "deleted" thay vì xóa vật lý khỏi database.

**Tại sao cần:**
- **Data Recovery:** Có thể khôi phục dữ liệu đã xóa nếu cần thiết
- **Audit Trail:** Giữ lịch sử đầy đủ, biết ai xóa gì, khi nào (kết hợp với BUILD_23 Auditing)
- **Referential Integrity:** Không phá vỡ foreign key relationships
- **Compliance:** Đáp ứng yêu cầu pháp lý về lưu trữ dữ liệu (GDPR, data retention policies)
- **Business Logic:** Nhiều business rules cần biết entity đã bị xóa (tính toán doanh thu, thống kê, reports)
- **Undo Operations:** Users có thể "undo" hành động xóa

**Trong bước này chúng ta sẽ:**
- ✅ Tạo `ISoftDelete` interface (marker interface)
- ✅ Update `AuditableEntity` để implement ISoftDelete
- ✅ Setup Global Query Filters (tự động exclude deleted entities)
- ✅ Implement soft delete logic trong `BaseDbContext.SaveChangesAsync()`
- ✅ Tạo extension methods để query deleted entities
- ✅ Tạo methods để restore deleted entities
- ✅ Tạo Specification patterns cho deleted items

**Real-world example:**
```csharp
// ===== SCENARIO 1: User xóa một product =====
var product = await _repository.GetByIdAsync(productId);
await _repository.DeleteAsync(product);
await _context.SaveChangesAsync();

// Product KHÔNG bị xóa khỏi database
// Chỉ set: DeletedOn = DateTime.UtcNow, DeletedBy = currentUserId
// SQL: UPDATE Products SET DeletedOn = '2024-01-30', DeletedBy = '...' WHERE Id = ' ...'

// ===== SCENARIO 2: Query bình thường - KHÔNG trả về deleted =====
var products = await _repository.ListAsync();
// SQL: SELECT * FROM Products WHERE DeletedOn IS NULL
// products không chứa deleted items (automatic via global filter)

// ===== SCENARIO 3: Query explicitly including deleted =====
var allProducts = await _context.Products
    .IgnoreQueryFilters()  // Disable global filter
    .ToListAsync();
// allProducts bao gồm cả deleted items

// ===== SCENARIO 4: Query chỉ deleted items =====
var spec = new OnlyDeletedProductsSpec();
var deletedProducts = await _repository.ListAsync(spec);
// SQL: SELECT * FROM Products WHERE DeletedOn IS NOT NULL

// ===== SCENARIO 5: Restore deleted product =====
product.DeletedOn = null;
product.DeletedBy = null;
await _context.SaveChangesAsync();
// Product xuất hiện trở lại trong queries bình thường
// SQL: UPDATE Products SET DeletedOn = NULL, DeletedBy = NULL WHERE Id = '...'

// ===== SCENARIO 6: Permanent delete (if needed) =====
_context.Entry(product).State = EntityState.Deleted;
await _context.SaveChangesAsync();
// SQL: DELETE FROM Products WHERE Id = '...'
```

---

## 2. Add Required Packages

**Không cần thêm packages mới** - sử dụng packages đã có:
- `Microsoft.EntityFrameworkCore` (đã có từ BUILD_05 - Infrastructure Layer)

⚠️ **Lưu ý:** Soft Delete hoàn toàn dựa trên EF Core features (Global Query Filters, ChangeTracker), không cần external packages.

---

## 3. Domain Layer - ISoftDelete Interface

### Bước 3.1: Tạo ISoftDelete Interface

**Làm gì:** Tạo marker interface để đánh dấu entities hỗ trợ soft delete.

**Tại sao:** 
- Entities implement interface này sẽ được auto-handle trong SaveChangesAsync
- Global query filters sẽ tự động apply cho tất cả `ISoftDelete` entities
- Type-safe - compiler enforce việc có `DeletedOn`/`DeletedBy` properties

**File:** `src/Core/Domain/Common/Contracts/ISoftDelete.cs`

```csharp
namespace ECO.WebApi.Domain.Common.Contracts;

/// <summary>
/// Marker interface cho entities hỗ trợ soft delete.
/// Entities implement interface này sẽ:
/// - Được đánh dấu DeletedOn/DeletedBy thay vì xóa vật lý
/// - Tự động bị exclude khỏi queries (via global query filter)
/// - Có thể restore bằng cách set DeletedOn = null
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// Thời điểm entity bị xóa (UTC).
    /// NULL = entity chưa bị xóa (active).
  /// Non-null = entity đã bị xóa (soft deleted).
    /// </summary>
    DateTime? DeletedOn { get; set; }

  /// <summary>
  /// User ID của người xóa entity.
    /// NULL = entity chưa bị xóa.
    /// Non-null = entity đã bị xóa bởi user này.
    /// </summary>
    Guid? DeletedBy { get; set; }
}
```

**Giải thích:**

**Marker Interface Pattern:**
- Interface không define methods, chỉ properties
- Đánh dấu entities có capability đặc biệt (soft delete)
- Infrastructure layer check type với `is ISoftDelete` để apply logic

**DeletedOn - DateTime?:**
- **Nullable** rất quan trọng: NULL = active, non-null = deleted
- Luôn dùng **UTC** để consistent across timezones
- Query dễ: `WHERE DeletedOn IS NULL` = active records

**DeletedBy - Guid?:**
- Track user thực hiện soft delete
- Nullable: NULL = active, non-null = deleted by this user
- Kết hợp với Audit Trail (BUILD_23) để biết ai xóa, khi nào

**Why Nullable instead of default values:**
```csharp
// ❌ BAD: Dùng default values
public DateTime DeletedOn { get; set; } = DateTime.MinValue;
// Khó query: WHERE DeletedOn != '0001-01-01'
// Khó understand: MinValue = active hay deleted?

// ✅ GOOD: Dùng Nullable
public DateTime? DeletedOn { get; set; }
// Dễ query: WHERE DeletedOn IS NULL
// Rõ ràng: NULL = active, non-null = deleted
```

**Tại sao Guid? cho DeletedBy:**
- Consistent với `ApplicationUser.Id` type (từ BUILD_03)
- Không phụ thuộc vào ApplicationUser entity (tránh circular dependency)

---

### Bước 3.2: Update AuditableEntity

**Làm gì:** Update `AuditableEntity` để implement `ISoftDelete`.

**Tại sao:** Hầu hết entities trong hệ thống kế thừa `AuditableEntity`, nên chúng tự động có soft delete support mà không cần code thêm.

**File:** `src/Core/Domain/Common/Contracts/AuditableEntity.cs`

```csharp
namespace ECO.WebApi.Domain.Common.Contracts;

/// <summary>
/// Base auditable entity với Guid primary key.
/// Hỗ trợ: Created tracking, Modified tracking, Soft Delete.
/// </summary>
public abstract class AuditableEntity : AuditableEntity<Guid>
{
}

/// <summary>
/// Base auditable entity với generic primary key.
/// Implements: IAuditableEntity (Created/Modified tracking) + ISoftDelete (Soft Delete).
/// </summary>
/// <typeparam name="T">Primary key type (Guid, int, string...)</typeparam>
public abstract class AuditableEntity<T> : BaseEntity<T>, IAuditableEntity, ISoftDelete
{
    #region IAuditableEntity - From BUILD_09

    /// <summary>
    /// User ID của người tạo entity
    /// </summary>
    public Guid CreatedBy { get; set; }

/// <summary>
    /// Thời điểm tạo entity (UTC)
    /// </summary>
    public DateTime CreatedOn { get; private set; }

    /// <summary>
    /// User ID của người modify entity lần cuối
    /// </summary>
    public Guid LastModifiedBy { get; set; }

    /// <summary>
    /// Thời điểm modify lần cuối (UTC)
    /// </summary>
    public DateTime? LastModifiedOn { get; set; }

    #endregion

    #region ISoftDelete - NEW in BUILD_22

  /// <summary>
/// Thời điểm entity bị soft delete (UTC).
    /// NULL = entity chưa bị xóa (active).
 /// Non-null = entity đã bị xóa (soft deleted).
    /// </summary>
    public DateTime? DeletedOn { get; set; }

    /// <summary>
    /// User ID của người soft delete entity.
    /// NULL = entity chưa bị xóa.
 /// Non-null = entity đã bị xóa bởi user này.
    /// </summary>
    public Guid? DeletedBy { get; set; }

    #endregion

    /// <summary>
    /// Constructor - Set CreatedOn và LastModifiedOn mặc định
    /// </summary>
    protected AuditableEntity()
    {
        CreatedOn = DateTime.UtcNow;
        LastModifiedOn = DateTime.UtcNow;
    }
}
```

**Changes from BUILD_09:**

```diff
// BUILD_09: Chỉ IAuditableEntity
- public abstract class AuditableEntity<T> : BaseEntity<T>, IAuditableEntity

// BUILD_22: Thêm ISoftDelete
+ public abstract class AuditableEntity<T> : BaseEntity<T>, IAuditableEntity, ISoftDelete
+ {
+     // ... existing IAuditableEntity properties ...
+     
+     #region ISoftDelete - NEW in BUILD_22
+     public DateTime? DeletedOn { get; set; }
+     public Guid? DeletedBy { get; set; }
+     #endregion
+ }
```

**Giải thích:**

**Multiple Interface Implementation:**
```csharp
public abstract class AuditableEntity<T> : 
    BaseEntity<T>,        // From BUILD_09 (Id, DomainEvents)
    IAuditableEntity,     // From BUILD_09 (Created/Modified)
    ISoftDelete           // NEW (DeletedOn/DeletedBy)
```

**All child entities auto-support soft delete:**
```csharp
// Bất kỳ entity nào kế thừa AuditableEntity đều có soft delete

// ✅ Có soft delete tự động
public class Product : AuditableEntity, IAggregateRoot { }
public class Category : AuditableEntity, IAggregateRoot { }
public class Order : AuditableEntity, IAggregateRoot { }

// ❌ Không có soft delete (dùng BaseEntity)
public class AuditLog : BaseEntity, IAggregateRoot { }
// AuditLog không cần soft delete vì là audit trail
```

**Benefits:**
- ✅ **Consistency** - tất cả auditable entities có cùng soft delete behavior
- ✅ **No boilerplate** - không cần implement ISoftDelete manually cho mỗi entity
- ✅ **Type-safe** - compiler enforce DeletedOn/DeletedBy properties
- ✅ **Single source of truth** - AuditableEntity là single place định nghĩa audit + soft delete

**⚠️ Lưu ý quan trọng:**

DeletedOn/DeletedBy **KHÔNG** được set trong constructor:
```csharp
protected AuditableEntity()
{
    CreatedOn = DateTime.UtcNow;      // ✅ Set
    LastModifiedOn = DateTime.UtcNow; // ✅ Set
    // DeletedOn = null;        // ❌ KHÔNG set (default is null)
    // DeletedBy = null;         // ❌ KHÔNG set (default is null)
}
```

**Tại sao:** Null là default value, set explicitly sẽ gây confusion.

---

## 4. Infrastructure Layer - Global Query Filters

### Bước 4.1: AppendGlobalQueryFilter Extension Method

**Làm gì:** Tạo extension method để apply global query filters cho interfaces (không chỉ concrete types).

**Tại sao:** EF Core's `HasQueryFilter()` chỉ work với concrete types. Chúng ta muốn filter trên interface (`ISoftDelete`) để apply cho TẤT CẢ entities implement interface đó.

**File:** `src/Infrastructure/Infrastructure/Persistence/Extensions/ModelBuilderExtensions.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;
using System.Reflection;

namespace ECO.WebApi.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods for ModelBuilder to work with global query filters
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Apply global query filter cho tất cả entities implement một interface.
    /// EF Core's HasQueryFilter() chỉ work với concrete types, extension này work với interfaces.
    /// </summary>
    /// <typeparam name="TInterface">Interface type (e.g., ISoftDelete)</typeparam>
    /// <param name="modelBuilder">EF Core ModelBuilder</param>
    /// <param name="filterExpression">Lambda expression cho filter (e.g., e => e.DeletedOn == null)</param>
    public static void AppendGlobalQueryFilter<TInterface>(
     this ModelBuilder modelBuilder,
        Expression<Func<TInterface, bool>> filterExpression)
    {
        // Get all entity types trong model
        var entities = modelBuilder.Model.GetEntityTypes();

        foreach (var entityType in entities)
        {
   var clrType = entityType.ClrType;

            // Skip nếu entity không implement interface
            if (!typeof(TInterface).IsAssignableFrom(clrType))
      continue;

 // Build lambda expression: e => (TInterface)e
var parameter = Expression.Parameter(clrType, "e");
  var castExpression = Expression.Convert(parameter, typeof(TInterface));

            // Invoke filter expression với casted parameter
            var invokeExpression = Expression.Invoke(filterExpression, castExpression);

            // Build final lambda: e => filterExpression((TInterface)e)
      var lambdaExpression = Expression.Lambda(invokeExpression, parameter);

            // Get existing filter (nếu có)
         var existingFilter = entityType.GetQueryFilter();

          if (existingFilter != null)
    {
     // Combine với existing filter bằng AND
      // finalFilter = existingFilter && newFilter
         var existingParameter = existingFilter.Parameters[0];
    var newParameter = lambdaExpression.Parameters[0];

           // Replace parameter trong existing filter
        var leftExpression = ReplacingExpressionVisitor.Replace(
 existingParameter,
            newParameter,
     existingFilter.Body);

        // Combine: existingFilter.Body && lambdaExpression.Body
  var combinedBody = Expression.AndAlso(leftExpression, lambdaExpression.Body);

    // Build combined lambda
      var combinedLambda = Expression.Lambda(combinedBody, newParameter);

       entityType.SetQueryFilter(combinedLambda);
   }
         else
      {
  // No existing filter, set new filter
    entityType.SetQueryFilter(lambdaExpression);
      }
        }
    }
}
```

**Giải thích chi tiết:**

**1. Why Extension Method:**
```csharp
// ❌ BAD: Apply filter manually cho từng entity
modelBuilder.Entity<Product>().HasQueryFilter(e => e.DeletedOn == null);
modelBuilder.Entity<Category>().HasQueryFilter(e => e.DeletedOn == null);
modelBuilder.Entity<Order>().HasQueryFilter(e => e.DeletedOn == null);
// ... 50+ entities = 50+ dòng code lặp lại

// ✅ GOOD: Apply filter cho TẤT CẢ ISoftDelete entities
modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(e => e.DeletedOn == null);
// 1 dòng code apply cho tất cả!
```

**2. Generic Type `<TInterface>`:**
```csharp
public static void AppendGlobalQueryFilter<TInterface>(...)
```
- `TInterface` là interface muốn filter (ISoftDelete, ITenant, etc.)
- Method work với **bất kỳ interface nào**, không chỉ ISoftDelete

**3. Filter Expression Parameter:**
```csharp
Expression<Func<TInterface, bool>> filterExpression
```
- Lambda expression: `e => e.DeletedOn == null`
- `Func<TInterface, bool>` = take TInterface, return bool
- `Expression<...>` = expression tree (EF Core can translate to SQL)

**4. Expression Tree Building:**

**Step 1: Get all entities**
```csharp
var entities = modelBuilder.Model.GetEntityTypes();
// entities = [Product, Category, Order, User, ...]
```

**Step 2: Check if entity implements interface**
```csharp
if (!typeof(TInterface).IsAssignableFrom(clrType))
    continue;

// Product implements ISoftDelete? → Yes → Apply filter
// AuditLog implements ISoftDelete? → No → Skip
```

**Step 3: Build expression**
```csharp
// Original: e => e.DeletedOn == null (where e is ISoftDelete)
// Need: e => ((ISoftDelete)e).DeletedOn == null (where e is Product)

var parameter = Expression.Parameter(clrType, "e");  
// e : Product

var castExpression = Expression.Convert(parameter, typeof(ISoftDelete));
// (ISoftDelete)e

var invokeExpression = Expression.Invoke(filterExpression, castExpression);
// filterExpression((ISoftDelete)e)

var lambdaExpression = Expression.Lambda(invokeExpression, parameter);
// e => filterExpression((ISoftDelete)e)
```

**Step 4: Combine with existing filters**
```csharp
if (existingFilter != null)
{
    // Entity already has a filter (e.g., multi-tenancy)
  // Combine: existingFilter && newFilter
    var combinedBody = Expression.AndAlso(leftExpression, lambdaExpression.Body);
}
```

**Why combine?**
```csharp
// Scenario: Product has multi-tenancy filter
// Existing: e => e.TenantId == currentTenant
// New: e => e.DeletedOn == null
// Combined: e => e.TenantId == currentTenant && e.DeletedOn == null
```

**Benefits:**
- ✅ **DRY** - Don't Repeat Yourself (1 dòng thay vì 50+)
- ✅ **Type-safe** - Works với bất kỳ interface nào
- ✅ **Composable** - Combine multiple filters với AND
- ✅ **Maintainable** - Thêm entity mới tự động có filter

---

### Bước 4.2: Apply Global Query Filter trong BaseDbContext

**Làm gì:** Apply soft delete filter trong `OnModelCreating()`.

**Tại sao:** Filter tự động exclude deleted entities khỏi TẤT CẢ queries (trừ khi dùng `IgnoreQueryFilters()`).

**File:** `src/Infrastructure/Infrastructure/Persistence/Context/BaseDbContext.cs`

```csharp
using ECO.WebApi.Application.Common.Events;
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Domain.Common.Contracts;
using ECO.WebApi.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Infrastructure.Persistence.Context;

/// <summary>
/// Base DbContext với audit trail, domain events, và soft delete support
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply soft delete global query filter cho TẤT CẢ ISoftDelete entities
        // Tất cả queries sẽ tự động filter: WHERE DeletedOn IS NULL
        modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(e => e.DeletedOn == null);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
 {
        // Handle audit trail và soft delete
        HandleAuditingBeforeSaveChanges();

        // Publish domain events
        await PublishDomainEventsAsync(cancellationToken);

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Handle auditing (Created/Modified/Deleted tracking) và soft delete
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
 // ⭐ SOFT DELETE LOGIC ⭐
         // Thay vì xóa vật lý, chuyển sang Modified và set DeletedOn/DeletedBy
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

**1. Global Query Filter Setup:**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // ⭐ Apply soft delete filter ⭐
    modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(e => e.DeletedOn == null);
}
```

**Effect:**
```csharp
// User code: Simple query
var products = await _context.Products.ToListAsync();

// Generated SQL (automatic filter):
SELECT * FROM Products WHERE DeletedOn IS NULL
```

**2. Soft Delete Logic in SaveChangesAsync:**

```csharp
case EntityState.Deleted:
    if (entry.Entity is ISoftDelete softDelete)
    {
        softDelete.DeletedOn = DateTime.UtcNow;
 softDelete.DeletedBy = userId;
        entry.State = EntityState.Modified;  // ⭐ Key change: Deleted → Modified
    }
    break;
```

**Flow:**
```
1. User calls: _repository.DeleteAsync(product)
   ↓
2. EF Core sets: entry.State = EntityState.Deleted
   ↓
3. SaveChangesAsync() intercepts
   ↓
4. Check: Is entity ISoftDelete?
   ↓
5. Yes: Set DeletedOn/DeletedBy, change State to Modified
   ↓
6. EF generates: UPDATE Products SET DeletedOn = '...', DeletedBy = '...'
   (NOT DELETE FROM Products)
```

**3. Audit Trail Integration:**

```csharp
case EntityState.Modified:
    entry.Entity.LastModifiedBy = userId;
    entry.Entity.LastModifiedOn = DateTime.UtcNow;
    break;
```

**Soft delete triggers Modified:**
```csharp
// Soft delete → State = Modified
// Modified case → Set LastModifiedBy/LastModifiedOn
// Result: Soft delete ALSO tracks who modified and when!
```

**Final entity state after soft delete:**
```json
{
  "Id": "...",
  "Name": "iPhone 15",
  "CreatedBy": "user-1",
  "CreatedOn": "2024-01-01",
  "LastModifiedBy": "user-2",       // ← Set by audit
  "LastModifiedOn": "2024-01-30",   // ← Set by audit
  "DeletedBy": "user-2", // ← Set by soft delete
  "DeletedOn": "2024-01-30"          // ← Set by soft delete
}
```

**Benefits:**
- ✅ **Automatic** - Developers chỉ call `DeleteAsync()`, logic tự động
- ✅ **Consistent** - Tất cả ISoftDelete entities có cùng behavior
- ✅ **Auditable** - Combine với IAuditableEntity tracking
- ✅ **Transparent** - User code không cần biết về soft delete implementation

---

## 5. Application Layer - Repository Extensions

### Bước 5.1: Include Deleted Specification

**Làm gì:** Tạo Specification để query deleted entities.

**Tại sao:** Default queries exclude deleted entities (via global filter). Cần specification để query deleted items khi cần.

**File:** `src/Core/Application/Common/Specifications/ISoftDeleteSpecification.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Domain.Common.Contracts;

namespace ECO.WebApi.Application.Common.Specifications;

/// <summary>
/// Specification base cho soft delete queries.
/// Provides methods để include/exclude deleted entities.
/// </summary>
public abstract class SoftDeleteSpecification<T> : Specification<T>
    where T : class, ISoftDelete
{
    /// <summary>
    /// Include deleted entities trong query.
    /// Disables global query filter.
    /// </summary>
    protected void IncludeDeleted()
    {
        Query.IgnoreQueryFilters();
    }

    /// <summary>
    /// Query chỉ deleted entities.
    /// Disables global filter và add explicit filter: DeletedOn != null
    /// </summary>
    protected void OnlyDeleted()
    {
    Query.IgnoreQueryFilters()
 .Where(e => e.DeletedOn != null);
    }
}
```

**Usage Examples:**

```csharp
// ===== Example 1: Query chỉ deleted products =====
public class OnlyDeletedProductsSpec : SoftDeleteSpecification<Product>
{
    public OnlyDeletedProductsSpec()
    {
        OnlyDeleted();  // WHERE DeletedOn IS NOT NULL
    }
}

var deletedProducts = await _repository.ListAsync(new OnlyDeletedProductsSpec());
// SQL: SELECT * FROM Products WHERE DeletedOn IS NOT NULL

// ===== Example 2: Query all products (bao gồm deleted) =====
public class AllProductsIncludingDeletedSpec : SoftDeleteSpecification<Product>
{
    public AllProductsIncludingDeletedSpec()
    {
   IncludeDeleted();  // Ignore global filter
    }
}

var allProducts = await _repository.ListAsync(new AllProductsIncludingDeletedSpec());
// SQL: SELECT * FROM Products (no WHERE DeletedOn IS NULL)

// ===== Example 3: Query deleted products trong date range =====
public class DeletedProductsByDateRangeSpec : SoftDeleteSpecification<Product>
{
    public DeletedProductsByDateRangeSpec(DateTime from, DateTime to)
 {
        Query.IgnoreQueryFilters()
      .Where(e => e.DeletedOn != null && 
        e.DeletedOn >= from && 
e.DeletedOn <= to);
    }
}

var recentlyDeleted = await _repository.ListAsync(
    new DeletedProductsByDateRangeSpec(
        DateTime.UtcNow.AddDays(-7), 
        DateTime.UtcNow
    )
);
// SQL: SELECT * FROM Products 
//      WHERE DeletedOn IS NOT NULL 
//    AND DeletedOn >= @from 
//AND DeletedOn <= @to
```

---

### Bước 5.2: Restore Extension Method

**Làm gì:** Tạo extension method để restore deleted entities.

**Tại sao:** Encapsulate restore logic, dễ sử dụng và test.

**File:** `src/Core/Application/Common/Extensions/SoftDeleteExtensions.cs`

```csharp
using ECO.WebApi.Domain.Common.Contracts;

namespace ECO.WebApi.Application.Common.Extensions;

/// <summary>
/// Extension methods cho soft delete operations
/// </summary>
public static class SoftDeleteExtensions
{
  /// <summary>
 /// Restore deleted entity bằng cách set DeletedOn/DeletedBy = null
    /// </summary>
    /// <typeparam name="T">Entity type implementing ISoftDelete</typeparam>
    /// <param name="entity">Entity to restore</param>
    public static void Restore<T>(this T entity)
        where T : ISoftDelete
    {
   entity.DeletedOn = null;
        entity.DeletedBy = null;
    }

    /// <summary>
    /// Check if entity đã bị soft delete
    /// </summary>
    /// <typeparam name="T">Entity type implementing ISoftDelete</typeparam>
 /// <param name="entity">Entity to check</param>
    /// <returns>True if entity is soft deleted (DeletedOn != null)</returns>
    public static bool IsDeleted<T>(this T entity)
        where T : ISoftDelete
    {
    return entity.DeletedOn.HasValue;
    }

    /// <summary>
    /// Soft delete entity manually (normally handled by SaveChangesAsync)
    /// </summary>
    /// <typeparam name="T">Entity type implementing ISoftDelete</typeparam>
    /// <param name="entity">Entity to soft delete</param>
    /// <param name="userId">User performing the delete</param>
    public static void SoftDelete<T>(this T entity, Guid userId)
      where T : ISoftDelete
    {
        entity.DeletedOn = DateTime.UtcNow;
        entity.DeletedBy = userId;
    }
}
```

**Usage Examples:**

```csharp
// ===== Example 1: Restore deleted product =====
var spec = new OnlyDeletedProductsSpec();
var deletedProducts = await _repository.ListAsync(spec);
var productToRestore = deletedProducts.First();

// Restore
productToRestore.Restore();  // Extension method
await _context.SaveChangesAsync();

// Product is now active again
// SQL: UPDATE Products SET DeletedOn = NULL, DeletedBy = NULL WHERE Id = '...'

// ===== Example 2: Check if product is deleted =====
var product = await _repository.GetByIdAsync(productId);

if (product.IsDeleted())
{
    throw new InvalidOperationException("Cannot update deleted product");
}

product.UpdatePrice(newPrice);
await _context.SaveChangesAsync();

// ===== Example 3: Manual soft delete (rare, normally use DeleteAsync) =====
var userId = _currentUser.GetUserId();
product.SoftDelete(userId);
await _context.SaveChangesAsync();
// Same effect as _repository.DeleteAsync(product)
```

**Benefits:**
- ✅ **Fluent API** - `product.Restore()` rõ ràng hơn `product.DeletedOn = null`
- ✅ **Encapsulation** - Logic ẩn trong extension methods
- ✅ **Type-safe** - Chỉ work với ISoftDelete entities
- ✅ **Testable** - Dễ mock và test

---

## 6. Complete Usage Examples

### Bước 6.1: Product Service với Soft Delete

**File:** `src/Core/Application/Catalog/Products/ProductService.cs` (example)

```csharp
using ECO.WebApi.Application.Common.Extensions;
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Application.Common.Specifications;
using ECO.WebApi.Domain.Catalog;
using Mapster;

namespace ECO.WebApi.Application.Catalog.Products;

public interface IProductService : ITransientService
{
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default);
 Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<List<ProductDto>> GetDeletedAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task PermanentDeleteAsync(Guid id, CancellationToken ct = default);
}

public class ProductService : IProductService
{
    private readonly IRepository<Product> _repository;
    private readonly ApplicationDbContext _context;

    public ProductService(
   IRepository<Product> repository,
    ApplicationDbContext context)
    {
        _repository = repository;
     _context = context;
    }

    // ===== QUERY: Get active product by ID =====
    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
     // Global filter automatically applies: WHERE DeletedOn IS NULL
        var product = await _repository.GetByIdAsync(id, ct);
        
        if (product == null)
         throw new NotFoundException("Product not found");

    return product.Adapt<ProductDto>();
    }

    // ===== QUERY: Get all active products =====
    public async Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        // Global filter applies: WHERE DeletedOn IS NULL
        var products = await _repository.ListAsync(ct);
        
        return products.Adapt<List<ProductDto>>();
    }

    // ===== QUERY: Get chỉ deleted products =====
    public async Task<List<ProductDto>> GetDeletedAsync(CancellationToken ct = default)
    {
        var spec = new OnlyDeletedProductsSpec();
        var deletedProducts = await _repository.ListAsync(spec, ct);
   
        return deletedProducts.Adapt<List<ProductDto>>();
 }

    // ===== CREATE: Tạo product mới =====
    public async Task<Guid> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
   var product = request.Adapt<Product>();
        
        await _repository.AddAsync(product, ct);
    
        return product.Id;
    }

    // ===== UPDATE: Cập nhật product =====
    public async Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct);
    
        if (product == null)
         throw new NotFoundException("Product not found");

        // ⚠️ Check nếu product đã bị xóa
        if (product.IsDeleted())
      throw new InvalidOperationException("Cannot update deleted product. Restore first.");

        request.Adapt(product);
        
        await _repository.UpdateAsync(product, ct);
    }

 // ===== SOFT DELETE: Xóa mềm product =====
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct);
     
        if (product == null)
            throw new NotFoundException("Product not found");

        // DeleteAsync → SaveChangesAsync intercepts → Set DeletedOn/DeletedBy
        await _repository.DeleteAsync(product, ct);
    }

    // ===== RESTORE: Khôi phục deleted product =====
    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        // Need to query deleted items explicitly
        var spec = new ProductByIdIncludingDeletedSpec(id);
  var product = await _repository.FirstOrDefaultAsync(spec, ct);
        
  if (product == null)
  throw new NotFoundException("Product not found");

        if (!product.IsDeleted())
  throw new InvalidOperationException("Product is not deleted");

        // Restore extension method
 product.Restore();
        
        await _context.SaveChangesAsync(ct);
    }

    // ===== PERMANENT DELETE: Xóa vĩnh viễn product =====
    public async Task PermanentDeleteAsync(Guid id, CancellationToken ct = default)
    {
      var spec = new ProductByIdIncludingDeletedSpec(id);
        var product = await _repository.FirstOrDefaultAsync(spec, ct);
        
        if (product == null)
  throw new NotFoundException("Product not found");

        // Force physical delete
        _context.Entry(product).State = EntityState.Deleted;
 await _context.SaveChangesAsync(ct);
    }
}

// ===== SPECIFICATIONS =====

public class OnlyDeletedProductsSpec : SoftDeleteSpecification<Product>
{
    public OnlyDeletedProductsSpec()
    {
        OnlyDeleted();
    }
}

public class ProductByIdIncludingDeletedSpec : SoftDeleteSpecification<Product>
{
public ProductByIdIncludingDeletedSpec(Guid id)
    {
      Query.Where(p => p.Id == id);
        IncludeDeleted();
    }
}
```

---

### Bước 6.2: Products Controller

**File:** `src/Host/Host/Controllers/Catalog/ProductsController.cs` (example)

```csharp
using ECO.WebApi.Application.Catalog.Products;
using ECO.WebApi.Infrastructure.Auth.Permissions;
using ECO.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers.Catalog;

[Route("api/catalog/products")]
public class ProductsController : BaseApiController
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    // ===== GET: Get all active products =====
    [HttpGet]
    [MustHavePermission(ECOAction.View, ECOFunction.Products)]
  public async Task<ActionResult<List<ProductDto>>> GetAll(CancellationToken ct)
    {
        var products = await _productService.GetAllAsync(ct);
  return Ok(products);
    }

  // ===== GET: Get product by ID =====
    [HttpGet("{id:guid}")]
  [MustHavePermission(ECOAction.View, ECOFunction.Products)]
public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
  {
      var product = await _productService.GetByIdAsync(id, ct);
        return Ok(product);
}

    // ===== GET: Get deleted products (Admin only) =====
  [HttpGet("deleted")]
    [MustHavePermission(ECOAction.View, ECOFunction.Products)]
    public async Task<ActionResult<List<ProductDto>>> GetDeleted(CancellationToken ct)
    {
   var deletedProducts = await _productService.GetDeletedAsync(ct);
        return Ok(deletedProducts);
    }

    // ===== POST: Create product =====
    [HttpPost]
    [MustHavePermission(ECOAction.Create, ECOFunction.Products)]
  public async Task<ActionResult<Guid>> Create(CreateProductRequest request, CancellationToken ct)
    {
   var productId = await _productService.CreateAsync(request, ct);
     return Ok(productId);
    }

    // ===== PUT: Update product =====
    [HttpPut("{id:guid}")]
    [MustHavePermission(ECOAction.Update, ECOFunction.Products)]
    public async Task<ActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        await _productService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    // ===== DELETE: Soft delete product =====
    [HttpDelete("{id:guid}")]
    [MustHavePermission(ECOAction.Delete, ECOFunction.Products)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        return NoContent();
    }

    // ===== POST: Restore deleted product =====
[HttpPost("{id:guid}/restore")]
    [MustHavePermission(ECOAction.Update, ECOFunction.Products)]
    public async Task<ActionResult> Restore(Guid id, CancellationToken ct)
    {
        await _productService.RestoreAsync(id, ct);
        return NoContent();
    }

    // ===== DELETE: Permanent delete (Admin only) =====
    [HttpDelete("{id:guid}/permanent")]
    [MustHavePermission(ECOAction.Delete, ECOFunction.Products)]
    public async Task<ActionResult> PermanentDelete(Guid id, CancellationToken ct)
    {
     await _productService.PermanentDeleteAsync(id, ct);
        return NoContent();
    }
}
```

**API Examples:**

```bash
# 1. Get all active products
GET /api/catalog/products
# Response: [{ "id": "...", "name": "iPhone 15", ... }]

# 2. Get deleted products
GET /api/catalog/products/deleted
# Response: [{ "id": "...", "name": "Old Product", "deletedOn": "2024-01-30", ... }]

# 3. Soft delete product
DELETE /api/catalog/products/123e4567-...
# Product still in database, DeletedOn set

# 4. Restore deleted product
POST /api/catalog/products/123e4567-.../restore
# Product active again, DeletedOn = null

# 5. Permanent delete (careful!)
DELETE /api/catalog/products/123e4567-.../permanent
# Product REMOVED from database forever
```

---

## 7. Database Migration

### Bước 7.1: Add Migration

**Commands:**

```powershell
# Navigate to Migrators.MSSQL project
cd src/Migrators/Migrators.MSSQL

# Add migration
dotnet ef migrations add Add_SoftDelete_To_AuditableEntity `
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
public partial class Add_SoftDelete_To_AuditableEntity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add DeletedOn column cho tất cả AuditableEntity tables
        migrationBuilder.AddColumn<DateTime>(
      name: "DeletedOn",
       table: "Products",
       type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
     name: "DeletedBy",
            table: "Products",
    type: "uniqueidentifier",
      nullable: true);

        migrationBuilder.AddColumn<DateTime>(
     name: "DeletedOn",
  table: "Categories",
 type: "datetime2",
     nullable: true);

        migrationBuilder.AddColumn<Guid>(
name: "DeletedBy",
       table: "Categories",
            type: "uniqueidentifier",
       nullable: true);

// ... repeat for all AuditableEntity tables
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "DeletedOn", table: "Products");
    migrationBuilder.DropColumn(name: "DeletedBy", table: "Products");
        migrationBuilder.DropColumn(name: "DeletedOn", table: "Categories");
        migrationBuilder.DropColumn(name: "DeletedBy", table: "Categories");
        // ... repeat for all tables
    }
}
```

**⚠️ Lưu ý:**
- Migration add columns cho **TẤT CẢ** tables có entities kế thừa AuditableEntity
- Columns are **nullable** (correct - NULL = active)
- No default values (correct - NULL is the default)

---

## 8. Best Practices

### ✅ DO

**1. Always use Soft Delete cho business entities:**
```csharp
// ✅ GOOD: Business entities với soft delete
public class Product : AuditableEntity, IAggregateRoot { }
public class Order : AuditableEntity, IAggregateRoot { }
public class Customer : AuditableEntity, IAggregateRoot { }
```

**2. Use Permanent Delete cho technical entities:**
```csharp
// ✅ GOOD: Technical entities WITHOUT soft delete
public class AuditLog : BaseEntity, IAggregateRoot { }
public class TempFile : BaseEntity, IAggregateRoot { }
```

**Why:** AuditLog không cần soft delete vì là audit trail. Temp files có thể xóa vĩnh viễn.

**3. Check IsDeleted() trước khi update:**
```csharp
// ✅ GOOD: Check trước khi update
if (product.IsDeleted())
    throw new InvalidOperationException("Cannot update deleted product");

product.UpdatePrice(newPrice);
```

**4. Use Specifications để query deleted items:**
```csharp
// ✅ GOOD: Use specification
var spec = new OnlyDeletedProductsSpec();
var deletedProducts = await _repository.ListAsync(spec);
```

**5. Provide Restore functionality cho users:**
```csharp
// ✅ GOOD: Allow restore
[HttpPost("{id}/restore")]
public async Task<ActionResult> Restore(Guid id, CancellationToken ct)
{
await _productService.RestoreAsync(id, ct);
    return NoContent();
}
```

---

### ❌ DON'T

**1. Don't query deleted items trực tiếp:**
```csharp
// ❌ BAD: Forget về soft delete
var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
// Throws if product is soft deleted (global filter excludes it)

// ✅ GOOD: Use specification
var spec = new ProductByIdIncludingDeletedSpec(id);
var product = await _repository.FirstOrDefaultAsync(spec);
// product found (even if deleted)
```

**2. Don't set DeletedOn manually without DeletedBy:**
```csharp
// ❌ BAD: Incomplete soft delete
product.DeletedOn = DateTime.UtcNow;
// Missing DeletedBy!

// ✅ GOOD: Use extension method
product.SoftDelete(userId);
// Or use repository.DeleteAsync() - automatic
```

**3. Don't permanent delete without confirmation:**
```csharp
// ❌ BAD: Immediate permanent delete
[HttpDelete("{id}/permanent")]
public async Task<ActionResult> PermanentDelete(Guid id)
{
    await _productService.PermanentDeleteAsync(id);
    return NoContent();
}

// ✅ GOOD: Require confirmation
[HttpDelete("{id}/permanent")]
public async Task<ActionResult> PermanentDelete(
    Guid id, 
    [FromQuery] bool confirmed = false)
{
    if (!confirmed)
        return BadRequest("Please confirm permanent delete");

    await _productService.PermanentDeleteAsync(id);
  return NoContent();
}
```

**4. Don't mix soft delete và permanent delete carelessly:**
```csharp
// ❌ BAD: Confusing API
[HttpDelete("{id}")]  // Soft or permanent?
public async Task<ActionResult> Delete(Guid id) { ... }

// ✅ GOOD: Clear separation
[HttpDelete("{id}")]             // Soft delete
[HttpDelete("{id}/permanent")]   // Permanent delete
```

---

## 9. Troubleshooting

### Issue 1: "Entity not found" sau khi soft delete

**Problem:**
```csharp
await _repository.DeleteAsync(product);
await _context.SaveChangesAsync();

// Later...
var product = await _repository.GetByIdAsync(productId);
// product is null! (global filter excludes deleted)
```

**Solution:**
```csharp
// Use specification to include deleted
var spec = new ProductByIdIncludingDeletedSpec(productId);
var product = await _repository.FirstOrDefaultAsync(spec);
// product found (even if deleted)
```

---

### Issue 2: Foreign Key Constraint khi xóa

**Problem:**
```csharp
// Product has OrderItems referencing it
await _repository.DeleteAsync(product);
await _context.SaveChangesAsync();
// Works! But what about OrderItems?
```

**Solution 1: Soft delete cascade (recommended):**
```csharp
public class Product : AuditableEntity
{
    public void Delete(Guid userId)
    {
        DeletedOn = DateTime.UtcNow;
        DeletedBy = userId;
        
        // Cascade soft delete to children
        foreach (var variant in Variants)
        {
       variant.DeletedOn = DeletedOn;
     variant.DeletedBy = DeletedBy;
}
    }
}
```

**Solution 2: Prevent delete if referenced:**
```csharp
public async Task DeleteAsync(Guid id, CancellationToken ct)
{
    var product = await _repository.GetByIdAsync(id, ct);
    
    // Check references
    var hasOrders = await _context.OrderItems
        .AnyAsync(oi => oi.ProductId == id, ct);
    
    if (hasOrders)
        throw new InvalidOperationException(
            "Cannot delete product with existing orders");
    
    await _repository.DeleteAsync(product, ct);
}
```

---

### Issue 3: Performance với large tables

**Problem:**
```csharp
// Global filter applies to EVERY query
SELECT * FROM Products WHERE DeletedOn IS NULL
// Index on DeletedOn needed!
```

**Solution: Add index:**
```csharp
// In ApplicationDbContext.OnModelCreating
modelBuilder.Entity<Product>()
    .HasIndex(p => p.DeletedOn)
    .HasFilter("DeletedOn IS NULL");  // Filtered index (SQL Server)
```

**Migration:**
```csharp
migrationBuilder.CreateIndex(
  name: "IX_Products_DeletedOn",
    table: "Products",
    column: "DeletedOn",
    filter: "DeletedOn IS NULL");
```

---

## 10. Integration với BUILD_20 Auditing

### Soft Delete Detection trong Audit Trail

**File:** `src/Infrastructure/Infrastructure/Auditing/AuditTrail.cs` (từ BUILD_20)

```csharp
// Audit trail tự động detect soft delete
foreach (var property in modifiedProperties)
{
    var propertyName = property.Metadata.Name;
    
    // ⭐ Detect soft delete: DeletedOn changed from null → value
    if (property.IsModified && 
        entry.Entity is ISoftDelete && 
        propertyName == nameof(ISoftDelete.DeletedOn) &&
        property.OriginalValue == null && 
        property.CurrentValue != null)
    {
        trailEntry.TrailType = TrailType.Delete;  // ✅ Log as Delete
     break;
    }
}
```

**Effect:**

Audit trail shows soft delete as "Delete" event:
```json
{
  "userId": "user-123",
  "type": "Delete",  // ← Soft delete detected
  "tableName": "Products",
  "dateTime": "2024-01-30T10:30:00Z",
"oldValues": { 
    "Name": "iPhone 15",
    "DeletedOn": null,
    "DeletedBy": null
  },
  "newValues": { 
    "DeletedOn": "2024-01-30T10:30:00Z",
    "DeletedBy": "user-123"
  },
  "affectedColumns": ["DeletedOn", "DeletedBy"],
  "primaryKey": "product-123"
}
```

**Benefits:**
- ✅ Audit trail chính xác reflect "Delete" operation
- ✅ Track who deleted, when deleted
- ✅ Can query all deleted items từ audit log

---

## 11. Summary

### ✅ Đã hoàn thành trong bước này:

**Domain Layer:**
- ✅ ISoftDelete interface (marker interface với DeletedOn/DeletedBy)
- ✅ AuditableEntity implements ISoftDelete (all entities auto-support soft delete)

**Infrastructure Layer:**
- ✅ ModelBuilderExtensions.AppendGlobalQueryFilter (apply filter cho interfaces)
- ✅ BaseDbContext.OnModelCreating (apply soft delete global filter)
- ✅ BaseDbContext.SaveChangesAsync (intercept Delete → set DeletedOn/DeletedBy)

**Application Layer:**
- ✅ SoftDeleteSpecification (base class cho soft delete queries)
- ✅ SoftDeleteExtensions (Restore, IsDeleted, SoftDelete methods)
- ✅ Specifications (OnlyDeleted, IncludeDeleted)

**Complete Examples:**
- ✅ ProductService với soft delete operations
- ✅ ProductsController với REST APIs
- ✅ Database migration

**Integration:**
- ✅ Audit Trail integration (BUILD_23)
- ✅ Permission-based authorization (BUILD_17)

---

### 📊 Architecture Diagram:

```
┌─────────────────────────────────────────────────────────────┐
│     API Layer         │
│   ProductsController           │
│   - GET /products (active only)     │
│   - GET /products/deleted                 │
│   - DELETE /products/{id} (soft delete)         │
│   - POST /products/{id}/restore    │
│   - DELETE /products/{id}/permanent    │
└────────────────────┬────────────────────────────────────────┘
     ↓
┌────────────────────┴────────────────────────────────────────┐
│ Application Layer       │
│   ProductService          │
│   - DeleteAsync() → Soft delete         │
│   - RestoreAsync() → Restore                  │
│   - PermanentDeleteAsync() → Physical delete                │
│    │
│   SoftDeleteSpecification  │
│   - OnlyDeleted()     │
│   - IncludeDeleted()              │
│       │
│   SoftDeleteExtensions       │
│   - Restore(), IsDeleted(), SoftDelete()       │
└────────────────────┬────────────────────────────────────────┘
       ↓
┌────────────────────┴────────────────────────────────────────┐
│    Infrastructure Layer  │
│   BaseDbContext   │
│   - OnModelCreating:     │
│     modelBuilder.AppendGlobalQueryFilter<ISoftDelete>( │
│         e => e.DeletedOn == null     │
│     )               │
│                   │
│   - SaveChangesAsync:            │
│     if (entry.State == Deleted && entity is ISoftDelete)    │
│     {                │
│         entity.DeletedOn = DateTime.UtcNow;                 │
│         entity.DeletedBy = userId;         │
│         entry.State = Modified;  │
│     }              │
└────────────────────┬────────────────────────────────────────┘
               ↓
┌────────────────────┴────────────────────────────────────────┐
│        Domain Layer          │
│   ISoftDelete          │
│   - DateTime? DeletedOn   │
│   - Guid? DeletedBy  │
│                  │
│   AuditableEntity : ISoftDelete               │
│   - Implements ISoftDelete    │
│   - All child entities auto-support soft delete             │
└─────────────────────────────────────────────────────────────┘
```

---

### 📌 Key Concepts:

**ISoftDelete Interface:**
- Marker interface cho entities support soft delete
- `DeletedOn` (DateTime?) - NULL = active, non-null = deleted
- `DeletedBy` (Guid?) - Track user performed delete

**Global Query Filter:**
- Auto-apply `WHERE DeletedOn IS NULL` to all queries
- Transparent - developers không cần code filter manually
- Can bypass với `IgnoreQueryFilters()`

**SaveChangesAsync Interception:**
- Intercept `EntityState.Deleted`
- Convert to `EntityState.Modified`
- Set `DeletedOn`/`DeletedBy` instead of physical delete

**Soft Delete Specifications:**
- `OnlyDeleted()` - Query chỉ deleted items
- `IncludeDeleted()` - Query all items (active + deleted)
- Combine với business logic filters

**Restore Pattern:**
- Set `DeletedOn = null`, `DeletedBy = null`
- Entity appears in queries again
- Audit trail tracks restore operation

**Permanent Delete:**
- `_context.Entry(entity).State = EntityState.Deleted`
- Physical delete from database
- Require confirmation (careful!)

---

### 📁 File Structure:

```
src/Core/Domain/Common/
├── Contracts/
│   ├── ISoftDelete.cs       ⭐ NEW
│   ├── IAuditableEntity.cs   (from BUILD_09)
│└── AuditableEntity.cs              ⭐ UPDATED (implements ISoftDelete)
│
src/Core/Application/Common/
├── Specifications/
│└── SoftDeleteSpecification.cs        ⭐ NEW
├── Extensions/
│   └── SoftDeleteExtensions.cs        ⭐ NEW
│
src/Infrastructure/Infrastructure/
├── Persistence/
│   ├── Extensions/
│   │   └── ModelBuilderExtensions.cs     ⭐ NEW (AppendGlobalQueryFilter)
│   └── Context/
│ └── BaseDbContext.cs         ⭐ UPDATED (global filter + soft delete logic)
│
src/Host/Host/
└── Controllers/
    └── Catalog/
        └── ProductsController.cs   ⭐ EXAMPLE (with soft delete endpoints)
```

---

### 🎯 Benefits Summary:

**Data Recovery:**
- ✅ Restore deleted items easily
- ✅ Undo user mistakes
- ✅ Meet compliance requirements

**Audit Trail:**
- ✅ Know who deleted what, when
- ✅ Complete history tracking
- ✅ Security investigation support

**Performance:**
- ✅ No foreign key cascade deletes
- ✅ Filtered indexes for DeletedOn
- ✅ Efficient queries

**Developer Experience:**
- ✅ Transparent - use DeleteAsync() as normal
- ✅ Global filter auto-applies
- ✅ Simple restore API

**Business Value:**
- ✅ Compliance ready (GDPR, SOX)
- ✅ Business intelligence on deleted items
- ✅ Better user experience (undo deletes)

---

## 12. Next Steps

**Tiếp theo:** [BUILD_20 - Auditing](BUILD_20_Auditing.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo Trail entity để lưu audit logs
2. ✅ Implement audit interceptor trong SaveChangesAsync
3. ✅ Track soft delete events (DeletedOn changed from null → value)
4. ✅ Tạo IAuditService để query audit logs
5. ✅ Expose audit logs qua API

**⚠️ Lưu ý:** BUILD_20 mở rộng soft delete bằng cách track delete events trong audit trail.

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
