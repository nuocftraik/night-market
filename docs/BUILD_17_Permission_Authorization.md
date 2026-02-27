# Permission-Based Authorization - Dynamic Permission System

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 16C (Function Service) đã hoàn thành

> [!IMPORTANT]
> **Implementation Notes (cập nhật sau khi implement):**
> - **Namespace:** `NightMarket.WebApi.*` và `NightMarket.Shared.*` (không phải `ECO.WebApi.*`)
> - **Constants:** `AppAction`, `AppFunction`, `AppPermission`, `AppClaims` (không phải `ECO*`)
> - **File names:** `AppPermission.cs`, `AppAction.cs`, `AppFunction.cs`, `AppClaims.cs` (không phải `ECOPermissions.cs`)
> - **TokenService:** Đã cập nhật `GetClaims()` → `GetClaimsAsync()` với async permission loading. Inject `IUserService` vào constructor
> - **UserService.Permission.cs:** Implemented as partial class, removed duplicate stub methods từ `UserService.cs`
> - **PermissionPolicyProvider:** Check `AppClaims.Permission` prefix (= `"permission"`) thay vì `"Permissions"` prefix

Tài liệu này hướng dẫn xây dựng Permission-Based Authorization System - Dynamic permission checks với ASP.NET Core Authorization.

---

## 1. Overview

**Làm gì:** Xây dựng Permission-Based Authorization System để protect API endpoints dựa trên dynamic permissions stored in database.

**Tại sao cần:**
- **Dynamic Authorization:** Permissions stored in database, not hardcoded
- **Fine-grained Access Control:** Check specific permissions (e.g., "Users.View", "Products.Create")
- **Declarative Security:** Use attributes `[MustHavePermission("Users", "View")]` on controllers
- **JWT-based Checks:** Permissions stored in JWT claims for fast authorization
- **Complete Permission Flow:** Function + Action + Role → JWT Claims → Authorization Handler

**Trong bước này chúng ta sẽ:**
- ✅ Tạo PermissionRequirement (IAuthorizationRequirement)
- ✅ Tạo PermissionAuthorizationHandler (check permissions from JWT claims)
- ✅ Tạo PermissionPolicyProvider (dynamic policy creation)
- ✅ Tạo MustHavePermissionAttribute (declarative attribute)
- ✅ Update TokenService để add permissions to JWT claims
- ✅ Update UserService.Permission.cs (GetPermissionsAsync, HasPermissionAsync)
- ✅ Register authorization services trong Startup
- ✅ Testing với protected endpoints

**Real-world example:**
```csharp
// Controller với permission protection
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    // Only users với "Users.View" permission có thể access
    [HttpGet]
    [MustHavePermission(AppAction.View, AppFunction.User)]
    public Task<List<UserDto>> GetAllAsync()
    {
        // Implementation
    }

    // Only users với "Users.Create" permission có thể access
    [HttpPost]
    [MustHavePermission(AppAction.Create, AppFunction.User)]
    public Task<string> CreateAsync(CreateUserRequest request)
    {
        // Implementation
    }
}

// Authorization Flow:
// 1. User logs in → TokenService generates JWT với permissions in claims
// 2. User calls API với JWT token
// 3. PermissionPolicyProvider creates policy "Permissions.Users.View"
// 4. PermissionAuthorizationHandler checks JWT claims
// 5. Has permission? → Allow request
//    No permission? → 403 Forbidden
```

---

## 2. Authorization System Architecture

### Bước 2.1: Authorization Flow Overview

**Complete Flow Diagram:**

```
┌─────────────────────────────────────────────────────────┐
│        PERMISSION-BASED AUTHORIZATION FLOW              │
└─────────────────────────────────────────────────────────┘

1. USER LOGIN
┌──────────────┐
│ POST /token  │
└──────┬───────┘
       │ { email, password }
       ▼
┌──────────────────┐
│  TokenService    │
└──────┬───────────┘
       │ 1. Validate credentials
       │ 2. Get user's roles (UserRoles table)
       │ 3. Get permissions from Permission table
       │    Query: SELECT Function.Name + '.' + Action.Name
       │           FROM Permission P
       │           JOIN Function F ON P.FunctionId = F.Id
       │        JOIN Action A ON P.ActionId = A.Id
       │           WHERE P.RoleId IN (user's roles)
       │ 4. Build JWT claims
       ▼
┌──────────────────┐
│   JWT Token      │
│  with Claims:    │
│  - NameIdentifier│
│  - Email         │
│  - Fullname      │
│  - permission:   │
│    "Users.View"  │
│  - permission:   │
│    "Users.Create"│
│  - permission:   │
│   "Products.View"│
└──────┬───────────┘
       │
       ▼ (Client stores token)

2. API CALL WITH AUTHORIZATION
┌──────────────────┐
│ GET /api/users   │
│ [MustHavePermission("View", "User")]
└──────┬───────────┘
       │ Authorization: Bearer {JWT}
       ▼
┌──────────────────────────┐
│ ASP.NET Core Pipeline    │
└──────┬───────────────────┘
       │ 1. Validate JWT signature
       │ 2. Extract claims from JWT
       ▼
┌──────────────────────────┐
│ PermissionPolicyProvider │
└──────┬───────────────────┘
       │ 3. Create policy "Permissions.User.View"
       │ 4. Add PermissionRequirement("Permissions.User.View")
       ▼
┌─────────────────────────────┐
│ PermissionAuthorizationHandler│
└──────┬──────────────────────┘
       │ 5. Check JWT claims
       │    Has claim "permission" = "Users.View"?
       │    → Call UserService.HasPermissionAsync()
       │   (Optional: double-check from database)
       ▼
┌──────────────────┐
│  Authorization   │
│  Decision        │
└──────┬───────────┘
       │
       ├─► ✅ Has Permission → Allow Request (200 OK)
       │
       └─► ❌ No Permission → Deny Request (403 Forbidden)
```

---

### Bước 2.2: Key Components

**1. PermissionRequirement (IAuthorizationRequirement):**
- Represents a permission requirement
- Contains permission string (e.g., "Permissions.Users.View")

