# Repository Pattern và Specification

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 10 (Service Registration) đã hoàn thành

Tài liệu này hướng dẫn về Repository Pattern với Ardalis.Specification và Domain Events.

---

## 1. Overview

**Làm gì:** Setup Repository Pattern với Specification để query linh hoạt và Domain Events tự động.

**Tại sao cần:**
- **Abstraction:** Tách Application khỏi Infrastructure (EF Core)
- **Flexible Query:** Specification pattern cho complex queries
- **Domain Events:** Tự động phát events khi entity thay đổi
- **Testable:** Dễ mock repositories cho unit tests

**Trong bước này chúng ta sẽ:**
- ✅ Tạo Search/Filter models
- ✅ Tạo Repository interfaces
- ✅ Implement repositories với EF Core
- ✅ Setup EventAddingRepositoryDecorator
- ✅ Tạo Base Specifications để reuse

**Real-world example:**
```csharp
// Controller
public class ProductsController
{
    public async Task<ActionResult> Search([FromBody] SearchProductsRequest request)
    {
  // Specification tự động build query từ request
        var spec = new ProductsBySearchSpec(request);
        
        var products = await _repository.ListAsync(spec); // Query with filters
        var count = await _repository.CountAsync(spec);   // Count without data
        
        return Ok(new PaginatedResult(products, count, request.PageNumber, request.PageSize));
    }
}
```

---

## 2. Tạo Search và Filter Models

### Bước 2.1: Search Model

**File:** `src/Core/Application/Common/Models/Search.cs`

```csharp
namespace ECO.WebApi.Application.Common.Models;

/// <summary>
/// Advanced search với keyword trong các fields cụ thể
/// </summary>
public class Search
{
    /// <summary>
    /// Keyword để search
    /// </summary>
    public string? Keyword { get; set; }
    
    /// <summary>
    /// Danh sách fields để search (nếu null thì search tất cả fields)
    /// Support nested: "Category.Name"
    /// </summary>
    public string[]? Fields { get; set; }
}
```

**Example JSON:**
```json
{
  "keyword": "iphone",
  "fields": ["Name", "Description", "Brand.Name"]
}
```

---

### Bước 2.2: Filter Model

**File:** `src/Core/Application/Common/Models/Filter.cs`

```csharp
namespace ECO.WebApi.Application.Common.Models;

/// <summary>
/// Advanced filter với operators và logic
/// </summary>
public class Filter
{
    /// <summary>
    /// Logic operator: "and", "or", "xor" (dùng khi có nhiều filters)
    /// </summary>
    public string? Logic { get; set; }
    
    /// <summary>
    /// Field name (support nested: "Category.Name")
    /// </summary>
    public string? Field { get; set; }
    
    /// <summary>
    /// Operator: "eq", "neq", "gt", "gte", "lt", "lte", "contains", "startswith", "endswith"
    /// </summary>
    public string? Operator { get; set; }
    
    /// <summary>
    /// Value để compare
    /// </summary>
    public object? Value { get; set; }
    
  /// <summary>
    /// Nested filters (dùng khi có Logic)
    /// </summary>
    public List<Filter>? Filters { get; set; }
}
```

**Example JSON (simple):**
```json
{
  "field": "Price",
  "operator": "gte",
  "value": 1000
}
```

**Example JSON (complex với logic):**
```json
{
  "logic": "and",
  "filters": [
    { "field": "Price", "operator": "gte", "value": 1000 },
    { "field": "Price", "operator": "lte", "value": 5000 },
    {
      "logic": "or",
      "filters": [
        { "field": "Brand.Name", "operator": "eq", "value": "Apple" },
        { "field": "Brand.Name", "operator": "eq", "value": "Samsung" }
      ]
    }
  ]
}
```

---

### Bước 2.3: BaseFilter và PaginationFilter

**File:** `src/Core/Application/Common/Models/BaseFilter.cs`

