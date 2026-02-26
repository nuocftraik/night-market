# BUILD_29: Notifications Module - Real-time Notifications với SignalR

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** BUILD_28 (Catalog Module) đã hoàn thành

Tài liệu này hướng dẫn xây dựng **Notifications Module** - Hệ thống thông báo real-time với SignalR, theo **theoretical design tốt nhất**.

---

## 1. Overview

**Làm gì:** Xây dựng hệ thống notifications với SignalR cho real-time push notifications và in-app notification center.

**Tại sao cần:**
- ✅ **Real-time Updates:** Push notifications ngay lập tức đến users
- ✅ **In-App Notification Center:** Lưu trữ và quản lý notifications
- ✅ **Multi-Channel:** Web (SignalR), Email, SMS (future)
- ✅ **Event-Driven:** Tự động trigger từ domain events
- ✅ **Scalable:** SignalR supports horizontal scaling với Redis backplane

**Research sources:**
- SignalR documentation (Microsoft)
- Real-time notification patterns (Firebase, Pusher)
- Notification design patterns (Martin Fowler)

**Trong bước này chúng ta sẽ:**
- ✅ Setup SignalR Hub
- ✅ Tạo Notification entity (Domain)
- ✅ Tạo NotificationService (Application)
- ✅ Event Handlers (tự động gửi notifications từ domain events)
- ✅ NotificationController (API endpoints)
- ✅ Client integration examples

**Real-world example:**
```csharp
// Domain event triggers notification
public class ProductLowStockEventHandler : INotificationHandler<ProductLowStockEvent>
{
    private readonly INotificationService _notificationService;

    public async Task Handle(ProductLowStockEvent notification, CancellationToken ct)
    {
        // Send real-time notification to inventory managers
        await _notificationService.SendToRoleAsync(
    role: "InventoryManager",
      title: "Low Stock Alert",
   message: $"{notification.ProductName} is low on stock ({notification.CurrentStock} remaining)",
    type: NotificationType.Warning
        );
    }
}

// User receives notification instantly via SignalR
// Notification also saved to database for later viewing
```

---

## 2. Add Required Packages

### Bước 2.1: SignalR Packages

**File:** `src/Host/Host/Host.csproj`

```xml
<ItemGroup>
    <!-- SignalR for real-time communication -->
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="8.0.0" />
</ItemGroup>
```

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- SignalR client (for testing) -->
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.0" />
    
    <!-- Redis backplane (for scaling SignalR across multiple servers) -->
    <PackageReference Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="8.0.0" />
</ItemGroup>
```

**Giải thích packages:**
- `Microsoft.AspNetCore.SignalR`: Core SignalR library
- `Microsoft.AspNetCore.SignalR.Client`: Client SDK (cho testing)
- `Microsoft.AspNetCore.SignalR.StackExchangeRedis`: Redis backplane cho horizontal scaling

---

## 3. Domain Layer

### Bước 3.1: Notification Entity

**File:** `src/Core/Domain/Notifications/Notification.cs`

```csharp
using ECO.WebApi.Domain.Common.Contracts;

namespace ECO.WebApi.Domain.Notifications;

/// <summary>
/// Notification entity - stores all notifications sent to users
/// </summary>
public sealed class Notification : AuditableEntity, IAggregateRoot
{
    /// <summary>
    /// Target user ID (null = broadcast to all users)
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// Target role (null = specific user, not null = all users with this role)
    /// </summary>
    public string? TargetRole { get; private set; }

    /// <summary>
    /// Notification title
    /// </summary>
    public string Title { get; private set; } = default!;

    /// <summary>
    /// Notification message/content
    /// </summary>
    public string Message { get; private set; } = default!;

    /// <summary>
    /// Notification type (Info, Success, Warning, Error)
    /// </summary>
    public NotificationType Type { get; private set; }

    /// <summary>
    /// Reference entity type (e.g., "Product", "Order")
    /// </summary>
    public string? ReferenceType { get; private set; }

