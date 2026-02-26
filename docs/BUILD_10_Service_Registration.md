# Service Registration Pattern

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 8 (Database Initialization) hoàn thành

Tài liệu này hướng dẫn về Service Registration Pattern - tự động đăng ký services mà không cần đăng ký thủ công từng service.

---

## 1. Overview

**Làm gì:** Setup auto-registration pattern cho services với marker interfaces.

**Tại sao cần:**
- **Automation:** Tự động đăng ký services, không cần manual registration
- **Convention-based:** Services follow convention → auto-discovered
- **Maintainability:** Thêm service mới không cần update Startup
- **Type-safe:** Compile-time safety với interfaces

**Trong bước này chúng ta sẽ:**
- ✅ Tạo marker interfaces (ITransientService, IScopedService)
- ✅ Tạo AddServices extension method
- ✅ Implement auto-registration logic
- ✅ Setup trong Infrastructure Startup

---

## 2. Understanding Service Lifetimes

### Bước 2.1: Service Lifetime Types

**Transient:**
```csharp
services.AddTransient<IService, Service>();
```
- Tạo instance mới mỗi lần request
- Use for: Stateless services, lightweight operations
- Examples: Validators, Formatters, Calculators

**Scoped:**
```csharp
services.AddScoped<IService, Service>();
```
- Tạo instance mới mỗi HTTP request
- Shared trong cùng request
- Use for: Services with request-specific state
- Examples: DbContext, Current User Service, Repository

**Singleton:**
```csharp
services.AddSingleton<IService, Service>();
```
- Tạo instance duy nhất cho toàn app lifetime
- Shared across all requests
- Use for: Configuration, Caching, Logging
- Examples: IConfiguration, IMemoryCache, ILogger

---

## 3. Tạo Marker Interfaces

### Bước 3.1: ITransientService Interface

**File:** `src/Core/Application/Common/Interfaces/ITransientService.cs`

```csharp
namespace ECO.WebApi.Application.Common.Interfaces;

/// <summary>
/// Marker interface for transient services.
/// Services implementing this will be registered with Transient lifetime.
/// </summary>
public interface ITransientService
{
}
```

---

### Bước 3.2: IScopedService Interface

**File:** `src/Core/Application/Common/Interfaces/IScopedService.cs`

```csharp
namespace ECO.WebApi.Application.Common.Interfaces;

/// <summary>
/// Marker interface for scoped services.
/// Services implementing this will be registered with Scoped lifetime.
/// </summary>
public interface IScopedService
{
}
```

**Marker Interfaces:**
- Không có methods hay properties
- Chỉ dùng để "đánh dấu" service lifetime
- Convention: Service implements business interface + marker interface

---

## 4. Tạo AddServices Extension Method

### Bước 4.1: Service Registration Extensions

**File:** `src/Infrastructure/Infrastructure/Common/Extensions.cs`

```csharp
using System.Reflection;
using ECO.WebApi.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Common;

internal static class Extensions
{
    /// <summary>
    /// Auto-register all services implementing ITransientService or IScopedService
    /// </summary>
    internal static IServiceCollection AddServices(this IServiceCollection services) =>
  services
        .AddServices(typeof(ITransientService), ServiceLifetime.Transient)
   .AddServices(typeof(IScopedService), ServiceLifetime.Scoped);

    /// <summary>
    /// Scan assemblies and register services implementing specified marker interface
    /// </summary>
    internal static IServiceCollection AddServices(
  this IServiceCollection services,
        Type markerInterfaceType,
    ServiceLifetime lifetime)
    {
        // Get all assemblies in current AppDomain
    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        // Scan for types implementing marker interface
   var implementationTypes = assemblies
   .SelectMany(assembly => assembly.GetTypes())
         .Where(type =>
   type.IsClass &&
      !type.IsAbstract &&
        !type.IsGenericType &&
          markerInterfaceType.IsAssignableFrom(type))
     .ToList();

  foreach (var implementationType in implementationTypes)
        {
       // Get business interfaces (exclude marker interfaces)
      var serviceInterfaces = implementationType.GetInterfaces()
        .Where(i => i != markerInterfaceType &&
            !typeof(ITransientService).IsAssignableFrom(i) &&
         !typeof(IScopedService).IsAssignableFrom(i))
     .ToList();

            // Register with first business interface found
  if (serviceInterfaces.Any())
  {
      var serviceInterface = serviceInterfaces.First();
       services.Add(new ServiceDescriptor(
           serviceInterface,
            implementationType,
            lifetime));
            }
        }

    return services;
    }
}
```