**2. PermissionAuthorizationHandler (AuthorizationHandler):**
- Handles permission requirements
- Checks if user has required permission in JWT claims
- Calls `UserService.HasPermissionAsync()` to verify

**3. PermissionPolicyProvider (IAuthorizationPolicyProvider):**
- Dynamically creates authorization policies
- Converts permission string → AuthorizationPolicy

**4. MustHavePermissionAttribute (AuthorizeAttribute):**
- Declarative attribute for controllers/actions
- Syntax: `[MustHavePermission(AppAction.View, AppFunction.User)]`
- Generates policy name: "Permissions.User.View"

**5. TokenService:**
- Adds permissions to JWT claims during login
- Claims: `new Claim(AppClaims.Permission, "Users.View")`

**6. UserService.Permission.cs:**
- `GetPermissionsAsync()`: Query permissions from database
- `HasPermissionAsync()`: Check if user has specific permission

---

## 3. Authorization Constants

### Bước 3.1: AppAction Constants

**Làm gì:** Define available actions (operations).

**Tại sao:** Standard actions để tái sử dụng across functions.

**File:** `src/Core/Shared/Authorization/AppPermissions.cs` (partial)

```csharp
namespace NightMarket.Shared.Authorization;

/// <summary>
/// Standard actions (operations) available in the system
/// Used to build permissions: Permissions.{Function}.{Action}
/// </summary>
public static class AppAction
{
    public const string View = nameof(View);
    public const string Search = nameof(Search);
    public const string Create = nameof(Create);
    public const string Update = nameof(Update);
    public const string Delete = nameof(Delete);
    public const string Import = nameof(Import);
    public const string Export = nameof(Export);
    public const string Clean = nameof(Clean);
}
```

**Giải thích:**
- **View:** Read/display data
- **Search:** Search với filters
- **Create:** Create new entities
- **Update:** Update existing entities
- **Delete:** Delete entities
- **Import:** Import data from external sources
- **Export:** Export data to files (Excel, CSV)
- **Clean:** Clean up old data

---

### Bước 3.2: AppFunction Constants

**Làm gì:** Define available functions (modules/features).

**Tại sao:** Standard functions để build permissions.

**File:** `src/Core/Shared/Authorization/AppPermissions.cs` (partial)

```csharp
/// <summary>
/// Functions (modules/features) available in the system
/// Used to build permissions: Permissions.{Function}.{Action}
/// </summary>
public static class AppFunction
{
    public const string Dashboard = nameof(Dashboard);
    public const string Hangfire = nameof(Hangfire);
    public const string User = nameof(User);
    public const string UserRole = nameof(UserRole);
    public const string Role = nameof(Role);
    public const string RoleClaim = nameof(RoleClaim);
    public const string Product = nameof(Product);
    public const string Category = nameof(Category);
}
```

**Giải thích:**
- Each constant represents a module/feature
- Used to build permission strings: `Permissions.{Function}.{Action}`
- Example: `Permissions.User.View`, `Permissions.Product.Create`

---

### Bước 3.3: AppPermission Record

**Làm gì:** Helper record để generate permission strings.

**Tại sao:** Type-safe permission generation và helper methods.

**File:** `src/Core/Shared/Authorization/AppPermissions.cs` (partial)

```csharp
/// <summary>
/// Permission record (Helper for permission string generation)
/// Format: "Permissions.{Function}.{Action}"
/// Example: "Permissions.User.View"
/// </summary>
public record AppPermission(string action, string function)
{
    /// <summary>
    /// Permission name (format: Permissions.Function.Action)
    /// </summary>
    public string Name => NameFor(action, function);

    /// <summary>
    /// Generate permission name from action and function
    /// </summary>
    public static string NameFor(string action, string function) => 
        $"Permissions.{function}.{action}";

    /// <summary>
    /// Generate all permissions for a function (all actions)
    /// Example: GeneratePermissionsForFunction("User")
    /// Returns: ["Permissions.User.View", "Permissions.User.Create", ...]
    /// </summary>
    public static List<string> GeneratePermissionsForFunction(string function)
    {
        // Get all action constants using reflection
        var actions = typeof(AppAction)
                 .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                 .Where(field => field.IsLiteral && !field.IsInitOnly) // Only constants
                 .Select(field => field.GetValue(null)?.ToString())
                 .Where(value => value != null)
                 .ToList();

        // Generate permission strings
        return actions
            .Select(action => $"Permissions.{function}.{action}")
            .ToList();
    }

    /// <summary>
    /// Generate permissions for a function with specific actions
    /// Example: GeneratePermissionsForFunction("User", ["View", "Create"])
    /// Returns: ["Permissions.User.View", "Permissions.User.Create"]
    /// </summary>
    public static List<string> GeneratePermissionsForFunction(string function, List<string> actions)
    {
        if (actions == null || actions.Count == 0)
  throw new ArgumentException("Actions list cannot be null or empty", nameof(actions));

        return actions
        .Select(action => $"Permissions.{function}.{action}")
        .ToList();
    }
}
```

**Giải thích:**

**Permission Format:**
- Standard format: `Permissions.{Function}.{Action}`
- Example: `Permissions.User.View`, `Permissions.Product.Create`

**Helper Methods:**
- **NameFor():** Generate single permission string
- **GeneratePermissionsForFunction(function):** Generate all permissions for function
- **GeneratePermissionsForFunction(function, actions):** Generate specific permissions

**Usage Examples:**
```csharp
// Single permission
var permission = AppPermission.NameFor(AppAction.View, AppFunction.User);
// → "Permissions.User.View"

// All permissions for User function
var allUserPermissions = AppPermission.GeneratePermissionsForFunction(AppFunction.User);
// → ["Permissions.User.View", "Permissions.User.Create", "Permissions.User.Update", ...]

// Specific permissions for Product function
var productPermissions = AppPermission.GeneratePermissionsForFunction(
    AppFunction.Product, 
    new List<string> { AppAction.View, AppAction.Create });
// → ["Permissions.Product.View", "Permissions.Product.Create"]
```

---

### Bước 3.4: AppClaims Constants

**Làm gì:** Define JWT claim names.

