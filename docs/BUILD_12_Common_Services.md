# Common Services - CurrentUser, Serializer, Event Publisher

> 👉 [Quay lại Mục lục](BUILD_INDEX.md)  
> 👉 **Prerequisites:** Bước 11 (Repository Pattern) đã hoàn thành

Tài liệu này hướng dẫn xây dựng các Core Services nền tảng: CurrentUser, Serializer, và Event Publisher.

---

## 1. Overview

**Làm gì:** Xây dựng các core services được sử dụng xuyên suốt application.

**Tại sao cần:**
- **CurrentUser Service:** Lấy thông tin user hiện tại từ JWT token trong mọi handler/service
- **Serializer Service:** Serialize/deserialize objects cho caching, logging, messaging
- **Event Publisher:** Publish domain events để trigger các event handlers (decoupling)

**Trong bước này chúng ta sẽ:**
- ✓ Tạo `ICurrentUser` và `ICurrentUserInitializer` interfaces
- ✓ Implement `CurrentUser` service với ClaimsPrincipal
- ✓ Tạo `CurrentUserMiddleware` để auto-set current user
- ✓ Tạo `ISerializerService` interface
- ✓ Implement `NewtonSoftService` (JSON serialization)
- ✓ Tạo `IEventPublisher` interface
- ✓ Implement `EventPublisher` với MediatR integration
- ✓ Register services và middleware

**Real-world example:**
```csharp
// Trong handler - Lấy current user
public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    private readonly ICurrentUser _currentUser;
  private readonly IEventPublisher _eventPublisher;

    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
        // Auto có thông tin user hiện tại
  var userId = _currentUser.GetUserId();
 var userEmail = _currentUser.GetUserEmail();
        
      var product = Product.Create(request.Name, request.Price);
      
        // Publish domain event
await _eventPublisher.PublishAsync(new ProductCreatedEvent(product));
   
        return product.Id;
    }
}
```

---

## 2. Add Required Packages

### Bước 2.1: Add Newtonsoft.Json Package

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- JSON Serialization -->
 <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
```

**Giải thích:**
- `Newtonsoft.Json`: JSON serializer/deserializer (mature và feature-rich hơn System.Text.Json)

**⚠️ Lưu ý:** MediatR đã có từ Application layer, không cần add lại.

---

## 3. CurrentUser Service

### Bước 3.1: ICurrentUser Interface

**Làm gì:** Tạo interface để lấy thông tin user hiện tại từ JWT token.

**Tại sao:** Handlers/Services cần biết user nào đang thực hiện action (audit, authorization).

**File:** `src/Core/Application/Common/Interfaces/ICurrentUser.cs`

```csharp
using System.Security.Claims;

namespace ECO.WebApi.Application.Common.Interfaces;

/// <summary>
/// Interface để lấy thông tin user hiện tại từ JWT token
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// User name từ Identity.Name
  /// </summary>
    string? Name { get; }

    /// <summary>
    /// Lấy User ID (Guid) từ NameIdentifier claim
  /// </summary>
 Guid GetUserId();

    /// <summary>
    /// Lấy User Email từ Email claim
    /// </summary>
    string? GetUserEmail();

    /// <summary>
    /// Check user đã authenticate chưa
    /// </summary>
    bool IsAuthenticated();

    /// <summary>
    /// Check user có role cụ thể không
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Lấy tất cả claims của user
    /// </summary>
    IEnumerable<Claim>? GetUserClaims();
}
```

**Giải thích:**
- `Name`: Display name từ JWT claims
- `GetUserId()`: User ID (Guid) từ NameIdentifier claim
- `GetUserEmail()`: Email từ Email claim
- `IsAuthenticated()`: Check xem user đã login chưa
- `IsInRole(role)`: Check user có role cụ thể (Admin, Basic, etc.)
- `GetUserClaims()`: Lấy all claims để custom logic

**Tại sao tách interface:**
- Read-only trong handlers/services
- Dễ mock cho unit testing
- Separation of concerns

---

### Bước 3.2: ICurrentUserInitializer Interface

**Làm gì:** Interface để set current user (dùng trong middleware).

**Tại sao:** Middleware cần set user từ HttpContext, còn handlers chỉ cần đọc.

**File:** `src/Core/Application/Common/Interfaces/ICurrentUserInitializer.cs`

```csharp
using System.Security.Claims;

namespace ECO.WebApi.Application.Common.Interfaces;