```csharp
namespace ECO.WebApi.Application.Common.Models;

/// <summary>
/// Base filter cho mọi search requests
/// </summary>
public class BaseFilter
{
    /// <summary>
    /// Simple keyword search (search trong tất cả fields)
    /// </summary>
    public string? Keyword { get; set; }
    
    /// <summary>
  /// Advanced search với fields cụ thể
  /// </summary>
    public Search? AdvancedSearch { get; set; }
    
    /// <summary>
    /// Advanced filter với operators và logic
    /// </summary>
    public Filter? AdvancedFilter { get; set; }
}
```

**File:** `src/Core/Application/Common/Models/PaginationFilter.cs`

```csharp
namespace ECO.WebApi.Application.Common.Models;

/// <summary>
/// Pagination filter kế thừa BaseFilter, thêm pagination và sorting
/// </summary>
public class PaginationFilter : BaseFilter
{
    /// <summary>
    /// Page number (bắt đầu từ 1)
    /// </summary>
    public int PageNumber { get; set; } = 1;
    
    /// <summary>
    /// Page size (số items mỗi page)
    /// </summary>
    public int PageSize { get; set; } = 10;
    
    /// <summary>
    /// OrderBy fields: ["Name", "Price Desc", "Category.Name"]
    /// </summary>
    public string[]? OrderBy { get; set; }
}

public static class PaginationFilterExtensions
{
    public static bool HasOrderBy(this PaginationFilter filter) =>
    filter.OrderBy?.Any() is true;
}
```

**Complete Example JSON:**
```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "orderBy": ["Name", "CreatedOn Desc"],
  "keyword": "phone",
  "advancedSearch": {
    "fields": ["Name", "Description"],
    "keyword": "pro"
  },
  "advancedFilter": {
    "logic": "and",
    "filters": [
      { "field": "Price", "operator": "gte", "value": 500 },
      { "field": "Stock", "operator": "gt", "value": 0 },
      { "field": "IsActive", "operator": "eq", "value": true }
    ]
  }
}
```

---

## 3. Specification Builder Extensions

### 📚 **Tổng quan**

Specification Builder Extensions là bộ extension methods giúp build specifications dễ dàng từ Search/Filter/Pagination requests.

**Core methods:**
- `SearchBy(filter)` - Apply search + filter
- `PaginateBy(filter)` - Apply pagination + sorting
- `OrderBy(fields)` - Apply custom ordering

**⚠️ Implementation Chi tiết:**

Code của SpecificationBuilderExtensions khá phức tạp (Expression Trees, Reflection, Generic types).  
**FULL CODE implementation** được viết trong document riêng: **[BUILD_11_Specification.md](BUILD_11_Specification.md)**

**Trong section này chúng ta chỉ học CÁCH SỬ DỤNG, không đi sâu vào implementation.**

---

### Bước 3.1: Cách sử dụng Specification Extensions

**⚠️ Note:** Trước khi sử dụng, cần tạo file `SpecificationBuilderExtensions.cs` theo hướng dẫn trong [BUILD_11_Specification.md](BUILD_11_Specification.md).

**Usage Example 1 - Simple search:**
```csharp
public class ProductsByKeywordSpec : Specification<Product>
{
    public ProductsByKeywordSpec(string keyword)
    {
        Query
       .SearchByKeyword(keyword)  // Search keyword trong tất cả fields
            .OrderBy(new[] { "Name" }); // Sort by name
    }
}

// Usage:
var spec = new ProductsByKeywordSpec("iphone");
var products = await _repository.ListAsync(spec);
```

**Usage Example 2 - With pagination:**
```csharp
public class ProductsBySearchSpec : Specification<Product>
{
    public ProductsBySearchSpec(PaginationFilter filter)
  {
        Query
    .SearchBy(filter)      // Apply search + filter
      .PaginateBy(filter);   // Apply pagination + sorting
    }
}

// Usage:
var request = new PaginationFilter
{
    PageNumber = 1,
    PageSize = 10,
    Keyword = "iphone",
  OrderBy = new[] { "Name", "Price Desc" }
};

var spec = new ProductsBySearchSpec(request);
var products = await _repository.ListAsync(spec);
var count = await _repository.CountAsync(spec);
```