**Tại sao:** Consistent claim names across application.

**File:** `src/Core/Shared/Authorization/AppClaims.cs`

```csharp
namespace NightMarket.Shared.Authorization;

/// <summary>
/// JWT claim names
/// </summary>
public static class AppClaims
{
    /// <summary>
    /// Full name claim (FirstName + LastName)
    /// </summary>
    public const string Fullname = "fullName";

    /// <summary>
    /// Permission claim (multiple claims with this name)
    /// Format: "Permissions.Function.Action"
    /// Example: "Permissions.User.View"
    /// </summary>
    public const string Permission = "permission";

    /// <summary>
    /// Image URL claim (avatar)
    /// </summary>
    public const string ImageUrl = "image_url";

    /// <summary>
    /// IP Address claim
    /// </summary>
    public const string IpAddress = "ipAddress";

    /// <summary>
    /// Expiration claim (standard JWT claim)
    /// </summary>
    public const string Expiration = "exp";
}
```

**Giải thích:**
- **Permission:** Multiple claims với cùng tên (one claim per permission)
- Standard JWT claims: NameIdentifier, Email, Name, Surname
- Custom claims: Fullname, Permission, ImageUrl, IpAddress

---

## 4. Permission Authorization Components (Các Thành phần Phân quyền)

### Bước 4.1: PermissionRequirement (Yêu cầu Quyền)

**Làm gì:** Authorization requirement để kiểm tra quyền.

**Tại sao:** Đại diện cho một yêu cầu quyền trong authorization pipeline.

**File:** `src/Infrastructure/Infrastructure/Auth/Permissions/PermissionRequirement.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Yêu cầu quyền (implements IAuthorizationRequirement)
/// Đại diện cho một quyền cần được kiểm tra
/// </summary>
internal class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Chuỗi permission (định dạng: "Permissions.Function.Action")
    /// Ví dụ: "Permissions.User.View"
    /// </summary>
    public string Permission { get; private set; }

    public PermissionRequirement(string permission)
    {
         Permission = permission;
    }
}
```

**Giải thích:**
- Implements `IAuthorizationRequirement` (ASP.NET Core Authorization)
- Lưu trữ chuỗi permission cần kiểm tra
- Được sử dụng bởi `PermissionAuthorizationHandler`

---

### Bước 4.2: PermissionAuthorizationHandler (Trình xử lý Phân quyền)

**Làm gì:** Authorization handler để kiểm tra quyền.

**Tại sao:** Đánh giá yêu cầu quyền dựa trên claims của user.

**File:** `src/Infrastructure/Infrastructure/Auth/Permissions/PermissionAuthorizationHandler.cs`

```csharp
using System.Security.Claims;
using NightMarket.WebApi.Application.Identity.Users;
using Microsoft.AspNetCore.Authorization;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Trình xử lý authorization cho yêu cầu quyền
/// Kiểm tra xem user có quyền yêu cầu trong JWT claims không
/// </summary>
internal class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserService _userService;

    public PermissionAuthorizationHandler(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Xử lý yêu cầu quyền
    /// Kiểm tra xem user có quyền yêu cầu không
    /// </summary>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        PermissionRequirement requirement)
    {
        // Lấy user ID từ JWT claims
        if (context.User?.GetUserId() is { } userId &&
        // Kiểm tra xem user có quyền không (từ JWT claims hoặc database)
        await _userService.HasPermissionAsync(userId, requirement.Permission))
        {
            // User có quyền → Thành công
            context.Succeed(requirement);
        }

        // Nếu không thành công → Authorization thất bại (403 Forbidden)
    }
}
```

**Giải thích:**

**Luồng HandleRequirementAsync:**
1. Lấy user ID từ JWT claims (`context.User.GetUserId()`)
2. Gọi `UserService.HasPermissionAsync()` để kiểm tra quyền
3. Nếu user có quyền → `context.Succeed(requirement)`
4. Nếu không → Authorization thất bại (handler không gọi Succeed)

**Tại sao gọi UserService.HasPermissionAsync():**
- Quyền được lưu trong JWT claims (kiểm tra nhanh)
- Tùy chọn: Kiểm tra lại từ database (cho quyền bị thu hồi)
- Linh hoạt: Có thể implement caching strategy

**Kết quả Authorization:**
- **Succeed (Thành công):** User có quyền → Cho phép request (200 OK)
- **Not Succeed (Không thành công):** User không có quyền → 403 Forbidden

---

### Bước 4.3: PermissionPolicyProvider (Nhà cung cấp Chính sách)

**Làm gì:** Dynamic policy provider cho permission-based policies.

**Tại sao:** Tạo authorization policies tức thì dựa trên chuỗi permission.

**File:** `src/Infrastructure/Infrastructure/Auth/Permissions/PermissionPolicyProvider.cs`

```csharp
using NightMarket.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Nhà cung cấp chính sách quyền (tạo policy động)
/// Tạo authorization policies dựa trên chuỗi permission
/// </summary>
internal class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    /// <summary>
    /// Nhà cung cấp policy dự phòng (cho các policy không phải permission)
    /// </summary>
    public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>
    /// Lấy default policy (không dùng cho permissions)
    /// </summary>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => 
        FallbackPolicyProvider.GetDefaultPolicyAsync();

    /// <summary>
    /// Lấy policy theo tên
    /// Nếu tên policy bắt đầu bằng "Permissions", tạo permission policy
    /// Ngược lại, dùng fallback provider
    /// </summary>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Kiểm tra xem policy có phải là permission policy không
        if (policyName.StartsWith(AppClaims.Permission, StringComparison.OrdinalIgnoreCase))
        {
            // Tạo permission policy động
             var policy = new AuthorizationPolicyBuilder();
            policy.AddRequirements(new PermissionRequirement(policyName));
            return Task.FromResult<AuthorizationPolicy?>(policy.Build());
        }

        // Dùng fallback cho các policy không phải permission
        return FallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    /// <summary>
    /// Lấy fallback policy (không dùng cho permissions)
    /// </summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => 
        Task.FromResult<AuthorizationPolicy?>(null);
}
```