/// <summary>
/// Interface để initialize current user (dùng trong middleware)
/// </summary>
public interface ICurrentUserInitializer
{
    /// <summary>
    /// Set current user từ ClaimsPrincipal (từ JWT token)
    /// </summary>
    void SetCurrentUser(ClaimsPrincipal user);

    /// <summary>
    /// Set current user ID manually (cho background jobs/system operations)
    /// </summary>
    void SetCurrentUserId(string userId);
}
```

**Giải thích:**
- `SetCurrentUser()`: Set từ HttpContext.User (có JWT token)
- `SetCurrentUserId()`: Set manually cho background jobs (không có HTTP context)

**Tại sao tách 2 interfaces:**
- `ICurrentUser`: Read-only cho handlers/services
- `ICurrentUserInitializer`: Write-only cho middleware
- Better encapsulation

---

### Bước 3.3: ClaimsPrincipal Extension Methods

**Làm gì:** Extension methods để lấy claims từ ClaimsPrincipal dễ dàng hơn.

**Tại sao:** Code gọn hơn, reusable, type-safe.

**File:** `src/Core/Shared/Authorization/ClaimsPrincipalExtensions.cs`

```csharp
using ECO.WebApi.Shared.Authorization;

namespace System.Security.Claims;

/// <summary>
/// Extension methods cho ClaimsPrincipal
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Lấy Email từ ClaimTypes.Email
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
  => principal.FindFirstValue(ClaimTypes.Email);

    /// <summary>
    /// Lấy Full Name từ ECOClaims.Fullname
    /// </summary>
    public static string? GetFullName(this ClaimsPrincipal principal)
        => principal?.FindFirst(ECOClaims.Fullname)?.Value;

    /// <summary>
    /// Lấy First Name từ ClaimTypes.Name
    /// </summary>
    public static string? GetFirstName(this ClaimsPrincipal principal)
      => principal?.FindFirst(ClaimTypes.Name)?.Value;

    /// <summary>
    /// Lấy Surname từ ClaimTypes.Surname
/// </summary>
    public static string? GetSurname(this ClaimsPrincipal principal)
    => principal?.FindFirst(ClaimTypes.Surname)?.Value;

    /// <summary>
    /// Lấy Phone Number từ ClaimTypes.MobilePhone
    /// </summary>
    public static string? GetPhoneNumber(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.MobilePhone);

    /// <summary>
    /// Lấy User ID từ ClaimTypes.NameIdentifier
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Lấy Image URL từ ECOClaims.ImageUrl
    /// </summary>
    public static string? GetImageUrl(this ClaimsPrincipal principal)
     => principal.FindFirstValue(ECOClaims.ImageUrl);

    /// <summary>
    /// Lấy Token Expiration từ ECOClaims.Expiration
    /// </summary>
 public static DateTimeOffset GetExpiration(this ClaimsPrincipal principal) =>
        DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(
    principal.FindFirstValue(ECOClaims.Expiration)));

    /// <summary>
    /// Helper method để tìm claim value
    /// </summary>
    private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType) =>
        principal is null
? throw new ArgumentNullException(nameof(principal))
            : principal.FindFirst(claimType)?.Value;
}
```

**Giải thích:**
- Extension methods để code gọn hơn: `user.GetUserId()` thay vì `user.FindFirst(ClaimTypes.NameIdentifier)?.Value`
- Support custom claims: `Fullname`, `ImageUrl`, `Expiration`
- Null-safe với `?` operator
- Private `FindFirstValue()` helper để avoid repetition

**Lợi ích:**
- ✓ Code gọn, dễ đọc
- ✓ Type-safe
- ✓ Reusable
- ✓ Dễ maintain

---

### Bước 3.4: CurrentUser Implementation

**Làm gì:** Implement CurrentUser service kết hợp ICurrentUser và ICurrentUserInitializer.

**Tại sao:** Một class implement cả 2 interfaces, scoped per request.

**File:** `src/Infrastructure/Infrastructure/Auth/CurrentUser.cs`

```csharp
using System.Security.Claims;
using ECO.WebApi.Application.Common.Interfaces;

namespace ECO.WebApi.Infrastructure.Auth;

/// <summary>
/// Implementation của ICurrentUser và ICurrentUserInitializer
/// Scoped per request - mỗi HTTP request có instance riêng
/// </summary>
public class CurrentUser : ICurrentUser, ICurrentUserInitializer
{
    private ClaimsPrincipal? _user;
    private Guid _userId = Guid.Empty;