    /// <summary>
    /// Reference entity ID
    /// </summary>
    public Guid? ReferenceId { get; private set; }

    /// <summary>
    /// Action URL (where to navigate when clicked)
    /// </summary>
    public string? ActionUrl { get; private set; }

    /// <summary>
    /// Is notification read
    /// </summary>
    public bool IsRead { get; private set; }

    /// <summary>
    /// When notification was read
    /// </summary>
    public DateTime? ReadOn { get; private set; }

    /// <summary>
    /// Is notification sent successfully
    /// </summary>
    public bool IsSent { get; private set; }

    /// <summary>
    /// When notification was sent
    /// </summary>
    public DateTime? SentOn { get; private set; }

    // EF Core constructor
    private Notification() { }

    // ==================== Factory Methods ====================

    /// <summary>
    /// Create notification for specific user
    /// </summary>
    public static Notification CreateForUser(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
     Guid? referenceId = null,
        string? actionUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
       throw new ArgumentException("Title is required", nameof(title));

if (string.IsNullOrWhiteSpace(message))
   throw new ArgumentException("Message is required", nameof(message));

  var notification = new Notification
        {
  UserId = userId,
            Title = title,
            Message = message,
      Type = type,
    ReferenceType = referenceType,
            ReferenceId = referenceId,
ActionUrl = actionUrl,
        IsRead = false,
            IsSent = false
  };

        return notification;
    }

    /// <summary>
    /// Create notification for role (broadcast to all users with this role)
    /// </summary>
    public static Notification CreateForRole(
        string role,
        string title,
        string message,
        NotificationType type,
  string? referenceType = null,
        Guid? referenceId = null,
    string? actionUrl = null)
    {
        if (string.IsNullOrWhiteSpace(role))
    throw new ArgumentException("Role is required", nameof(role));

        if (string.IsNullOrWhiteSpace(title))
     throw new ArgumentException("Title is required", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));

        var notification = new Notification
        {
            TargetRole = role,
            Title = title,
            Message = message,
            Type = type,
            ReferenceType = referenceType,
        ReferenceId = referenceId,
        ActionUrl = actionUrl,
      IsRead = false,
      IsSent = false
      };