**Giải thích:**

**Luồng GetPolicyAsync:**
1. Kiểm tra xem tên policy có bắt đầu bằng "Permissions" không (VD: "Permissions.User.View")
2. Nếu có → Tạo `AuthorizationPolicy` với `PermissionRequirement`
3. Nếu không → Dùng fallback provider (cho các policy khác như Roles)

**Tại sao Tạo Policy Động:**
- Không cần đăng ký từng permission policy
- Policies được tạo tức thì dựa trên chuỗi permission
- Có thể mở rộng: Hỗ trợ không giới hạn permissions

**Ví dụ:**
```csharp
// Attribute trên controller:
[MustHavePermission(AppAction.View, AppFunction.User)]
// → Tên policy: "Permissions.User.View"

// PermissionPolicyProvider tạo:
// AuthorizationPolicy với PermissionRequirement("Permissions.User.View")

// PermissionAuthorizationHandler kiểm tra:
// User có quyền "Permissions.User.View" không?
```

---

### Bước 4.4: MustHavePermissionAttribute (Thuộc tính Phải có Quyền)

**Làm gì:** Thuộc tính khai báo cho permission-based authorization.

**Tại sao:** Thuộc tính dễ sử dụng cho controllers/actions.

**File:** `src/Infrastructure/Infrastructure/Auth/Permissions/MustHavePermissionAttribute.cs`

```csharp
using NightMarket.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Thuộc tính MustHavePermission (authorization khai báo)
/// Cách dùng: [MustHavePermission(AppAction.View, AppFunction.User)]
/// Tạo policy: "Permissions.User.View"
/// </summary>
public class MustHavePermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Constructor với tham số action và function
    /// </summary>
    /// <param name="action">Action (VD: AppAction.View)</param>
    /// <param name="function">Function (VD: AppFunction.User)</param>
    public MustHavePermissionAttribute(string action, string function)
    {
        // Tạo tên policy: "Permissions.{Function}.{Action}"
        Policy = AppPermission.NameFor(action, function);
    }
}
```

**Giải thích:**

**Cách hoạt động:**
1. Attribute đặt thuộc tính `Policy` (từ `AuthorizeAttribute`)
2. Định dạng tên policy: `Permissions.{Function}.{Action}`
3. ASP.NET Core Authorization pipeline gọi `PermissionPolicyProvider.GetPolicyAsync(policyName)`
4. Policy provider tạo policy với `PermissionRequirement`
5. `PermissionAuthorizationHandler` đánh giá requirement

**Ví dụ sử dụng:**
```csharp
// Permission ở cấp Controller
[ApiController]
[Route("api/users")]
[MustHavePermission(AppAction.View, AppFunction.User)] // Tất cả actions yêu cầu Users.View
public class UsersController : ControllerBase
{
  // ...
}

// Permission ở cấp Action
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    [HttpGet]
    [MustHavePermission(AppAction.View, AppFunction.User)]
    public Task<List<UserDto>> GetAllAsync()
    {
        // Chỉ users có quyền "Permissions.User.View"
    }

    [HttpPost]
    [MustHavePermission(AppAction.Create, AppFunction.User)]
    public Task<string> CreateAsync(CreateUserRequest request)
    {
        // Chỉ users có quyền "Permissions.User.Create"
    }

    [HttpDelete("{id}")]
  [MustHavePermission(AppAction.Delete, AppFunction.User)]
    public Task DeleteAsync(string id)
    {
        // Chỉ users có quyền "Permissions.User.Delete"
    }
}
```

---

## 5. UserService - Permission Operations (UserService - Các Thao tác Quyền)

### Bước 5.1: UserService.Permission.cs (Partial Class - Lớp Một phần)

**Làm gì:** Implement các thao tác truy vấn quyền.

**Tại sao:** Lấy danh sách quyền của user từ database và kiểm tra quyền.

**File:** `src/Infrastructure/Infrastructure/Identity/UserService.Permission.cs`

```csharp
using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Các Thao tác Quyền (Partial Class)
/// </summary>
internal partial class UserService
{
  /// <summary>
    /// Lấy danh sách quyền của user từ database
    /// Trả về danh sách chuỗi permission (Định dạng: "Function.Action")
    /// Lưu ý: Lưu trong bảng Permission dưới dạng (RoleId, FunctionId, ActionId)
    /// </summary>
    public async Task<List<string>> GetPermissionsAsync(
         string userId, 
         CancellationToken cancellationToken)
    {
        // Tìm user
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            throw new UnauthorizedException("Xác thực thất bại.");
        }

    // Lấy các roles của user (từ bảng UserRoles - Identity)
    var userRoles = await _userManager.GetRolesAsync(user);

     // Truy vấn permissions từ bảng Permission
        // JOIN: Permission → Role → Function → Action
      var permissions = await _db.Permissions
            .Include(p => p.Role)
            .Include(p => p.Function)
            .Include(p => p.Action)
            .Where(p => userRoles.Contains(p.Role.Name!)) // Lọc theo roles của user
            .Select(p => $"{p.Function.Name}.{p.Action.Name}") // Định dạng: "Function.Action"
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions;
    }

    /// <summary>
    /// Kiểm tra xem user có quyền cụ thể hay không
    /// Được sử dụng bởi PermissionAuthorizationHandler
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        string userId, 
  string permission, 
  CancellationToken cancellationToken = default)
    {
        // Lấy danh sách quyền của user
        var permissions = await GetPermissionsAsync(userId, cancellationToken);

       // Kiểm tra xem permission có tồn tại trong danh sách không
      // Định dạng permission: "Permissions.Function.Action" (từ JWT claims)
     // HOẶC "Function.Action" (từ database)
    // Vì vậy cần chuẩn hóa để so sánh
        var normalizedPermission = permission
        .Replace("Permissions.", "", StringComparison.OrdinalIgnoreCase);

        return permissions?.Contains(normalizedPermission) ?? false;
    }
}
```

**Giải thích:**