**Key Logic:**

1. **Scan assemblies:** Get all types from loaded assemblies
2. **Filter classes:** Only concrete, non-abstract, non-generic classes
3. **Check marker:** Must implement marker interface (ITransientService/IScopedService)
4. **Get business interface:** Filter out marker interfaces, get first business interface
5. **Register:** Add to DI container with specified lifetime

**Why filter marker interfaces:**
```csharp
// ❌ Bad: Register with marker interface
services.AddTransient<ITransientService, ProductService>(); // Wrong!

// ✅ Good: Register with business interface
services.AddTransient<IProductService, ProductService>(); // Correct!
```

---

## 5. Setup trong Infrastructure

### Bước 5.1: Update Infrastructure Startup

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Auth;
using ECO.WebApi.Infrastructure.BackgroundJobs;
using ECO.WebApi.Infrastructure.Caching;
using ECO.WebApi.Infrastructure.Common;
using ECO.WebApi.Infrastructure.FileStorage;
using ECO.WebApi.Infrastructure.Localization;
using ECO.WebApi.Infrastructure.Mailing;
using ECO.WebApi.Infrastructure.Middleware;
using ECO.WebApi.Infrastructure.Notifications;
using ECO.WebApi.Infrastructure.OpenApi;
using ECO.WebApi.Infrastructure.Persistence;
using ECO.WebApi.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure;

public static class Startup
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
    MapsterSettings.Configure();
        
        return services
  .AddApiVersioning()
 .AddAuth(config)
            .AddBackgroundJobs(config)
            .AddCaching(config)
   .AddExceptionMiddleware()
    .AddLocalization(config)
          .AddMailing(config)
  .AddNotifications(config)
 .AddOpenApiDocumentation(config)
       .AddPersistence(config)
      .AddRequestLogging(config)
            .AddRouting(options => options.LowercaseUrls = true)
  .AddServices(); // ⭐ Auto-register services
    }

    // ... other methods ...
}
```

**Thứ tự registration:**
- Call `AddServices()` cuối cùng
- Đảm bảo tất cả dependencies (DbContext, etc.) đã được register trước

---

## 6. Ví dụ Sử dụng

### Bước 6.1: Transient Service Example

**File:** `src/Infrastructure/Infrastructure/Services/EmailService.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;

namespace ECO.WebApi.Infrastructure.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}

// ⭐ Implement business interface + marker interface
internal class EmailService : IEmailService, ITransientService
{
    public async Task SendAsync(string to, string subject, string body)
    {
        // Send email implementation
    await Task.CompletedTask;
    }
}
```

**Result:** Tự động đăng ký:
```csharp
services.AddTransient<IEmailService, EmailService>();
```

---

### Bước 6.2: Scoped Service Example

**File:** `src/Infrastructure/Infrastructure/Services/CurrentUserService.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;

namespace ECO.WebApi.Infrastructure.Services;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
}

// ⭐ Scoped per HTTP request
internal class CurrentUserService : ICurrentUserService, IScopedService
{
  private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => 
   _httpContextAccessor.HttpContext?.User?.FindFirst("uid")?.Value;

    public string? Email => 
        _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;
}
```

**Result:** Tự động đăng ký:
```csharp
services.AddScoped<ICurrentUserService, CurrentUserService>();
```

---

### Bước 6.3: Multiple Interfaces Example

**File:** `src/Infrastructure/Infrastructure/Services/ProductService.cs`

```csharp
public interface IProductService
{
    Task<ProductDto> GetByIdAsync(int id);
}

public interface IProductQueryService
{
    Task<List<ProductDto>> SearchAsync(string query);
}

// ⭐ Multiple business interfaces + marker
internal class ProductService : 
IProductService,    // First interface → used for registration
    IProductQueryService,     // Also implemented
    ITransientService   // Marker
{
    public async Task<ProductDto> GetByIdAsync(int id)
    {
 // Implementation
   return new ProductDto();
    }

    public async Task<List<ProductDto>> SearchAsync(string query)
    {
        // Implementation
        return new List<ProductDto>();
    }
}
```

**Result:** Registered với first business interface:
```csharp
services.AddTransient<IProductService, ProductService>();
```

**⚠️ Note:** Chỉ interface đầu tiên được registered. Nếu cần cả 2, phải manual register:
```csharp
services.AddTransient<IProductQueryService>(sp => 
    sp.GetRequiredService<IProductService>() as ProductService);