    /// <summary>
    /// User name từ Identity.Name
    /// </summary>
    public string? Name => _user?.Identity?.Name;

    /// <summary>
    /// Lấy User ID từ NameIdentifier claim
  /// </summary>
    public Guid GetUserId() =>
        IsAuthenticated()
            ? Guid.Parse(_user?.GetUserId() ?? Guid.Empty.ToString())
            : _userId;

    /// <summary>
    /// Lấy User Email từ Email claim
    /// </summary>
public string? GetUserEmail() =>
        IsAuthenticated()
        ? _user!.GetEmail()
            : string.Empty;

  /// <summary>
    /// Check user đã authenticate chưa
    /// </summary>
    public bool IsAuthenticated() =>
_user?.Identity?.IsAuthenticated is true;

  /// <summary>
    /// Check user có role không
    /// </summary>
    public bool IsInRole(string role) =>
        _user?.IsInRole(role) is true;

    /// <summary>
    /// Lấy tất cả claims
    /// </summary>
    public IEnumerable<Claim>? GetUserClaims() =>
        _user?.Claims;

    /// <summary>
    /// Set current user từ ClaimsPrincipal
    /// Chỉ được gọi một lần per request (từ middleware)
    /// </summary>
    public void SetCurrentUser(ClaimsPrincipal user)
    {
        if (_user != null)
        {
  throw new Exception("Method reserved for in-scope initialization");
        }

        _user = user;
    }

 /// <summary>
    /// Set current user ID manually (cho background jobs)
    /// </summary>
    public void SetCurrentUserId(string userId)
    {
        if (_userId != Guid.Empty)
        {
     throw new Exception("Method reserved for in-scope initialization");
     }

        if (!string.IsNullOrEmpty(userId))
        {
    _userId = Guid.Parse(userId);
        }
    }
}
```

**Giải thích:**

**Private fields:**
- `_user`: ClaimsPrincipal từ JWT token (HTTP requests)
- `_userId`: User ID manual (background jobs không có HTTP context)

**Thread-safety:**
- Service là `Scoped` → mỗi request có instance riêng
- Check `_user != null` để prevent double initialization
- Throw exception nếu gọi `SetCurrentUser()` nhiều lần

**Fallback logic:**
- Nếu authenticated → lấy từ claims
- Nếu không → return empty/default values (background jobs)

**Tại sao cần _userId riêng:**
- Background jobs (Hangfire) không có HTTP context
- Vẫn cần track user thực hiện job
- Set manual qua `SetCurrentUserId()`

---

### Bước 3.5: CurrentUserMiddleware

**Làm gì:** Middleware để tự động set current user từ HttpContext.User.

**Tại sao:** Mỗi request đều cần user context, middleware tự động set thay vì manual.

**File:** `src/Infrastructure/Infrastructure/Auth/CurrentUserMiddleware.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ECO.WebApi.Infrastructure.Auth;

/// <summary>
/// Middleware để set current user từ HttpContext.User
/// Phải đặt SAU UseAuthentication() trong pipeline
/// </summary>
public class CurrentUserMiddleware : IMiddleware
{
    private readonly ICurrentUserInitializer _currentUserInitializer;

    public CurrentUserMiddleware(ICurrentUserInitializer currentUserInitializer) =>
        _currentUserInitializer = currentUserInitializer;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
  {
        // Set current user từ HttpContext.User (đã authenticate bởi JWT middleware)
    _currentUserInitializer.SetCurrentUser(context.User);
        
      // Continue pipeline
        await next(context);
}
}
```

**Giải thích:**
- `IMiddleware` interface → ASP.NET Core middleware pattern
- `SetCurrentUser(context.User)` → Set ClaimsPrincipal từ authenticated user
- `await next(context)` → Continue pipeline

**Thứ tự middleware (QUAN TRỌNG):**
```
1. UseRouting()
2. UseAuthentication()   → JWT middleware populate context.User
3. UseCurrentUserMiddleware()    → Set ICurrentUser từ context.User
4. UseAuthorization()
5. MapControllers()
```

**⚠️ Lưu ý:** Middleware này phải đặt SAU `UseAuthentication()` để có `context.User`.

---

### Bước 3.6: Register CurrentUser Service

**Làm gì:** Register CurrentUser và middleware vào DI container.

**Tại sao:** ASP.NET Core cần biết cách tạo và inject services.

**File:** `src/Infrastructure/Infrastructure/Auth/Startup.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Auth;