**Usage Example 3 - Custom specifications:**
```csharp
public class ProductsBySearchSpec : Specification<Product>
{
    public ProductsBySearchSpec(SearchProductsRequest request)
    {
        Query
            .SearchBy(request)      // Apply search/filter từ request
     .PaginateBy(request);   // Apply pagination
     
        // Custom logic riêng
        if (request.CategoryId.HasValue)
        {
   Query.Where(p => p.CategoryId == request.CategoryId.Value);
 }
   
        // Include related entities
   Query
.Include(p => p.Brand)
         .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category);
    }
}
```

**📖 Để hiểu chi tiết implementation:**
- Expression Trees
- Filter operators (eq, neq, gt, contains, etc.)
- Filter logic (and, or, xor)
- Nested property support
- Type conversion (Enum, Guid, DateTime)

→ Xem [BUILD_11_Specification.md](BUILD_11_Specification.md)

---

## 4. Base Specifications

### Bước 4.1: EntitiesByBaseFilterSpec

**File:** `src/Core/Application/Common/Specification/EntitiesByBaseFilterSpec.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Application.Common.Models;

namespace ECO.WebApi.Application.Common.Specification;

/// <summary>
/// Base spec với search + filter (không có pagination)
/// </summary>
public class EntitiesByBaseFilterSpec<T> : Specification<T>
{
    public EntitiesByBaseFilterSpec(BaseFilter filter) =>
        Query.SearchBy(filter);
}

/// <summary>
/// Base spec với search + filter và projection
/// </summary>
public class EntitiesByBaseFilterSpec<T, TResult> : Specification<T, TResult>
{
    public EntitiesByBaseFilterSpec(BaseFilter filter) =>
        Query.SearchBy(filter);
}
```

---

### Bước 4.2: EntitiesByPaginationFilterSpec

**File:** `src/Core/Application/Common/Specification/EntitiesByPaginationFilterSpec.cs`

```csharp
using ECO.WebApi.Application.Common.Models;

namespace ECO.WebApi.Application.Common.Specification;

/// <summary>
/// Base spec với search + filter + pagination
/// </summary>
public class EntitiesByPaginationFilterSpec<T> : EntitiesByBaseFilterSpec<T>
{
    public EntitiesByPaginationFilterSpec(PaginationFilter filter)
    : base(filter) =>
        Query.PaginateBy(filter);
}

/// <summary>
/// Base spec với search + filter + pagination và projection
/// </summary>
public class EntitiesByPaginationFilterSpec<T, TResult> : EntitiesByBaseFilterSpec<T, TResult>
{
    public EntitiesByPaginationFilterSpec(PaginationFilter filter)
        : base(filter) =>
      Query.PaginateBy(filter);
}
```

---

### Bước 4.3: Ví dụ sử dụng Base Specifications

**Example 1 - Simple inherited spec:**
```csharp
// Request DTO
public class SearchProductsRequest : PaginationFilter
{
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }
}

// Specification
public class ProductsBySearchSpec : EntitiesByPaginationFilterSpec<Product>
{
    public ProductsBySearchSpec(SearchProductsRequest request)
        : base(request) // Base class handle search + filter + pagination
    {
        // Chỉ cần add custom logic riêng
        if (request.CategoryId.HasValue)
        {
            Query.Where(p => p.CategoryId == request.CategoryId.Value);
        }
        
        if (request.IsActive.HasValue)
        {
            Query.Where(p => p.IsActive == request.IsActive.Value);
        }
        
        // Include related data
        Query.Include(p => p.Brand)
             .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category);
    }
}
```

**Example 2 - Projection spec:**
```csharp
// DTO
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public string BrandName { get; set; } = default!;
}

// Specification với projection
public class ProductsBySearchSpec : EntitiesByPaginationFilterSpec<Product, ProductDto>
{
    public ProductsBySearchSpec(PaginationFilter filter)
        : base(filter)
    {
        Query.Select(p => new ProductDto
     {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        BrandName = p.Brand.Name
     });
    }
}

// Usage:
var spec = new ProductsBySearchSpec(filter);
var dtos = await _repository.ListAsync(spec); // Return List<ProductDto>
```