**Luồng GetPermissionsAsync:**
1. Tìm user theo ID
2. Lấy các roles của user (`_userManager.GetRolesAsync()`)
3. Truy vấn bảng Permission:
   - JOIN với Role, Function, Action
   - WHERE Role.Name IN (các roles của user)
   - SELECT Function.Name + '.' + Action.Name
4. Trả về các permissions duy nhất (distinct)

**HasPermissionAsync:**
- Được gọi bởi `PermissionAuthorizationHandler`
- Kiểm tra xem user có quyền cụ thể không
- Chuẩn hóa chuỗi permission (xóa tiền tố "Permissions." nếu có)

**Định dạng Permission:**
- **Trong Database:** `"Users.View"` (Function.Action)
- **Trong JWT Claims:** `"Permissions.Users.View"` (có tiền tố)
- **So sánh:** Chuẩn hóa về định dạng `"Users.View"`

**Tại sao Include Relations (Eager Loading):**
- Tải trước: Load Role, Function, Action trong một query duy nhất
- Tránh vấn đề N+1 query
- Hiệu suất tốt hơn

---

## 6. TokenService - Add Permissions to JWT (TokenService - Thêm Quyền vào JWT)

### Bước 6.1: Update TokenService.GetClaims() (Cập nhật phương thức GetClaims)

**Làm gì:** Thêm permissions vào JWT claims khi đăng nhập.

**Tại sao:** Permissions được lưu trong JWT để kiểm tra authorization nhanh.

**File:** `src/Infrastructure/Infrastructure/Identity/TokenService.cs` (partial - update existing method)

```csharp
using NightMarket.WebApi.Application.Identity.Tokens;
using NightMarket.WebApi.Application.Identity.Users;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Auth.Jwt;
using NightMarket.WebApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.Shared.Authorization;

namespace NightMarket.WebApi.Infrastructure.Identity;

internal class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserService _userService;
    private readonly SecuritySettings _securitySettings;
    private readonly JwtSettings _jwtSettings;

    public TokenService(
        UserManager<ApplicationUser> userManager,
        IUserService userService,
        IOptions<JwtSettings> jwtSettings,
         IOptions<SecuritySettings> securitySettings)
    {
         _userManager = userManager;
         _userService = userService;
         _jwtSettings = jwtSettings.Value;
         _securitySettings = securitySettings.Value;
    }

    // ... các methods hiện có ...

    /// <summary>
    /// Generate JWT token with claims
    /// </summary>
    private string GenerateJwt(ApplicationUser user, string ipAddress) =>
    GenerateEncryptedToken(GetSigningCredentials(), GetClaims(user, ipAddress));

    /// <summary>
    /// Lấy claims cho JWT token
    /// Bao gồm permissions từ database
    /// </summary>
    private async Task<IEnumerable<Claim>> GetClaimsAsync(ApplicationUser user, string ipAddress)
    {
        // Standard claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(AppClaims.Fullname, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(AppClaims.IpAddress, ipAddress),
            new(AppClaims.ImageUrl, user.ImageUrl ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty)
        };

        // Thêm permissions vào claims
        // Truy vấn permissions từ database
        var permissions = await _userService.GetPermissionsAsync(user.Id, CancellationToken.None);

        // Thêm mỗi permission thành một claim riêng biệt
        // Nhiều claims có cùng tên (AppClaims.Permission)
        foreach (var permission in permissions)
        {
            // Thêm với tiền tố "Permissions." để đồng nhất
            claims.Add(new Claim(AppClaims.Permission, $"Permissions.{permission}"));
        }

        return claims;
    }

    /// <summary>
    /// Generate encrypted JWT token
    /// </summary>
    private string GenerateEncryptedToken(SigningCredentials signingCredentials, IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.TokenExpirationInMinutes),
            signingCredentials: signingCredentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    // ... other existing methods ...
}
```

**Giải thích:**
- **Thay đổi trong GetClaimsAsync:**
  - Đổi từ synchronous `GetClaims()` sang async `GetClaimsAsync()`
  - Truy vấn permissions từ database: `_userService.GetPermissionsAsync()`
  - Thêm mỗi permission thành một claim riêng biệt
  - Định dạng: `new Claim(AppClaims.Permission, "Permissions.Function.Action")`

- **Nhiều Claims có Cùng Tên:**
  - JWT hỗ trợ nhiều claims có cùng tên
  - Ví dụ JWT payload:
  ```json
  {
    "nameid": "user-id",
    "email": "user@example.com",
    "permission": "Permissions.Users.View",
    "permission": "Permissions.Users.Create",
    "permission": "Permissions.Products.View"
  }
  ```

- **Tại sao Thêm Permissions vào JWT:**
  - **Authorization Nhanh:** Không cần query database mỗi request
  - **Stateless (Không trạng thái):** Tất cả thông tin trong JWT token
  - **Có thể mở rộng:** Không cần lưu session

- **⚠️ Lưu ý Quan trọng:**
  - Cần cập nhật `GenerateJwt()` để gọi async `GetClaimsAsync()`
  - Cập nhật tất cả method signatures sang async nếu cần

---

## 7. Register Authorization Services (Đăng ký Dịch vụ Authorization)

### Bước 7.1: Auth Startup Configuration (Cấu hình Startup Auth)

**Làm gì:** Đăng ký authorization services trong dependency injection.

**Tại sao:** Cấu hình ASP.NET Core Authorization với các components tùy chỉnh.

**File:** `src/Infrastructure/Infrastructure/Auth/Startup.cs`