internal static class Startup
{
    /// <summary>
    /// Register CurrentUser services
    /// </summary>
    internal static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
    // Register middleware as Scoped (per request)
   services.AddScoped<CurrentUserMiddleware>();
        
        // Register CurrentUser as Scoped - mỗi request một instance
        // Cả 2 interfaces đều resolve về cùng instance
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentUserInitializer, CurrentUser>();

   return services;
    }

    /// <summary>
    /// Use CurrentUser middleware
  /// </summary>
 internal static IApplicationBuilder UseCurrentUserMiddleware(this IApplicationBuilder app) =>
 app.UseMiddleware<CurrentUserMiddleware>();
}
```

**Giải thích:**
- `Scoped` lifetime → mỗi HTTP request có instance riêng, dispose sau khi request done
- `ICurrentUser` và `ICurrentUserInitializer` → cùng resolve về một instance `CurrentUser`
- Extension methods để code gọn

**Tại sao Scoped:**
- ✓ Mỗi request có user riêng (thread-safe)
- ✓ Dispose tự động sau request
- ✓ Performance tốt hơn Transient

---

## 4. Serializer Service

### Bước 4.1: ISerializerService Interface

**Làm gì:** Interface để serialize/deserialize objects thành JSON.

**Tại sao:** Caching, logging, messaging đều cần serialize objects. Interface để dễ thay đổi implementation.

**File:** `src/Core/Application/Common/Interfaces/ISerializerService.cs`

```csharp
namespace ECO.WebApi.Application.Common.Interfaces;

/// <summary>
/// Interface để serialize/deserialize objects
/// Dùng cho caching, logging, messaging, etc.
/// </summary>
public interface ISerializerService : ITransientService
{
    /// <summary>
    /// Serialize object thành JSON string
    /// </summary>
    string Serialize<T>(T obj);

 /// <summary>
    /// Serialize object thành JSON string với type cụ thể
 /// </summary>
  string Serialize<T>(T obj, Type type);

    /// <summary>
    /// Deserialize JSON string thành object
    /// </summary>
    T Deserialize<T>(string text);
}
```

**Giải thích:**
- `Transient` lifetime → tạo instance mới mỗi lần inject (lightweight)
- Generic methods → support any type
- 2 overloads cho `Serialize()` để flexible

**Use cases:**
- **Caching:** Serialize objects trước khi cache vào Redis
- **Logging:** Serialize request/response để log
- **Messaging:** Serialize events/commands để send qua queue
- **Database:** Serialize complex objects vào JSON column

---

### Bước 4.2: NewtonSoftService Implementation

**Làm gì:** Implement serializer sử dụng Newtonsoft.Json.

**Tại sao:** Newtonsoft.Json mature hơn, feature-rich hơn System.Text.Json. Support nhiều scenarios phức tạp.

**File:** `src/Infrastructure/Infrastructure/Common/Services/NewtonSoftService.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace ECO.WebApi.Infrastructure.Common.Services;

/// <summary>
/// JSON serializer implementation sử dụng Newtonsoft.Json
/// </summary>
public class NewtonSoftService : ISerializerService
{
    /// <summary>
    /// Deserialize JSON string thành object
    /// </summary>
    public T Deserialize<T>(string text)
    {
        return JsonConvert.DeserializeObject<T>(text)!;
  }

    /// <summary>
    /// Serialize object thành JSON string với custom settings
    /// </summary>
    public string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
  {
     // CamelCase property names (firstName thay vì FirstName)
 ContractResolver = new CamelCasePropertyNamesContractResolver(),
        
   // Ignore null values (không serialize properties null)
    NullValueHandling = NullValueHandling.Ignore,
      
            // Enum as string (thay vì number)
            Converters = new List<JsonConverter>
  {
new StringEnumConverter { CamelCaseText = true }
            }
  });
    }

    /// <summary>
    /// Serialize object thành JSON string với type cụ thể
    /// </summary>
    public string Serialize<T>(T obj, Type type)
{
  return JsonConvert.SerializeObject(obj, type, new JsonSerializerSettings());
    }
}
```

**Giải thích JsonSerializerSettings:**

**CamelCasePropertyNamesContractResolver:**
- Property names → camelCase: `firstName` thay vì `FirstName`
- Chuẩn JSON API

**NullValueHandling.Ignore:**
- Không serialize properties null
- Giảm response size
- Cleaner JSON

**StringEnumConverter:**
- Enum as string: `"active"` thay vì `1`
- Dễ đọc, dễ debug
- API-friendly

**Example:**
```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public ProductStatus Status { get; set; }  // Enum
    public string? Description { get; set; }   // Nullable
}