---

## 5. Repository Interfaces

### Bước 5.1: IRepository Interfaces

**File:** `src/Core/Application/Common/Persistence/IRepository.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Domain.Common.Contracts;

namespace ECO.WebApi.Application.Common.Persistence;

/// <summary>
/// Read/write repository cho aggregate roots
/// </summary>
public interface IRepository<T> : IRepositoryBase<T>
  where T : class, IAggregateRoot
{
}

/// <summary>
/// Read-only repository cho aggregate roots
/// </summary>
public interface IReadRepository<T> : IReadRepositoryBase<T>
    where T : class, IAggregateRoot
{
}

/// <summary>
/// Repository tự động thêm Domain Events khi Add/Update/Delete
/// </summary>
public interface IRepositoryWithEvents<T> : IRepositoryBase<T>
    where T : class, IAggregateRoot
{
}
```

**Giải thích:**

**IRepository<T>:**
- Read + Write operations
- Kế thừa `IRepositoryBase<T>` từ Ardalis.Specification
- Methods: `GetByIdAsync`, `ListAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, etc.

**IReadRepository<T>:**
- Chỉ Read operations
- Kế thừa `IReadRepositoryBase<T>`
- Methods: `GetByIdAsync`, `ListAsync`, `CountAsync`, etc.

**IRepositoryWithEvents<T>:**
- Giống `IRepository<T>` nhưng tự động thêm Domain Events
- Dùng khi cần events cho: Created/Updated/Deleted

**Why constraint `IAggregateRoot`:**
- DDD principle: Chỉ Aggregate Roots được access từ repositories
- Child entities chỉ access qua Aggregate Root parent

---

### Bước 5.2: Common Repository Methods

**Methods từ Ardalis.Specification:**

```csharp
// Get by ID
Task<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default);

// Get single with spec
Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

// List with spec
Task<List<T>> ListAsync(CancellationToken cancellationToken = default);
Task<List<T>> ListAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

// Count
Task<int> CountAsync(CancellationToken cancellationToken = default);
Task<int> CountAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

// Any
Task<bool> AnyAsync(CancellationToken cancellationToken = default);
Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

// Add
Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

// Update
Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

// Delete
Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

// Save changes
Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
```

---

## 6. Repository Implementation

### Bước 6.1: ApplicationDbRepository

**File:** `src/Infrastructure/Infrastructure/Persistence/Repository/ApplicationDbRepository.cs`

```csharp
using Ardalis.Specification.EntityFrameworkCore;
using ECO.WebApi.Application.Common.Persistence;
using ECO.WebApi.Domain.Common.Contracts;
using ECO.WebApi.Infrastructure.Persistence.Context;

namespace ECO.WebApi.Infrastructure.Persistence.Repository;

/// <summary>
/// EF Core implementation của Repository Pattern với Ardalis.Specification
/// </summary>
public class ApplicationDbRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T>
  where T : class, IAggregateRoot
{
    public ApplicationDbRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
}
```

**Giải thích:**
- Kế thừa `RepositoryBase<T>` từ Ardalis.Specification.EntityFrameworkCore
- `RepositoryBase` cung cấp tất cả implementations cho methods
- Implement cả `IReadRepository<T>` và `IRepository<T>`
- Constructor inject `ApplicationDbContext`

---

## 7. Event Adding Decorator

### Bước 7.1: EventAddingRepositoryDecorator

**File:** `src/Infrastructure/Infrastructure/Persistence/Repository/EventAddingRepositoryDecorator.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Application.Common.Persistence;
using ECO.WebApi.Domain.Common.Contracts;
using ECO.WebApi.Domain.Common.Events;

namespace ECO.WebApi.Infrastructure.Persistence.Repository;