```

---

## 7. Best Practices

### Bước 7.1: Service Conventions

**✅ Good:**
```csharp
// Clear naming
public interface IEmailService { }
internal class EmailService : IEmailService, ITransientService { }

// Business interface first, marker last
internal class UserService : IUserService, IScopedService { }
```

**❌ Bad:**
```csharp
// Marker first (confusing)
internal class EmailService : ITransientService, IEmailService { }

// No interface
internal class EmailService : ITransientService { } // Won't be registered!
```

---

### Bước 7.2: When to Use Each Lifetime

**Transient:**
- ✅ Stateless services
- ✅ Lightweight operations
- ✅ No shared state
- Examples: Validators, Formatters, Calculators

**Scoped:**
- ✅ Per-request state
- ✅ Database operations (DbContext)
- ✅ Current user context
- Examples: Repositories, UnitOfWork, CurrentUserService

**Singleton (manual only):**
- ✅ Application-wide state
- ✅ Expensive to create
- ✅ Thread-safe
- Examples: Configuration, Caching, Logging

---

### Bước 7.3: Testing

**Unit Test Example:**
```csharp
[Fact]
public void AddServices_ShouldRegisterTransientServices()
{
    // Arrange
    var services = new ServiceCollection();

    // Act
    services.AddServices();

    // Assert
    var descriptor = services.FirstOrDefault(s => 
  s.ServiceType == typeof(IEmailService));
    
    Assert.NotNull(descriptor);
    Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    Assert.Equal(typeof(EmailService), descriptor.ImplementationType);
}
```

---

## 8. Common Issues

### Issue 1: "Service not registered"

**Nguyên nhân:** Service không implement cả business interface VÀ marker interface

**Giải pháp:**
```csharp
// ❌ Missing marker interface
internal class EmailService : IEmailService { }

// ✅ Include marker interface
internal class EmailService : IEmailService, ITransientService { }
```

---

### Issue 2: "Wrong interface registered"

**Nguyên nhân:** Marker interface ở vị trí đầu tiên

**Giải pháp:**
```csharp
// ❌ Marker first
internal class EmailService : ITransientService, IEmailService { }

// ✅ Business interface first
internal class EmailService : IEmailService, ITransientService { }
```

---

### Issue 3: "Multiple implementations conflict"

**Nguyên nhân:** Nhiều classes implement cùng interface

**Giải pháp:**
```csharp
// Option 1: Sử dụng named services (manual)
services.AddTransient<IEmailService, SmtpEmailService>();
services.AddTransient<IEmailService, SendGridEmailService>();

// Option 2: Factory pattern
services.AddTransient<IEmailServiceFactory>(sp => 
    new EmailServiceFactory(sp));
```

---

## 9. Summary

### ✅ Đã hoàn thành trong bước này:

**Marker Interfaces:**
- ✅ ITransientService (for transient lifetime)
- ✅ IScopedService (for scoped lifetime)

**Auto-Registration:**
- ✅ AddServices() extension method
- ✅ Assembly scanning logic
- ✅ Business interface detection

**Infrastructure Setup:**
- ✅ Integrated vào Infrastructure Startup
- ✅ Auto-register tất cả services

### 📊 Registration Flow:

```
Service class implements IXxxService + ITransientService
    ↓
AddServices() scans assemblies
    ↓
Detects marker interface
    ↓
Gets business interface (IXxxService)
    ↓
Registers: services.AddTransient<IXxxService, XxxService>()
```

### 🎯 Benefits:

- **No manual registration:** Không cần update Startup khi thêm service
- **Convention-based:** Follow naming conventions
- **Type-safe:** Compile-time checks
- **Maintainable:** Clear service organization

### 💡 Key Takeaways:

1. **Service must implement:** Business interface + Marker interface
2. **Business interface first:** For correct registration
3. **Marker is just a flag:** No methods, only for lifetime indication
4. **Transient for stateless:** Scoped for per-request state

---

## 10. Next Steps

**Tiếp theo:** [BUILD_10 - Domain Base Entities](BUILD_10_Domain_Base_Entities.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo base entities (BaseEntity, AuditableEntity)
2. ✅ Implement domain events
3. ✅ Setup audit fields (CreatedBy, UpdatedBy, etc.)
4. ✅ Value objects pattern
5. ✅ Entity equality

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