// Input
var product = new Product 
{ 
    Id = Guid.NewGuid(), 
    Name = "iPhone", 
    Status = ProductStatus.Active,
    Description = null 
};

// Serialize
var json = _serializer.Serialize(product);

// Output
{"id":"...","name":"iPhone","status":"active"}
// (description bị bỏ vì null, status là "active" thay vì 1)
```

**Lợi ích:**
- ✓ API-friendly format
- ✓ Smaller response size
- ✓ Human-readable
- ✓ Easy debugging

---

### Bước 4.3: Register Serializer Service

**Làm gì:** Register serializer service vào DI container.

**File:** `src/Infrastructure/Infrastructure/Common/Startup.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Infrastructure.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Common;

internal static class Startup
{
    /// <summary>
  /// Register common services
    /// </summary>
  internal static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        // Register Serializer as Transient
        services.AddTransient<ISerializerService, NewtonSoftService>();

        return services;
    }
}
```

**Giải thích:**
- `Transient` lifetime → lightweight, stateless service
- Extension method pattern để modular registration

---

## 5. Event Publisher Service

### Bước 5.1: IEvent Marker Interface

**Làm gì:** Marker interface cho tất cả domain events.

**Tại sao:** Đánh dấu class là domain event, support generic event handling.

**⚠️ Lưu ý quan trọng:**
- Trong BUILD_09, `IEvent` đã được **di chuyển** từ `Shared.Events` sang `Domain.Common.Contracts`
- Nếu bạn đã tạo `IEvent` trong Shared layer (BUILD_02 cũ), xem [BUILD_09 Section 12](BUILD_09_Domain_Base_Entities.md#12-migration-note) để migrate

**File:** `src/Core/Domain/Common/Contracts/IEvent.cs` (đã tạo trong BUILD_09)

```csharp
namespace ECO.WebApi.Domain.Common.Contracts;

/// <summary>
/// Marker interface cho tất cả domain events
/// Domain events represent something that happened in the domain
/// </summary>
public interface IEvent
{
}
```

**Giải thích:**
- Marker interface → không có methods
- Tất cả domain events phải implement
- **Ở Domain layer** (BUILD_09 đã di chuyển từ Shared) → Pure domain concept

**Tại sao trong Domain layer:**
- Domain events là domain concept (business logic)
- Không phụ thuộc infrastructure
- Follow DDD principles

**Migration from BUILD_02:**
- BUILD_02 cũ có `IEvent` trong `Shared.Events` (deprecated)
- BUILD_09 di chuyển sang `Domain.Common.Contracts` (correct)
- Update imports: `using ECO.WebApi.Domain.Common.Contracts;`

---

### Bước 5.2: EventNotification Wrapper

**Làm gì:** Wrapper class để wrap IEvent thành INotification (MediatR).

**Tại sao:** Domain events (`IEvent`) không phụ thuộc MediatR. Wrapper để publish qua MediatR.

**File:** `src/Core/Application/Common/Events/EventNotification.cs`

```csharp
using ECO.WebApi.Domain.Common.Contracts; // ⚠️ Updated from Shared.Events
using MediatR;

namespace ECO.WebApi.Application.Common.Events;

/// <summary>
/// Wrapper class để wrap IEvent thành INotification (MediatR)
/// Giữ cho Domain layer không phụ thuộc MediatR
/// </summary>
public class EventNotification<TEvent> : INotification
    where TEvent : IEvent
{
    public EventNotification(TEvent @event) => Event = @event;

    /// <summary>
    /// Domain event được wrap
    /// </summary>
    public TEvent Event { get; }
}
```

**Giải thích:**
- `INotification` → MediatR notification interface
- Wrap `IEvent` thành `INotification` để publish qua MediatR
- Generic class → support any event type

**Tại sao cần wrapper:**
- Domain events (`IEvent`) **không phụ thuộc** MediatR → Clean Architecture
- MediatR cần `INotification` để publish → Infrastructure concern
- Wrapper tách biệt Domain và Infrastructure → Separation of concerns

**Design pattern:** Adapter Pattern

---

### Bước 5.3: IEventPublisher Interface

**Làm gì:** Interface để publish domain events.

**Tại sao:** Application layer cần publish events, nhưng không biết implementation (MediatR).

**File:** `src/Core/Application/Common/Events/IEventPublisher.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Domain.Common.Contracts; // ⚠️ Updated from Shared.Events