/// <summary>
/// Decorator tự động thêm Domain Events khi Add/Update/Delete entities
/// </summary>
public class EventAddingRepositoryDecorator<T> : IRepositoryWithEvents<T>
    where T : class, IAggregateRoot
{
 private readonly IRepository<T> _decorated;

    public EventAddingRepositoryDecorator(IRepository<T> decorated) => 
  _decorated = decorated;

    public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.DomainEvents.Add(EntityCreatedEvent.WithEntity(entity));
  return _decorated.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
{
 entity.DomainEvents.Add(EntityUpdatedEvent.WithEntity(entity));
        return _decorated.UpdateAsync(entity, cancellationToken);
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
    entity.DomainEvents.Add(EntityDeletedEvent.WithEntity(entity));
   return _decorated.DeleteAsync(entity, cancellationToken);
    }

    public Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
       entity.DomainEvents.Add(EntityDeletedEvent.WithEntity(entity));
        }
        return _decorated.DeleteRangeAsync(entities, cancellationToken);
 }

    // Tất cả methods khác forward đến decorated repository
    public Task<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default) 
where TId : notnull =>
        _decorated.GetByIdAsync(id, cancellationToken);

    public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) =>
      _decorated.FirstOrDefaultAsync(specification, cancellationToken);

    public Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default) =>
        _decorated.FirstOrDefaultAsync(specification, cancellationToken);

    public Task<List<T>> ListAsync(CancellationToken cancellationToken = default) =>
        _decorated.ListAsync(cancellationToken);

    public Task<List<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) =>
        _decorated.ListAsync(specification, cancellationToken);

    public Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default) =>
        _decorated.ListAsync(specification, cancellationToken);

    public Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) =>
      _decorated.CountAsync(specification, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
     _decorated.CountAsync(cancellationToken);

    public Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) =>
        _decorated.AnyAsync(specification, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
 _decorated.AnyAsync(cancellationToken);

    public Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        _decorated.AddRangeAsync(entities, cancellationToken);

    public Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        _decorated.UpdateRangeAsync(entities, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      _decorated.SaveChangesAsync(cancellationToken);
}
```

**Decorator Pattern explained:**

**Mục đích:**
- Tách biệt logic: Repository + Event handling
- Có thể bật/tắt events dễ dàng
- Không modify repository chính

**Flow:**
```
Client
  ↓
IRepositoryWithEvents<Product>
  ↓
EventAddingRepositoryDecorator<Product>
  ├─ AddAsync → Thêm EntityCreatedEvent
  ├─ UpdateAsync → Thêm EntityUpdatedEvent
  └─ DeleteAsync → Thêm EntityDeletedEvent
  ↓
IRepository<Product>
  ↓
ApplicationDbRepository<Product>
  ↓
DbContext.SaveChangesAsync()
  ↓
Publish Domain Events
```

---

## 8. Repository Registration

### Bước 8.1: Đăng ký trong DI Container

**File:** `src/Infrastructure/Infrastructure/Persistence/Startup.cs`

```csharp
using ECO.WebApi.Application.Common.Persistence;
using ECO.WebApi.Domain.Common.Contracts;
using ECO.WebApi.Infrastructure.Persistence.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Persistence;

internal static class Startup
{
  internal static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // ... existing code (DbContext setup) ...

        return services
          .AddRepositories();
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
     // Register base repositories
        services.AddScoped(typeof(IRepository<>), typeof(ApplicationDbRepository<>));
        
        // Auto-discover all Aggregate Roots và register repositories
        foreach (var aggregateRootType in
  typeof(IAggregateRoot).Assembly.GetExportedTypes()
       .Where(t => typeof(IAggregateRoot).IsAssignableFrom(t) && t.IsClass)
          .ToList())
        {
            // IReadRepository<T> → alias của IRepository<T>
            services.AddScoped(
     typeof(IReadRepository<>).MakeGenericType(aggregateRootType),
          sp => sp.GetRequiredService(typeof(IRepository<>).MakeGenericType(aggregateRootType)));

         // IRepositoryWithEvents<T> → EventAddingRepositoryDecorator wrapping IRepository
            services.AddScoped(
      typeof(IRepositoryWithEvents<>).MakeGenericType(aggregateRootType),
              sp => Activator.CreateInstance(
        typeof(EventAddingRepositoryDecorator<>).MakeGenericType(aggregateRootType),
            sp.GetRequiredService(typeof(IRepository<>).MakeGenericType(aggregateRootType)))
       ?? throw new InvalidOperationException(
    $"Could not create EventAddingRepositoryDecorator for {aggregateRootType.Name}"));
}

        return services;
    }
}
```

**Giải thích:**

**Auto-discovery pattern:**
1. Tìm tất cả types implement `IAggregateRoot`
2. Đăng ký repositories cho từng aggregate root
3. Không cần manual registration cho từng entity

**3 loại registrations:**
```csharp
// 1. IRepository<Product> → ApplicationDbRepository<Product>
services.AddScoped<IRepository<Product>, ApplicationDbRepository<Product>>();