```csharp
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Infrastructure.Auth.Jwt;
using NightMarket.WebApi.Infrastructure.Auth.OAuth2;
using NightMarket.WebApi.Infrastructure.Auth.Permissions;
using NightMarket.WebApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NightMarket.WebApi.Infrastructure.Auth;

internal static class Startup
{
    /// <summary>
    /// Thêm authentication và authorization services
    /// </summary>
    internal static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        services
        .AddCurrentUser()
        .AddPermissions() // ← Thêm permission services
        // Phải thêm identity trước khi thêm auth!
       .AddIdentity();

        services.Configure<SecuritySettings>(config.GetSection(nameof(SecuritySettings)));
        services.AddO2Authentication(config);
        return services.AddJwtAuth();
    }

    /// <summary>
    /// Sử dụng current user middleware
    /// </summary>
    internal static IApplicationBuilder UseCurrentUser(this IApplicationBuilder app) =>
  app.UseMiddleware<CurrentUserMiddleware>();

    /// <summary>
    /// Thêm current user services
    /// </summary>
    private static IServiceCollection AddCurrentUser(this IServiceCollection services) =>
        services
            .AddScoped<CurrentUserMiddleware>()
            .AddScoped<ICurrentUser, CurrentUser>()
            .AddScoped(sp => (ICurrentUserInitializer)sp.GetRequiredService<ICurrentUser>());

    /// <summary>
    /// Thêm permission-based authorization services
    /// </summary>
    private static IServiceCollection AddPermissions(this IServiceCollection services) =>
        services
             // Đăng ký PermissionPolicyProvider như Singleton
             // Singleton: Policy provider không có state, an toàn khi chia sẻ
            .AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
            // Đăng ký PermissionAuthorizationHandler như Scoped
            // Scoped: Handler cần UserService (scoped), nên handler cũng phải scoped
            .AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
}
```

**Giải thích:**
- **Phương thức AddPermissions():**
  - **PermissionPolicyProvider:** Đăng ký như Singleton
    - Policy provider không có state
  - An toàn khi chia sẻ giữa các requests
    - Hiệu suất tốt hơn

  - **PermissionAuthorizationHandler:** Đăng ký như Scoped
    - Handler phụ thuộc vào `IUserService` (scoped service)
    - Phải khớp với service lifetime
    - Instance mới cho mỗi request

- **Service Lifetimes (Vòng đời Service):**
```
Singleton ← PermissionPolicyProvider
    │
    ├─ Instance giống nhau cho tất cả requests
    └─ Không có state, thread-safe

Scoped  ← PermissionAuthorizationHandler
    │
    ├─ Instance mới cho mỗi request
    ├─ Có thể phụ thuộc vào scoped services khác (UserService)
    └─ Được dispose khi kết thúc request

Transient
    │
    ├─ Instance mới mỗi lần inject
    └─ Services ngắn hạn
```

- **Thứ tự Đăng ký:**
1. `AddCurrentUser()` - Đăng ký current user services
2. `AddPermissions()` - Đăng ký authorization services
3. `AddIdentity()` - Đăng ký ASP.NET Core Identity
4. `AddJwtAuth()` - Đăng ký JWT authentication

---

## 8. Testing Permission Authorization (Kiểm thử Phân quyền)

### Bước 8.1: Test Setup - Create Test User with Permissions (Thiết lập Test - Tạo User Test với Quyền)

**Step 1: Create Manager Role (Tạo Role Manager - đã làm trong BUILD_16B)**

```csharp
// Manager role đã được tạo trong RoleService tests
```

**Step 2: Assign Permissions to Manager Role (Gán Quyền cho Role Manager)**

**API Call:**
```bash
curl -X PUT https://localhost:7001/api/role/{managerRoleId}/permissions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {adminToken}" \
  -d '{
    "roleId": "{managerRoleId}",
    "permissions": [
      {
        "functionId": "{usersFunction}",
        "actionId": "{viewAction}"
   },
      {
   "functionId": "{usersFunction}",
        "actionId": "{createAction}"
      },
      {
        "functionId": "{productsFunction}",
     "actionId": "{viewAction}"
      }
    ]
  }'
```

**Expected Response (Kết quả mong đợi):**
```json
{
  "message": "Permissions Updated." // Đã cập nhật quyền
}
```

**Step 3: Assign Manager Role to Test User (Gán Role Manager cho User Test)**

**API Call:**
```bash
curl -X POST https://localhost:7001/api/users/{userId}/roles \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {adminToken}" \
  -d '{
    "userRoles": [
{
        "roleId": "{managerRoleId}",
        "roleName": "Manager",
        "enabled": true
      }
    ]
  }'
```

---

### Bước 8.2: Test Login and JWT Claims (Test Đăng nhập và JWT Claims)

**API Call:**
```bash
curl -X POST https://localhost:7001/api/tokens \
  -H "Content-Type: application/json" \
  -d '{
    "email": "manager@example.com",
    "password": "SecurePass123!"
  }'
```

**Expected Response (Kết quả mong đợi):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "refreshTokenExpiryTime": "2024-02-29T12:00:00Z"
}
```

**Verify JWT Claims (Xác minh JWT Claims - Giải mã JWT trên jwt.io):**
```json
{
  "nameid": "user-id",
  "email": "manager@example.com",
  "fullName": "Manager User",
  "permission": "Permissions.User.View",
  "permission": "Permissions.User.Create",
  "permission": "Permissions.Product.View",
  "exp": 1706529600,
  "iss": "NightMarket.WebApi",
  "aud": "NightMarket.WebApi"
}
```

✅ **Verify (Xác minh):** JWT chứa nhiều `permission` claims

---

### Bước 8.3: Test Protected Endpoint - Success Case (Test Endpoint được Bảo vệ - Trường hợp Thành công)

**Scenario (Tình huống):** Manager user gọi `GET /api/users` (yêu cầu quyền "Users.View")

**API Call:**
```bash
curl -X GET https://localhost:7001/api/users \
  -H "Authorization: Bearer {managerToken}"
```

**Expected Response (Kết quả mong đợi):**
```json
[
  {
    "id": "user-1",
    "userName": "admin",
    "firstName": "Admin",
    "lastName": "User",
    "email": "admin@example.com",
    "isActive": true
  },
  {
    "id": "user-2",
  "userName": "manager",
  "firstName": "Manager",
 "lastName": "User",
    "email": "manager@example.com",
    "isActive": true
  }
]
```

**✅ Success (Thành công):** Manager có quyền "Users.View" → Cho phép request

---

### Bước 8.4: Test Protected Endpoint - Forbidden Case (Test Endpoint được Bảo vệ - Trường hợp Bị Cấm)

**Scenario (Tình huống):** Manager user gọi `DELETE /api/users/{id}` (yêu cầu quyền "Users.Delete")

**API Call:**
```bash
curl -X DELETE https://localhost:7001/api/users/{userId} \
  -H "Authorization: Bearer {managerToken}"