namespace ECO.WebApi.Application.Common.Events;

/// <summary>
/// Interface để publish domain events
/// Implementation sẽ dùng MediatR để dispatch events đến handlers
/// </summary>
public interface IEventPublisher : ITransientService
{
    /// <summary>
    /// Publish domain event
    /// </summary>
    Task PublishAsync(IEvent @event);
}
```

**Giải thích:**
- `Transient` lifetime
- Accept `IEvent` (domain abstraction)
- Async method → await handlers

**Lợi ích:**
- ✓ Application layer không phụ thuộc MediatR
- ✓ Dễ mock cho testing
- ✓ Dễ thay đổi implementation

---

### Bước 5.4: EventPublisher Implementation

**Làm gì:** Implement EventPublisher sử dụng MediatR để dispatch events.

**Tại sao:** MediatR handle event routing và invocation. Chúng ta chỉ cần wrap events.

**File:** `src/Infrastructure/Infrastructure/Common/Events/EventPublisher.cs`

```csharp
using ECO.WebApi.Application.Common.Events;
using ECO.WebApi.Domain.Common.Contracts; // ⚠️ Updated from Shared.Events
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Infrastructure.Common.Events;

/// <summary>
/// Implementation của IEventPublisher sử dụng MediatR
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IPublisher _mediator;

    public EventPublisher(ILogger<EventPublisher> logger, IPublisher mediator) =>
(_logger, _mediator) = (logger, mediator);

    /// <summary>
    /// Publish domain event qua MediatR
    /// </summary>
  public Task PublishAsync(IEvent @event)
    {
      // Log event type để tracking
        _logger.LogInformation("Publishing Event: {EventType}", @event.GetType().Name);
        
        // Wrap event thành EventNotification và publish qua MediatR
     return _mediator.Publish(CreateEventNotification(@event));
    }

    /// <summary>
    /// Create EventNotification&lt;TEvent&gt; từ IEvent bằng reflection
    /// Vì runtime type, không thể dùng generic compile-time
    /// </summary>
    private static INotification CreateEventNotification(IEvent @event)
    {
   // Step 1: Lấy runtime type của event (ví dụ: ProductCreatedEvent)
    var eventType = @event.GetType();
  
    // Step 2: Tạo generic type EventNotification<ProductCreatedEvent>
        var notificationType = typeof(EventNotification<>).MakeGenericType(eventType);
        
        // Step 3: Create instance: new EventNotification<ProductCreatedEvent>(event)
     var instance = Activator.CreateInstance(notificationType, @event);

        // Step 4: Cast về INotification
        return (INotification)instance!;
    }
}
```

**Giải thích Reflection Magic:**

```csharp
// Input: ProductCreatedEvent (implements IEvent)
var @event = new ProductCreatedEvent(product);

// Step 1: Get runtime type
var eventType = @event.GetType(); 
// Result: typeof(ProductCreatedEvent)

// Step 2: Make generic type
var notificationType = typeof(EventNotification<>).MakeGenericType(eventType);
// Result: typeof(EventNotification<ProductCreatedEvent>)

// Step 3: Create instance with constructor parameter
var instance = Activator.CreateInstance(notificationType, @event);
// Result: new EventNotification<ProductCreatedEvent>(event)

// Step 4: Cast to INotification
return (INotification)instance;
// MediatR accepts INotification
```

**Tại sao cần reflection:**
- `IEvent` là interface → không biết concrete type compile-time
- Runtime type → phải dùng reflection để tạo `EventNotification<T>`
- Generic type argument cần runtime type information

**Performance consideration:**
- Reflection có overhead nhưng acceptable
- Events không publish thường xuyên như queries
- Tradeoff để giữ clean architecture

---

### Bước 5.5: EventNotificationHandler Base Class

**Làm gì:** Base class để dễ dàng tạo event handlers.

**Tại sao:** Auto unwrap EventNotification, handlers chỉ cần handle domain event.

**File:** `src/Core/Application/Common/Events/IEventNotificationHandler.cs`

```csharp
using ECO.WebApi.Domain.Common.Contracts; // ⚠️ Updated from Shared.Events
using MediatR;

namespace ECO.WebApi.Application.Common.Events;

/// <summary>
/// Interface cho event notification handlers (shorthand)
/// </summary>
public interface IEventNotificationHandler<TEvent> : INotificationHandler<EventNotification<TEvent>>
    where TEvent : IEvent
{
}