// 2. IReadRepository<Product> → IRepository<Product> (alias)
services.AddScoped<IReadRepository<Product>>(sp => 
    sp.GetRequiredService<IRepository<Product>>());

// 3. IRepositoryWithEvents<Product> → EventAddingRepositoryDecorator wrapping IRepository
services.AddScoped<IRepositoryWithEvents<Product>>(sp =>
    new EventAddingRepositoryDecorator<Product>(
        sp.GetRequiredService<IRepository<Product>>()));
```

---

## 9. Usage Examples

### Bước 9.1: Complete Example - Products CRUD

**Request DTOs:**
```csharp
// Search request
public class SearchProductsRequest : PaginationFilter
{
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? IsActive { get; set; }
}

// Create request
public class CreateProductRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public Guid BrandId { get; set; }
    public List<Guid> CategoryIds { get; set; } = new();
}

// Update request
public class UpdateProductRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
```

**Specifications:**
```csharp
// Search specification
public class ProductsBySearchSpec : EntitiesByPaginationFilterSpec<Product>
{
    public ProductsBySearchSpec(SearchProductsRequest request)
        : base(request)
  {
        if (request.CategoryId.HasValue)
        {
            Query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == request.CategoryId.Value));
        }

        if (request.BrandId.HasValue)
        {
    Query.Where(p => p.BrandId == request.BrandId.Value);
 }

  if (request.MinPrice.HasValue)
     {
            Query.Where(p => p.Price >= request.MinPrice.Value);
   }

   if (request.MaxPrice.HasValue)
        {
   Query.Where(p => p.Price <= request.MaxPrice.Value);
        }

     if (request.IsActive.HasValue)
    {
     Query.Where(p => p.IsActive == request.IsActive.Value);
        }

    Query.Include(p => p.Brand)
       .Include(p => p.ProductCategories)
              .ThenInclude(pc => pc.Category);
    }
}

// Get by ID specification
public class ProductByIdSpec : Specification<Product>
{
    public ProductByIdSpec(Guid id)
    {
        Query.Where(p => p.Id == id)
   .Include(p => p.Brand)
  .Include(p => p.ProductCategories)
  .ThenInclude(pc => pc.Category);
    }
}
```

**Handler:**
```csharp
public class SearchProductsHandler : IRequestHandler<SearchProductsRequest, PaginatedResult<ProductDto>>
{
    private readonly IReadRepository<Product> _repository;
    private readonly IMapper _mapper;

  public SearchProductsHandler(IReadRepository<Product> repository, IMapper mapper)
  {
      _repository = repository;
  _mapper = mapper;
    }