```

**Expected Response (Kết quả mong đợi):**
```json
{
  "statusCode": 403,
  "message": "You do not have permission to access this resource."
  // Bạn không có quyền truy cập tài nguyên này
}
```

**❌ Forbidden (Bị cấm):** Manager không có quyền "Users.Delete" → 403 Forbidden

---

### Bước 8.5: Test Without Authentication (Test Không có Xác thực)

**Scenario (Tình huống):** Anonymous user (user ẩn danh) gọi protected endpoint

**API Call:**
```bash
curl -X GET https://localhost:7001/api/users
# Không có Authorization header
```

**Expected Response (Kết quả mong đợi):**
```json
{
  "statusCode": 401,
"message": "Unauthorized. Please authenticate."
  // Chưa xác thực. Vui lòng đăng nhập
}
```

**❌ Unauthorized (Chưa xác thực):** Không có JWT token → 401 Unauthorized

---

## 9. Example: Protected Controller (Ví dụ: Controller được Bảo vệ)

### Bước 9.1: UsersController with Permission Protection (UsersController với Bảo vệ Quyền)

**File:** `src/Host/Host/Controllers/Identity/UsersController.cs` (update existing)

```csharp
using NightMarket.WebApi.Application.Identity.Users;
using NightMarket.WebApi.Infrastructure.Auth.Permissions;
using NightMarket.Shared.Authorization;
using NSwag.Annotations;

namespace NightMarket.WebApi.Host.Controllers.Identity;

/// <summary>
/// APIs quản lý User (có bảo vệ quyền)
/// </summary>
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
     _userService = userService;
    }

    /// <summary>
    /// Lấy danh sách tất cả users
  /// Yêu cầu: Quyền Users.View
    /// </summary>
    [HttpGet("list")]
    [MustHavePermission(AppAction.View, AppFunction.User)]
    [OpenApiOperation("Lấy danh sách tất cả users.", "")]
    public Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return _userService.GetListAsync(cancellationToken);
    }

    /// <summary>
    /// Lấy chi tiết user theo ID
    /// Yêu cầu: Quyền Users.View
    /// </summary>
    [HttpGet("{id}")]
 [MustHavePermission(AppAction.View, AppFunction.User)]
    [OpenApiOperation("Lấy chi tiết một user.", "")]
public Task<UserDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return _userService.GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Tạo user mới (chỉ Admin)
  /// Yêu cầu: Quyền Users.Create
/// </summary>
[HttpPost("create")]
    [MustHavePermission(AppAction.Create, AppFunction.User)]
    [OpenApiOperation("Tạo một user mới.", "")]
    public Task<string> CreateAsync(CreateUserRequest request)
    {
        return _userService.CreateAsync(request, GetOriginFromRequest());
    }

    /// <summary>
    /// Cập nhật thông tin user
    /// Yêu cầu: Quyền Users.Update
    /// </summary>
    [HttpPut("{id}")]
    [MustHavePermission(AppAction.Update, AppFunction.User)]
    [OpenApiOperation("Cập nhật thông tin user.", "")]
    public async Task<ActionResult> UpdateAsync(string id, UpdateUserRequest request)
    {
        if (id != request.Id)
    {
         return BadRequest();
        }

 await _userService.UpdateAsync(request, id);
     return Ok();
    }

    /// <summary>
 /// Xóa user
    /// Yêu cầu: Quyền Users.Delete
    /// </summary>
    [HttpDelete("{id}")]
    [MustHavePermission(AppAction.Delete, AppFunction.User)]
    [OpenApiOperation("Xóa một user.", "")]
    public async Task<ActionResult> DeleteAsync(string id)
    {
        // Implementation (chưa có trong UserService)
      return NoContent();
    }

    /// <summary>
    /// Tự đăng ký (Anonymous - không cần quyền)
    /// </summary>
    [HttpPost("self-register")]
    [AllowAnonymous]
  [OpenApiOperation("User tự tạo tài khoản.", "")]
public Task<string> SelfRegisterAsync(CreateUserRequest request)
    {
        return _userService.CreateAsync(request, GetOriginFromRequest());
    }

    private string GetOriginFromRequest() =>
     $"{Request.Scheme}://{Request.Host.Value}{Request.PathBase.Value}";
}
```

**Giải thích:**

**Permission Attributes (Thuộc tính Quyền):**
- `[MustHavePermission(AppAction.View, AppFunction.User)]`
  - Tạo policy: "Permissions.User.View"
  - Chỉ users có quyền "Users.View" mới có thể truy cập

**AllowAnonymous (Cho phép Ẩn danh):**
- Endpoint `/self-register` không cần authentication
- Bất kỳ ai cũng có thể đăng ký

**Authorization Flow (Luồng Phân quyền):**
```
Request → JWT Authentication → Permission Check → Controller Action
    │     │              │
    │    │
    │              └─ JwtBearerHandler validates JWT
    │
    └─ Authorization: Bearer {token}
```

---

## 10. Summary (Tổng kết)

### ✅ Đã hoàn thành trong bước này:

**Authorization Components (Các Thành phần Phân quyền):**
- ✅ PermissionRequirement (Yêu cầu Quyền - IAuthorizationRequirement)
- ✅ PermissionAuthorizationHandler (Trình xử lý Phân quyền - kiểm tra quyền)
- ✅ PermissionPolicyProvider (Nhà cung cấp Chính sách - tạo policy động)
- ✅ MustHavePermissionAttribute (Thuộc tính khai báo)

**Permission Constants (Hằng số Quyền):**
- ✅ AppAction (View, Create, Update, Delete, v.v.)
- ✅ AppFunction (User, Role, Product, v.v.)
- ✅ AppPermission (helper record)
- ✅ AppClaims (Tên Permission claim)

**UserService - Permission Operations (UserService - Các Thao tác Quyền):**
- ✅ GetPermissionsAsync (truy vấn từ database)
- ✅ HasPermissionAsync (kiểm tra quyền cụ thể)

**TokenService - JWT Claims:**
- ✅ Thêm permissions vào JWT claims khi đăng nhập
- ✅ Nhiều permission claims trong JWT

**Startup Configuration (Cấu hình Startup):**
- ✅ Đăng ký authorization services
- ✅ PermissionPolicyProvider (Singleton)
- ✅ PermissionAuthorizationHandler (Scoped)

**Testing (Kiểm thử):**
- ✅ Protected endpoints với MustHavePermission
- ✅ Success case (có quyền)
- ✅ Forbidden case (không có quyền)
- ✅ Unauthorized case (không có authentication)

### 📊 Complete Authorization Flow (Luồng Phân quyền Hoàn chỉnh):

```
┌─────────────────────────────────────────────────┐
│   LUỒNG PHÂN QUYỀN DỰA TRÊN PERMISSION   │
└─────────────────────────────────────────────────┘