/// <summary>
/// Abstract base class cho event notification handlers
/// Auto unwrap EventNotification để handlers chỉ cần handle domain event
/// </summary>
public abstract class EventNotificationHandler<TEvent> : INotificationHandler<EventNotification<TEvent>>
  where TEvent : IEvent
{
    /// <summary>
    /// Handle EventNotification (wrapper) - auto called bởi MediatR
    /// </summary>
    public Task Handle(EventNotification<TEvent> notification, CancellationToken cancellationToken) =>
   Handle(notification.Event, cancellationToken);

    /// <summary>
    /// Handle domain event (phải implement trong derived class)
    /// </summary>
 public abstract Task Handle(TEvent @event, CancellationToken cancellationToken);
}
```

**Giải thích:**

**Interface shorthand:**
- `IEventNotificationHandler<ProductCreatedEvent>` thay vì `INotificationHandler<EventNotification<ProductCreatedEvent>>`
- Gọn hơn, dễ đọc hơn

**Abstract class:**
- Auto unwrap `EventNotification` → handler chỉ cần handle `TEvent`
- Abstract method → force derived classes implement
- Template Method pattern

**Usage example:**
```csharp
// ❌ Không dùng base class - phải unwrap manually
public class ProductCreatedHandler : INotificationHandler<EventNotification<ProductCreatedEvent>>
{
    public Task Handle(EventNotification<ProductCreatedEvent> notification, ...)
    {
        var @event = notification.Event; // Unwrap manually
 // Handle event logic
    }
}

// ✓ Dùng base class - auto unwrap
public class ProductCreatedHandler : EventNotificationHandler<ProductCreatedEvent>
{
    public override Task Handle(ProductCreatedEvent @event, ...)
  {
     // Handle event directly - đã unwrap rồi
        _logger.LogInformation("Product created: {Name}", @event.Product.Name);
        return Task.CompletedTask;
    }
}
```

**Lợi ích:**
- ✓ Code gọn hơn
- ✓ Ít boilerplate
- ✓ Focus vào business logic

---

### Bước 5.6: Register Event Publisher

**Làm gì:** Register EventPublisher vào DI container.

**File:** `src/Infrastructure/Infrastructure/Common/Startup.cs`

```csharp
using ECO.WebApi.Application.Common.Events;
using ECO.WebApi.Infrastructure.Common.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Common;

internal static class Startup
{
    internal static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        // Serializer
        services.AddTransient<ISerializerService, NewtonSoftService>();
        
      // Event Publisher
        services.AddTransient<IEventPublisher, EventPublisher>();

   return services;
  }
}
```

**Giải thích:**
- `Transient` lifetime → stateless service
- MediatR auto-scan và register event handlers

---

## 6. Update Infrastructure Startup

### Bước 6.1: Consolidate All Services

**Làm gì:** Update Infrastructure Startup để register tất cả services.

**Tại sao:** Centralized registration, dễ maintain.

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Auth;
using ECO.WebApi.Infrastructure.Common;
using ECO.WebApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure;

public static class Startup
{
    /// <summary>
    /// Add all infrastructure services
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
  return services
       // Persistence (DbContext, Repositories)
            .AddPersistence()
            
  // CurrentUser service
    .AddCurrentUser()
  
   // Common services (Serializer, EventPublisher)
        .AddCommonServices()
  
            // Routing
            .AddRouting(options => options.LowercaseUrls = true);
    }

    /// <summary>
    /// Use infrastructure middleware
    /// </summary>
 public static IApplicationBuilder UseInfrastructure(
 this IApplicationBuilder builder,
        IConfiguration config)
    {
      return builder
            .UseRouting()
       
      // CurrentUser middleware - SAU UseRouting, TRƯỚC UseAuthentication
     .UseCurrentUserMiddleware()
            
        .UseHttpsRedirection();
    }
}
```

**⚠️ Lưu ý thứ tự middleware:**
```
1. UseRouting()
2. UseCurrentUserMiddleware()  → Set current user
3. UseAuthentication()   → Will add in BUILD_15
4. UseAuthorization()           → Will add in BUILD_17
5. MapControllers()
```

**Giải thích:**
- Fluent interface pattern (.AddX().AddY())
- Modular registration
- Clear middleware order

---

## 7. Testing

### Bước 7.1: Test CurrentUser Service

**Create test handler:**