    public async Task<PaginatedResult<ProductDto>> Handle(
   SearchProductsRequest request,
        CancellationToken cancellationToken)
    {
var spec = new ProductsBySearchSpec(request);

        var products = await _repository.ListAsync(spec, cancellationToken);
        var count = await _repository.CountAsync(spec, cancellationToken);

        var dtos = _mapper.Map<List<ProductDto>>(products);

        return new PaginatedResult<ProductDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    private readonly IRepositoryWithEvents<Product> _repository;

    public CreateProductHandler(IRepositoryWithEvents<Product> repository)
    {
   _repository = repository;
    }

    public async Task<Guid> Handle(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
 var product = Product.Create(
   request.Name,
  request.Description,
       request.Price,
 request.Stock,
       request.BrandId);

        foreach (var categoryId in request.CategoryIds)
        {
            product.AddCategory(categoryId);
        }

 // AddAsync tự động thêm EntityCreatedEvent
        await _repository.AddAsync(product, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // Event được publish trong SaveChangesAsync
        // → Event handlers tự động xử lý (email, cache, audit, etc.)

        return product.Id;
    }
}

public class UpdateProductHandler : IRequestHandler<UpdateProductRequest>
{
    private readonly IRepositoryWithEvents<Product> _repository;

    public UpdateProductHandler(IRepositoryWithEvents<Product> repository)
    {
_repository = repository;
    }

    public async Task Handle(
        UpdateProductRequest request,
CancellationToken cancellationToken)
    {
    var product = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Product {request.Id} not found");

        product.Update(request.Name, request.Price, request.Stock);

        // UpdateAsync tự động thêm EntityUpdatedEvent
        await _repository.UpdateAsync(product, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
```

---

## 10. Summary

### ✅ Đã hoàn thành trong bước này:

**Models:**
- ✅ Search model (keyword + fields)
- ✅ Filter model (operators + logic)
- ✅ BaseFilter (keyword + advancedSearch + advancedFilter)
- ✅ PaginationFilter (+ pageNumber + pageSize + orderBy)

**Specifications:**
- ✅ SpecificationBuilderExtensions (SearchBy, PaginateBy, OrderBy)
- ✅ EntitiesByBaseFilterSpec (base search/filter)
- ✅ EntitiesByPaginationFilterSpec (+ pagination)

**Repositories:**
- ✅ IRepository<T> (read + write)
- ✅ IReadRepository<T> (read-only)
- ✅ IRepositoryWithEvents<T> (+ domain events)
- ✅ ApplicationDbRepository (EF Core implementation)
- ✅ EventAddingRepositoryDecorator (auto add events)
- ✅ Auto-registration cho tất cả aggregate roots

### 📊 Architecture Diagram:

```
Controller
    ↓
Handler (MediatR)
    ↓
IRepositoryWithEvents<Product>
    ↓
EventAddingRepositoryDecorator
    ├─ Add EntityCreatedEvent
    ├─ Add EntityUpdatedEvent
    └─ Add EntityDeletedEvent
    ↓
IRepository<Product>
    ↓
ApplicationDbRepository
    ↓
ApplicationDbContext
    ├─ SaveChangesAsync
    ├─ Publish Domain Events
    └─ Commit transaction
```

### 🎯 Key Concepts:

**Repository Pattern:**
- Abstraction layer giữa Application và Infrastructure
- Dễ test với mocked repositories
- Support Specification pattern

**Specification Pattern:**
- Build complex queries từ simple objects
- Reusable query logic
- Type-safe

**Decorator Pattern:**
- Tách biệt Repository logic và Event logic
- Dễ bật/tắt features
- Follow Open/Closed Principle

**Domain Events:**
- Tự động phát khi entity thay đổi
- Loose coupling giữa modules
- Event-driven architecture

### 📁 File Structure:

```
src/Core/Application/Common/
├── Models/
│   ├── Search.cs
│   ├── Filter.cs
│   ├── BaseFilter.cs
│ └── PaginationFilter.cs
├── Specification/
│   ├── SpecificationBuilderExtensions.cs
│   ├── EntitiesByBaseFilterSpec.cs
│   └── EntitiesByPaginationFilterSpec.cs
└── Persistence/
    └── IRepository.cs

src/Infrastructure/Infrastructure/Persistence/
├── Repository/
│   ├── ApplicationDbRepository.cs
│   └── EventAddingRepositoryDecorator.cs
└── Startup.cs (registration)
```

---

## 11. Next Steps

**Tiếp theo:** [BUILD_12 - CQRS với MediatR](BUILD_12_CQRS_MediatR.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Setup MediatR cho CQRS pattern
2. ✅ Tạo Commands và Queries
3. ✅ Implement Handlers
4. ✅ Setup Validation với FluentValidation
5. ✅ Setup Behaviors (Logging, Validation, etc.)

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)