1. ĐĂNG KÝ USER & GÁN ROLE
   User tạo tài khoản → Admin gán role Manager
   → Role Manager có quyền: Users.View, Users.Create

2. ĐĂNG NHẬP & TẠO JWT
   POST /tokens
   → TokenService.GetTokenAsync()
   → UserService.GetPermissionsAsync()
   Query: SELECT Function.Name + '.' + Action.Name
 FROM Permission P
 WHERE P.RoleId IN (các roles của user)
 → Thêm permissions vào JWT claims
   → Trả về JWT token

3. GỌI API VỚI JWT
   GET /api/users
   Authorization: Bearer {JWT}
 [MustHavePermission(AppAction.View, AppFunction.User)]
   → JWT middleware validates token
   → Trích xuất claims từ JWT

4. KIỂM TRA AUTHORIZATION
   → PermissionPolicyProvider.GetPolicyAsync("Permissions.User.View")
   → Tạo policy với PermissionRequirement
   → PermissionAuthorizationHandler.HandleRequirementAsync()
   → Kiểm tra JWT claims: Có "permission" = "Permissions.User.View"?
   → UserService.HasPermissionAsync() (tùy chọn kiểm tra lại)

5. KỐT QUẢ
 ✅ Có quyền → 200 OK với data
   ❌ Không có quyền → 403 Forbidden
   ❌ Chưa xác thực → 401 Unauthorized
```

### 📌 Key Concepts (Khái niệm Chính):

**Permission Format (Định dạng Quyền):**
- **Database (Cơ sở dữ liệu):** `"Users.View"` (Function.Action)
- **JWT Claims:** `"Permissions.Users.View"` (có tiền tố)
- **Attribute (Thuộc tính):** `[MustHavePermission(AppAction.View, AppFunction.User)]`
- **Policy (Chính sách):** `"Permissions.User.View"`

**Components Interaction (Tương tác giữa các Thành phần):**
1. **MustHavePermissionAttribute:** Đặt tên policy
2. **PermissionPolicyProvider:** Tạo policy với PermissionRequirement
3. **PermissionAuthorizationHandler:** Đánh giá requirement dựa trên JWT claims
4. **UserService:** Truy vấn permissions từ database (để tạo JWT)
5. **TokenService:** Thêm permissions vào JWT claims

**Benefits (Lợi ích):**
- ✅ Dynamic authorization (Phân quyền động - từ database)
- ✅ Fast checks (Kiểm tra nhanh - quyền trong JWT claims)
- ✅ Fine-grained access control (Kiểm soát truy cập chi tiết - theo function, action)
- ✅ Declarative security (Bảo mật khai báo - dùng attributes)
- ✅ Scalable (Có thể mở rộng - hỗ trợ không giới hạn quyền)

**Security Considerations (Cân nhắc Bảo mật):**
- Quyền được load từ database khi đăng nhập
- Lưu trong JWT để authorization nhanh
- Nếu quyền thay đổi, user phải đăng nhập lại
- Tùy chọn: Implement permission cache invalidation (vô hiệu hóa cache quyền)

### 📁 Complete File Structure (Cấu trúc File Hoàn chỉnh):

```
src/
├── Core/
│   ├── Shared/
│   │   └── Authorization/
│   │       ├── AppPermissions.cs (AppAction, AppFunction, AppPermission)
│   │       ├── AppClaims.cs
│   │       └── AppRoles.cs
│   ├── Domain/
│   │   └── Identity/
│   │  ├── Permission.cs (entity)
│   │       ├── Function.cs
│   │       └── Action.cs
│   └── Application/
│ └── Identity/
│           ├── Users/
│           │   └── IUserService.cs (GetPermissionsAsync, HasPermissionAsync)
│           └── Tokens/
│      └── ITokenService.cs
├── Infrastructure/
│   └── Infrastructure/
│       ├── Auth/
│ │   ├── Startup.cs (AddPermissions)
│       │   └── Permissions/
│       │       ├── PermissionRequirement.cs
│       │       ├── PermissionAuthorizationHandler.cs
│       │ ├── PermissionPolicyProvider.cs
│       │       └── MustHavePermissionAttribute.cs
│   └── Identity/
│           ├── TokenService.cs (GetClaimsAsync - thêm permissions)
│     └── UserService.Permission.cs (GetPermissionsAsync, HasPermissionAsync)
└── Host/
    └── Host/
        └── Controllers/
            └── Identity/
     └── UsersController.cs (với [MustHavePermission] attributes)
```

---

## 11. Next Steps (Các Bước Tiếp theo)

**Tiếp theo:** [BUILD_18 - OAuth2 Integration](BUILD_18_OAuth2_Integration.md)

Trong bước tiếp theo, chúng ta sẽ implement OAuth2 authentication:
1. ✅ Google OAuth2 setup (Thiết lập Google OAuth2)
2. ✅ Facebook OAuth2 setup (Thiết lập Facebook OAuth2)
3. ✅ IAuthenticationService interface (Interface Dịch vụ Xác thực)
4. ✅ AuthenticationService implementation (Triển khai Dịch vụ Xác thực)
5. ✅ OAuth2 middleware configuration (Cấu hình middleware OAuth2)
6. ✅ Social login flows (Luồng đăng nhập mạng xã hội)

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