**File:** `src/Core/Application/Identity/Users/GetMyProfileRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using MediatR;

namespace ECO.WebApi.Application.Identity.Users;

public class GetMyProfileRequest : IRequest<UserDetailDto> { }

public class GetMyProfileHandler : IRequestHandler<GetMyProfileRequest, UserDetailDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserService _userService;

    public GetMyProfileHandler(ICurrentUser currentUser, IUserService userService)
    {
        _currentUser = currentUser;
        _userService = userService;
    }

    public async Task<UserDetailDto> Handle(GetMyProfileRequest request, CancellationToken ct)
    {
     // Lấy current user info từ JWT token
        var userId = _currentUser.GetUserId();
        var email = _currentUser.GetUserEmail();
        var isAuthenticated = _currentUser.IsAuthenticated();

        // Get user from database
        var user = await _userService.GetAsync(userId.ToString(), ct);
        
        return user;
    }
}
```

**Test với curl:**
```bash
# Step 1: Login để lấy token
curl -X POST https://localhost:7001/api/tokens \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@root.com",
    "password": "123Pa$$word!"
  }'

# Step 2: Get token from response, then call API
curl -X GET https://localhost:7001/api/users/me \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Expected response:**
```json
{
  "id": "xxx-xxx-xxx",
  "firstName": "Admin",
  "lastName": "Root",
  "email": "admin@root.com"
}
```

---

### Bước 7.2: Test Serializer Service

**Create test:**
```csharp
public class SerializerTest
{
  private readonly ISerializerService _serializer;

    public void Test()
    {
var product = new Product
   {
   Id = Guid.NewGuid(),
            Name = "Test Product",
            Price = 100,
     Status = ProductStatus.Active,
    Description = null
        };

        // Serialize
        var json = _serializer.Serialize(product);
        Console.WriteLine(json);
        // Output: {"id":"...","name":"Test Product","price":100,"status":"active"}

        // Deserialize
        var deserialized = _serializer.Deserialize<Product>(json);
        Assert.Equal(product.Id, deserialized.Id);
        Assert.Equal(product.Name, deserialized.Name);
    }
}
```

---

### Bước 7.3: Test Event Publisher

**Create domain event:**
```csharp
// File: src/Core/Domain/Catalog/Events/ProductCreatedEvent.cs
using ECO.WebApi.Domain.Common.Contracts; // ⚠️ Updated: IEvent now in Domain.Common.Contracts

namespace ECO.WebApi.Domain.Catalog.Events;

public class ProductCreatedEvent : DomainEvent // ⚠️ Extends DomainEvent (from BUILD_09)
{
    public Product Product { get; }

    public ProductCreatedEvent(Product product)
{
        Product = product;
 }
}
```

**⚠️ Alternative using BUILD_09 Static Factory Pattern:**
```csharp
// Option 2: Use EntityCreatedEvent generic (recommended from BUILD_09)
using ECO.WebApi.Domain.Common.Events;

// In handler - no need custom event class
var createdEvent = EntityCreatedEvent.WithEntity(product);
await _eventPublisher.PublishAsync(createdEvent);
```

**Create event handler:**
```csharp
// File: src/Core/Application/Catalog/Products/EventHandlers/ProductCreatedEventHandler.cs
using ECO.WebApi.Application.Common.Events;
using ECO.WebApi.Domain.Catalog.Events;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Application.Catalog.Products.EventHandlers;

public class ProductCreatedEventHandler : EventNotificationHandler<ProductCreatedEvent>
{
    private readonly ILogger<ProductCreatedEventHandler> _logger;

    public ProductCreatedEventHandler(ILogger<ProductCreatedEventHandler> logger)
 {
        _logger = logger;
    }

  public override Task Handle(ProductCreatedEvent @event, CancellationToken ct)
  {
        _logger.LogInformation("Product created: {ProductId} - {ProductName}",
      @event.Product.Id,
       @event.Product.Name);

    // TODO: Send email notification
      // TODO: Update cache
     // TODO: Send webhook

   return Task.CompletedTask;
    }
}
```

**Publish event trong handler:**
```csharp
public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    private readonly IRepository<Product> _repository;
    private readonly IEventPublisher _eventPublisher;

public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
        var product = Product.Create(request.Name, request.Price);
        
        await _repository.AddAsync(product, ct);
        await _repository.SaveChangesAsync(ct);
    
        // Publish event SAU KHI save
        await _eventPublisher.PublishAsync(new ProductCreatedEvent(product));
   
        return product.Id;
    }
}
```

**Expected log:**
```
info: Publishing Event: ProductCreatedEvent
info: Product created: a1b2c3d4-e5f6-... - iPhone 15
```

---