        return notification;
    }

    /// <summary>
    /// Create broadcast notification (to all users)
    /// </summary>
    public static Notification CreateBroadcast(
        string title,
    string message,
        NotificationType type,
    string? actionUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

    if (string.IsNullOrWhiteSpace(message))
    throw new ArgumentException("Message is required", nameof(message));

        var notification = new Notification
    {
  Title = title,
   Message = message,
       Type = type,
 ActionUrl = actionUrl,
        IsRead = false,
            IsSent = false
};

     return notification;
    }

    // ==================== Business Logic Methods ====================

    /// <summary>
    /// Mark notification as read
    /// </summary>
    public void MarkAsRead()
    {
        if (IsRead)
    return;

  IsRead = true;
        ReadOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Mark notification as unread
    /// </summary>
    public void MarkAsUnread()
    {
        IsRead = false;
        ReadOn = null;
    }

    /// <summary>
    /// Mark notification as sent
    /// </summary>
    public void MarkAsSent()
    {
        if (IsSent)
         return;

        IsSent = true;
 SentOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if notification is for specific user
    /// </summary>
    public bool IsForUser(Guid userId) => UserId.HasValue && UserId.Value == userId;

    /// <summary>
    /// Check if notification is for role
    /// </summary>
    public bool IsForRole() => !string.IsNullOrWhiteSpace(TargetRole);

    /// <summary>
    /// Check if notification is broadcast
    /// </summary>
  public bool IsBroadcast() => !UserId.HasValue && string.IsNullOrWhiteSpace(TargetRole);
}
```

**Giải thích:**

**1. Target Types:**
- **Specific User**: `UserId` not null → notification for one user
- **Role-based**: `TargetRole` not null → notification for all users with role
- **Broadcast**: Both null → notification for all users

**2. Reference Linking:**
- `ReferenceType` + `ReferenceId`: Link notification to entity (Product, Order, etc.)
- `ActionUrl`: Where to navigate when notification clicked

**3. Status Tracking:**
- `IsRead/ReadOn`: Track if user read notification
- `IsSent/SentOn`: Track if notification sent successfully via SignalR

---

### Bước 3.2: NotificationType Enum

**File:** `src/Core/Domain/Notifications/NotificationType.cs`

```csharp
namespace ECO.WebApi.Domain.Notifications;

/// <summary>
/// Notification type for UI styling
/// </summary>
public enum NotificationType
{
/// <summary>
    /// Informational notification (blue)
    /// </summary>
    Info = 1,

    /// <summary>
 /// Success notification (green)
    /// </summary>
    Success = 2,

    /// <summary>
    /// Warning notification (yellow/orange)
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Error notification (red)
    /// </summary>
    Error = 4
}
```

---

## 4. Application Layer

### Bước 4.1: Notification DTOs

**File:** `src/Core/Application/Notifications/NotificationDto.cs`

```csharp
namespace ECO.WebApi.Application.Notifications;

/// <summary>
/// Notification DTO for API responses
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? TargetRole { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public NotificationType Type { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadOn { get; set; }
    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// Simplified notification DTO for real-time push
/// </summary>
public class NotificationPushDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public NotificationType Type { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedOn { get; set; }
}
```

---

### Bước 4.2: INotificationService Interface

**File:** `src/Core/Application/Notifications/INotificationService.cs`

```csharp
namespace ECO.WebApi.Application.Notifications;

/// <summary>
/// Notification service interface
/// </summary>
public interface INotificationService : ITransientService
{
/// <summary>
    /// Send notification to specific user
    /// </summary>
    Task<Guid> SendToUserAsync(
        Guid userId,
        string title,
        string message,
      NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null,
        string? actionUrl = null,
   CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification to all users with specific role
    /// </summary>
 Task<Guid> SendToRoleAsync(
        string role,
        string title,
     string message,
        NotificationType type,
    string? referenceType = null,
        Guid? referenceId = null,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send broadcast notification to all users
    /// </summary>
    Task<Guid> SendBroadcastAsync(
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark notification as read
    /// </summary>
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark all user notifications as read
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete notification
    /// </summary>
    Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user notifications with pagination
    /// </summary>
    Task<PaginatedResult<NotificationDto>> GetUserNotificationsAsync(
    Guid userId,
  int pageNumber,
        int pageSize,
        bool? isRead = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get unread notification count for user
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

---

### Bước 4.3: NotificationService Implementation

**File:** `src/Infrastructure/Notifications/NotificationService.cs`

```csharp
using ECO.WebApi.Application.Notifications;
using ECO.WebApi.Domain.Notifications;
using ECO.WebApi.Infrastructure.Notifications.Hubs;
using Microsoft.AspNetCore.SignalR;
using Mapster;

namespace ECO.WebApi.Infrastructure.Notifications;

/// <summary>
/// Notification service implementation
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _repository;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ICurrentUser _currentUser;

    public NotificationService(
        IRepository<Notification> repository,
        IHubContext<NotificationHub> hubContext,
      ICurrentUser currentUser)
    {
        _repository = repository;
        _hubContext = hubContext;
        _currentUser = currentUser;
    }

    public async Task<Guid> SendToUserAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null,
  string? actionUrl = null,
    CancellationToken cancellationToken = default)
    {
        // Create notification entity
     var notification = Notification.CreateForUser(
            userId, title, message, type, referenceType, referenceId, actionUrl);

 // Save to database
        await _repository.AddAsync(notification, cancellationToken);

        // Send via SignalR
        var pushDto = notification.Adapt<NotificationPushDto>();
  await _hubContext.Clients.User(userId.ToString())
            .SendAsync("ReceiveNotification", pushDto, cancellationToken);

   // Mark as sent
      notification.MarkAsSent();
        await _repository.UpdateAsync(notification, cancellationToken);

  return notification.Id;
    }

    public async Task<Guid> SendToRoleAsync(
        string role,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
    Guid? referenceId = null,
        string? actionUrl = null,
        CancellationToken cancellationToken = default)
    {
        // Create notification entity
      var notification = Notification.CreateForRole(
   role, title, message, type, referenceType, referenceId, actionUrl);

        // Save to database
        await _repository.AddAsync(notification, cancellationToken);

        // Send via SignalR to role group
        var pushDto = notification.Adapt<NotificationPushDto>();
     await _hubContext.Clients.Group(role)
  .SendAsync("ReceiveNotification", pushDto, cancellationToken);

   // Mark as sent
   notification.MarkAsSent();
  await _repository.UpdateAsync(notification, cancellationToken);

      return notification.Id;
    }

    public async Task<Guid> SendBroadcastAsync(
        string title,
        string message,
        NotificationType type,
  string? actionUrl = null,
 CancellationToken cancellationToken = default)
    {
        // Create notification entity
        var notification = Notification.CreateBroadcast(title, message, type, actionUrl);

        // Save to database
        await _repository.AddAsync(notification, cancellationToken);

     // Send via SignalR to all connected clients
        var pushDto = notification.Adapt<NotificationPushDto>();
        await _hubContext.Clients.All
      .SendAsync("ReceiveNotification", pushDto, cancellationToken);

        // Mark as sent
        notification.MarkAsSent();
        await _repository.UpdateAsync(notification, cancellationToken);

        return notification.Id;
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
    throw new NotFoundException($"Notification {notificationId} not found");

        notification.MarkAsRead();
        await _repository.UpdateAsync(notification, cancellationToken);
  }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spec = new UserUnreadNotificationsSpec(userId);
      var notifications = await _repository.ListAsync(spec, cancellationToken);

        foreach (var notification in notifications)
        {
 notification.MarkAsRead();
        }

        await _repository.UpdateRangeAsync(notifications, cancellationToken);
    }

    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
 if (notification == null)
            throw new NotFoundException($"Notification {notificationId} not found");

        await _repository.DeleteAsync(notification, cancellationToken);
    }

    public async Task<PaginatedResult<NotificationDto>> GetUserNotificationsAsync(
  Guid userId,
        int pageNumber,
   int pageSize,
      bool? isRead = null,
      CancellationToken cancellationToken = default)
    {
        var spec = new UserNotificationsSpec(userId, pageNumber, pageSize, isRead);
        var notifications = await _repository.ListAsync(spec, cancellationToken);
        var totalCount = await _repository.CountAsync(spec, cancellationToken);

        var dtos = notifications.Adapt<List<NotificationDto>>();

        return new PaginatedResult<NotificationDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spec = new UserUnreadNotificationsSpec(userId);
        return await _repository.CountAsync(spec, cancellationToken);
    }
}
```

---

## 5. SignalR Hub

### Bước 5.1: NotificationHub

**File:** `src/Infrastructure/Notifications/Hubs/NotificationHub.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECO.WebApi.Infrastructure.Notifications.Hubs;

/// <summary>
/// SignalR Hub for real-time notifications
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ICurrentUser _currentUser;

    public NotificationHub(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Called when client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // Get current user ID
        var userId = _currentUser.GetUserId();

        // Add connection to user group (for targeting specific users)
     if (userId != Guid.Empty)
        {
      await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());
        }

      // Add connection to role groups (for role-based notifications)
        var roles = _currentUser.GetRoles();
        foreach (var role in roles)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, role);
      }

        await base.OnConnectedAsync();
    }

 /// <summary>
    /// Called when client disconnects
/// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
 // Cleanup is automatic when connection closes
    await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client method to mark notification as read
    /// </summary>
    public async Task MarkAsRead(Guid notificationId)
{
        // Can trigger server-side logic here if needed
      await Clients.Caller.SendAsync("NotificationMarkedAsRead", notificationId);
    }
}
```

**Giải thích:**

**1. Authorization:**
- `[Authorize]` - Only authenticated users can connect

**2. Group Management:**
- User group: `userId.ToString()` - for targeting specific user
- Role groups: User's roles - for role-based notifications

**3. Client Methods:**
- `MarkAsRead()` - Called from client to mark notification as read

---

## 6. Specifications

### Bước 6.1: Notification Specifications

**File:** `src/Core/Application/Notifications/NotificationSpecifications.cs`

```csharp
using Ardalis.Specification;

namespace ECO.WebApi.Application.Notifications;

/// <summary>
/// Specification to get user notifications with pagination
/// </summary>
public class UserNotificationsSpec : Specification<Notification>
{
    public UserNotificationsSpec(Guid userId, int pageNumber, int pageSize, bool? isRead = null)
    {
        Query
       .Where(n => n.UserId == userId || n.TargetRole != null || (n.UserId == null && n.TargetRole == null))
       .OrderByDescending(n => n.CreatedOn)
         .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        if (isRead.HasValue)
        {
            Query.Where(n => n.IsRead == isRead.Value);
  }
  }
}

/// <summary>
/// Specification to get unread notifications for user
/// </summary>
public class UserUnreadNotificationsSpec : Specification<Notification>
{
    public UserUnreadNotificationsSpec(Guid userId)
    {
        Query.Where(n => 
        !n.IsRead && 
   (n.UserId == userId || n.TargetRole != null || (n.UserId == null && n.TargetRole == null)));
    }
}
```

---

## 7. Infrastructure Setup

### Bước 7.1: SignalR Configuration

**File:** `src/Infrastructure/Notifications/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Notifications.Hubs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Notifications;

public static class Startup
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration config)
    {
        // Add SignalR
        var signalRBuilder = services.AddSignalR();

        // Add Redis backplane for scaling (optional)
        var redisConnection = config.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
   {
            signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
          {
        options.Configuration.ChannelPrefix = "ECO.Notifications";
            });
      }

        return services;
    }

    public static IApplicationBuilder UseNotifications(this IApplicationBuilder app)
    {
        // Map SignalR Hub
    app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<NotificationHub>("/hubs/notifications");
        });

     return app;
    }
}
```

---

### Bước 7.2: Register in Program.cs

**File:** `src/Host/Program.cs`

```csharp
// Add SignalR & Notifications
builder.Services.AddNotifications(builder.Configuration);

// ...

// Use SignalR
app.UseNotifications();
```

---

## 8. Event Handlers

### Bước 8.1: Product Event Handlers

**File:** `src/Infrastructure/Notifications/EventHandlers/ProductEventHandlers.cs`

```csharp
using ECO.WebApi.Application.Notifications;
using ECO.WebApi.Domain.Catalog.Events;
using MediatR;

namespace ECO.WebApi.Infrastructure.Notifications.EventHandlers;

/// <summary>
/// Send notification when product stock is low
/// </summary>
public class ProductLowStockNotificationHandler : INotificationHandler<ProductLowStockEvent>
{
    private readonly INotificationService _notificationService;

    public ProductLowStockNotificationHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Handle(ProductLowStockEvent notification, CancellationToken ct)
    {
  await _notificationService.SendToRoleAsync(
          role: "InventoryManager",
 title: "Low Stock Alert",
   message: $"{notification.ProductName} is low on stock. Current: {notification.CurrentStock}, Threshold: {notification.LowStockThreshold}",
        type: NotificationType.Warning,
  referenceType: "Product",
            referenceId: notification.ProductId,
   actionUrl: $"/products/{notification.ProductId}",
            cancellationToken: ct
        );
  }
}

/// <summary>
/// Send notification when product is out of stock
/// </summary>
public class ProductOutOfStockNotificationHandler : INotificationHandler<ProductOutOfStockEvent>
{
    private readonly INotificationService _notificationService;

    public ProductOutOfStockNotificationHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Handle(ProductOutOfStockEvent notification, CancellationToken ct)
    {
      await _notificationService.SendToRoleAsync(
            role: "InventoryManager",
            title: "Out of Stock",
     message: $"{notification.ProductName} is now out of stock!",
          type: NotificationType.Error,
    referenceType: "Product",
            referenceId: notification.ProductId,
   actionUrl: $"/products/{notification.ProductId}",
            cancellationToken: ct
        );
    }
}

/// <summary>
/// Send notification when new product is published
/// </summary>
public class ProductPublishedNotificationHandler : INotificationHandler<ProductPublishedEvent>
{
    private readonly INotificationService _notificationService;

    public ProductPublishedNotificationHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Handle(ProductPublishedEvent notification, CancellationToken ct)
    {
        // Broadcast to all users
    await _notificationService.SendBroadcastAsync(
      title: "New Product Available",
      message: $"Check out our new product: {notification.ProductName}",
            type: NotificationType.Info,
            actionUrl: $"/products/{notification.ProductId}",
         cancellationToken: ct
   );
    }
}
```

---

## 9. REST API Controller

### Bước 9.1: NotificationsController

**File:** `src/Host/Controllers/NotificationsController.cs`

```csharp
using ECO.WebApi.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

/// <summary>
/// Notifications API endpoints
/// </summary>
[Route("api/notifications")]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;
 private readonly ICurrentUser _currentUser;

    public NotificationsController(
      INotificationService notificationService,
 ICurrentUser currentUser)
    {
  _notificationService = notificationService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get current user's notifications
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int pageNumber = 1,
     [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null)
    {
   var userId = _currentUser.GetUserId();
  var result = await _notificationService.GetUserNotificationsAsync(
    userId, pageNumber, pageSize, isRead);

        return Ok(result);
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    [HttpGet("unread-count")]
 [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = _currentUser.GetUserId();
   var count = await _notificationService.GetUnreadCountAsync(userId);

        return Ok(count);
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _currentUser.GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// Delete notification
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _notificationService.DeleteAsync(id);
        return NoContent();
 }

    /// <summary>
/// Send test notification (Admin only)
    /// </summary>
    [HttpPost("test")]
    [MustHavePermission("Notifications.Send")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendTestNotification()
    {
        var userId = _currentUser.GetUserId();
        var id = await _notificationService.SendToUserAsync(
  userId,
        title: "Test Notification",
      message: "This is a test notification from the system",
         type: NotificationType.Info
        );

   return Ok(id);
    }
}
```

---

## 10. Client Integration

### Bước 10.1: JavaScript Client Example

```javascript
// Install SignalR client: npm install @microsoft/signalr

import * as signalR from "@microsoft/signalr";

// Create connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
  accessTokenFactory: () => localStorage.getItem("access_token")
    })
    .withAutomaticReconnect()
    .build();

// Handle incoming notifications
connection.on("ReceiveNotification", (notification) => {
    console.log("New notification:", notification);
    
    // Show toast notification
    showToast(notification.title, notification.message, notification.type);
    
    // Update notification badge
    updateNotificationBadge();
    
    // Add to notification list
    addNotificationToList(notification);
});

// Start connection
async function start() {
    try {
 await connection.start();
    console.log("SignalR Connected");
    } catch (err) {
console.error(err);
        setTimeout(start, 5000);
    }
}

connection.onclose(async () => {
    await start();
});

// Start the connection
start();

// Mark notification as read
async function markAsRead(notificationId) {
    await fetch(`/api/notifications/${notificationId}/read`, {
        method: "PUT",
        headers: {
   "Authorization": `Bearer ${localStorage.getItem("access_token")}`
 }
    });
}

// Get unread count
async function getUnreadCount() {
    const response = await fetch("/api/notifications/unread-count", {
        headers: {
        "Authorization": `Bearer ${localStorage.getItem("access_token")}`
        }
    });
    return await response.json();
}
```

---

## 11. Database Migration

### Bước 11.1: EF Core Configuration

**File:** `src/Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
 {
        builder.ToTable("Notifications", "Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId)
            .IsRequired(false);

        builder.Property(n => n.TargetRole)
     .HasMaxLength(100)
            .IsRequired(false);

    builder.Property(n => n.Title)
    .IsRequired()
      .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

  builder.Property(n => n.Type)
  .IsRequired();

        builder.Property(n => n.ReferenceType)
            .HasMaxLength(100)
     .IsRequired(false);

     builder.Property(n => n.ActionUrl)
         .HasMaxLength(500)
      .IsRequired(false);

        builder.Property(n => n.IsRead)
            .IsRequired()
     .HasDefaultValue(false);

        builder.Property(n => n.IsSent)
        .IsRequired()
     .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(n => n.UserId);
    builder.HasIndex(n => n.TargetRole);
        builder.HasIndex(n => n.IsRead);
        builder.HasIndex(n => n.CreatedOn);
    }
}
```

---

## 12. Summary

### ✅ BUILD_29 Complete:

**Domain Layer:**
- ✅ Notification entity (with user/role/broadcast targeting)
- ✅ NotificationType enum

**Application Layer:**
- ✅ INotificationService interface
- ✅ NotificationService implementation
- ✅ Notification DTOs
- ✅ Specifications (UserNotificationsSpec, UserUnreadNotificationsSpec)

**Infrastructure:**
- ✅ SignalR Hub (NotificationHub)
- ✅ Event Handlers (ProductLowStock, ProductOutOfStock, ProductPublished)
- ✅ EF Core Configuration

**Controllers:**
- ✅ NotificationsController (REST API)

**Client Integration:**
- ✅ JavaScript SignalR client example

### 📊 Complete Flow:

```
1. Domain Event → ProductLowStockEvent
   ↓
2. Event Handler → ProductLowStockNotificationHandler
   ↓
3. NotificationService.SendToRoleAsync()
   ↓
4. Create Notification entity → Save to database
   ↓
5. SignalR Hub → Send to role group
   ↓
6. Connected clients → Receive real-time notification
   ↓
7. Client displays → Toast notification
   ↓
8. User clicks → Mark as read via API
```

### 📁 File Structure:

```
ECO.WebApi/
├── Domain/Notifications/
│   ├── Notification.cs
│   └── NotificationType.cs
├── Application/Notifications/
│   ├── INotificationService.cs
│ ├── NotificationDto.cs
│   └── NotificationSpecifications.cs
├── Infrastructure/Notifications/
│   ├── NotificationService.cs
│   ├── Hubs/NotificationHub.cs
│   ├── EventHandlers/ProductEventHandlers.cs
│   └── Startup.cs
├── Infrastructure/Persistence/Configurations/
│   └── NotificationConfiguration.cs
└── Host/Controllers/
  └── NotificationsController.cs
```

---

## 13. Next Steps

**Tiếp theo:** [BUILD_30 - Payment Integration](BUILD_30_Payment_Integration.md)

Trong bước tiếp theo, chúng ta sẽ:
- ✅ VNPay payment gateway integration
- ✅ Payment flow (request → callback → verify)
- ✅ Payment status tracking
- ✅ Refund handling

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)

---

**Document Version:** 1.0 (Theoretical Design - SignalR Real-time Notifications)  
**Last Updated:** 2026-01-30  
**Note:** Complete implementation with SignalR, domain events integration, and client examples.
