# Identity Controllers - REST API Endpoints

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 16C (Function Service) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Identity Controllers - REST API endpoints cho User, Role, Token và Personal management.

---

## 1. Overview (Tổng quan)

**Làm gì:** Xây dựng REST API Controllers để expose Identity services (User, Role, Token, Personal) cho frontend.

**Tại sao cần:**
- **RESTful APIs:** Expose services dưới dạng HTTP endpoints
- **Swagger Documentation:** Tự động generate API documentation
- **Clean Controller Pattern:** Controllers thin, chỉ route requests đến services
- **Consistent Response Format:** Unified response format cho tất cả endpoints
- **Authentication & Authorization:** Protect endpoints với JWT và permissions

**Trong bước này chúng ta sẽ:**
- ✅ Tạo TokensController (login, refresh token)
- ✅ Tạo UsersController (user management APIs)
- ✅ Tạo RoleController (role & function management APIs)
- ✅ Tạo PersonalController (current user profile APIs)
- ✅ Tạo AuthController (OAuth2 endpoints - đã có trong BUILD_18)
- ✅ Apply OpenAPI attributes cho Swagger
- ✅ Testing với Swagger UI

**Real-world example (Ví dụ thực tế):**
```csharp
// TokensController - Login endpoint
[HttpPost("get")]
[AllowAnonymous]
public Task<TokenResponse> GetTokenAsync(TokenRequest request)
{
    // Validate credentials
    // Generate JWT token
    return _tokenService.GetTokenAsync(request, GetIpAddress());
}

// UsersController - Get user by ID (Protected)
[HttpGet("{id}")]
[MustHavePermission(ECOAction.View, ECOFunction.User)]
public Task<UserDetailDto> GetByIdAsync(string id)
{
    // Only users với "Users.View" permission
    return _userService.GetAsync(id);
}

// PersonalController - Get current user profile
[HttpGet("profile")]
public Task<UserDetailDto> GetProfileAsync()
{
    // Get current logged-in user
    var userId = User.GetUserId();
    return _userService.GetAsync(userId);
}

// Controller Flow:
// 1. Client gửi HTTP request với JWT token
// 2. ASP.NET Core Authentication validates JWT
// 3. Authorization checks permissions (nếu có)
// 4. Controller route request đến Service
// 5. Service thực hiện business logic
// 6. Controller trả về response
```

---

## 2. Controllers Architecture (Kiến trúc Controllers)

### Bước 2.1: Controllers Overview (Tổng quan Controllers)

**Controllers trong Identity Module:**

```
src/Host/Host/Controllers/
├── Identity/
│   ├── TokensController.cs       # Login, Refresh token
│   ├── UsersController.cs        # User management
│   ├── RoleController.cs    # Role & Function management
│   └── AuthController.cs         # OAuth2 (Google, Facebook)
├── Personal/
│   └── PersonalController.cs     # Current user profile
└── BaseApiController.cs          # Base controller với MediatR
```

**Controllers Responsibilities (Trách nhiệm):**
- **TokensController:** Authentication (login, refresh)
- **UsersController:** User CRUD, assign roles, confirm email
- **RoleController:** Role CRUD, manage permissions, Function CRUD
- **PersonalController:** Current user operations (profile, change password, permissions)
- **AuthController:** Social login (Google, Facebook)

---

### Bước 2.2: BaseApiController

**Làm gì:** Base controller với MediatR support.

**Tại sao:** Centralize common functionality cho tất cả controllers.

**File:** `src/Host/Host/Controllers/BaseApiController.cs`

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

/// <summary>
/// Base API Controller với MediatR support
/// Tất cả controllers khác kế thừa từ BaseApiController
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Mặc định tất cả endpoints đều require authentication
public class BaseApiController : ControllerBase
{
    private ISender _mediator = null!;

    /// <summary>
    /// MediatR sender instance
    /// Lazy initialization: Chỉ tạo khi cần
    /// </summary>
    protected ISender Mediator => 
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
```

**Giải thích:**

**BaseApiController Features:**
- **[ApiController]:** Enable automatic model validation, binding source inference
- **[Route("api/[controller]")]:** Convention-based routing
- **[Authorize]:** Mặc định require authentication (override bằng [AllowAnonymous] nếu cần)
- **Mediator Property:** Lazy initialization, inject từ DI container

**Why Lazy Initialization:**
- Chỉ tạo instance khi controller action cần MediatR
- Giảm overhead cho endpoints không dùng MediatR
- Thread-safe với null-coalescing operator

---

## 3. TokensController - Authentication Endpoints

### Bước 3.1: TokensController Implementation

**Làm gì:** Controller cho login và refresh token operations.

**Tại sao:** Expose authentication endpoints cho frontend.

**File:** `src/Host/Host/Controllers/Identity/TokensController.cs`

```csharp
using ECO.WebApi.Application.Identity.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace ECO.WebApi.Host.Controllers.Identity;

/// <summary>
/// Tokens Controller - Authentication endpoints
/// Endpoints: Login, Refresh token
/// </summary>
public sealed class TokensController : BaseApiController
{ 
    private readonly ITokenService _tokenService;

    public TokensController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    /// <summary>
    /// Login endpoint - Đăng nhập bằng email và password
    /// </summary>
    /// <param name="request">Token request với email và password</param>
  /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT token và refresh token</returns>
    [HttpPost("get")]
    [AllowAnonymous] // Không cần authentication để login
    [OpenApiOperation("Request an access token using credentials.", "")]
    public Task<TokenResponse> GetTokenAsync(
    TokenRequest request, 
        CancellationToken cancellationToken)
    {
        // Get client IP address
        var ipAddress = GetIpAddress();

        // Validate credentials và generate JWT token
 return _tokenService.GetTokenAsync(request, ipAddress!, cancellationToken);
    }

    /// <summary>
    /// Refresh token endpoint - Làm mới access token bằng refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New JWT token và refresh token</returns>
    [HttpPost("refresh")]
    [AllowAnonymous] // Không cần authentication để refresh
    [OpenApiOperation("Request an access token using a refresh token.", "")]
    public Task<TokenResponse> RefreshAsync(RefreshTokenRequest request)
    {
        // Get client IP address
      var ipAddress = GetIpAddress();

  // Validate refresh token và generate new JWT token
        return _tokenService.RefreshTokenAsync(request, ipAddress!);
    }

    /// <summary>
    /// Helper method để lấy IP address của client
    /// </summary>
    /// <returns>Client IP address</returns>
private string? GetIpAddress() =>
        // Check X-Forwarded-For header (nếu đằng sau proxy/load balancer)
        Request.Headers.ContainsKey("X-Forwarded-For")
      ? Request.Headers["X-Forwarded-For"]
  // Fallback sang RemoteIpAddress
       : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
}
```

**Giải thích:**

**TokensController Endpoints:**
- **POST /api/tokens/get:** Login bằng email/password
- **POST /api/tokens/refresh:** Refresh access token

**AllowAnonymous:**
- Authentication endpoints phải public
- Bất kỳ ai cũng có thể login

**GetIpAddress():**
- Lấy IP address cho audit logging
- Check X-Forwarded-For header (behind proxy)
- Fallback sang RemoteIpAddress

**OpenApiOperation:**
- Swagger documentation metadata
- Hiển thị description trong Swagger UI

---

## 4. UsersController - User Management APIs

### Bước 4.1: UsersController Implementation

**Làm gì:** Controller cho user management operations.

**Tại sao:** Expose user CRUD, role assignment, email confirmation endpoints.

**File:** `src/Host/Host/Controllers/Identity/UsersController.cs`

```csharp
using ECO.WebApi.Application.Identity.Users;
using ECO.WebApi.Application.Identity.Users.Password;
using ECO.WebApi.Infrastructure.Auth.Permissions;
using ECO.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace ECO.WebApi.Host.Controllers.Identity;

/// <summary>
/// Users Controller - User management APIs
/// Endpoints: List users, Get user, Create user, Assign roles, Email confirmation, Password reset
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
    /// Requires: Users.View permission
    /// </summary>
    [HttpGet("list")]
    [MustHavePermission(ECOAction.View, ECOFunction.User)]
    [OpenApiOperation("Get list of all users.", "")]
    public Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return _userService.GetListAsync(cancellationToken);
    }

    /// <summary>
    /// Lấy chi tiết user theo ID
    /// Requires: Users.View permission
    /// </summary>
    [HttpGet("{id}")]
    [MustHavePermission(ECOAction.View, ECOFunction.User)]
    [OpenApiOperation("Get a user's details.", "")]
    public Task<UserDetailDto> GetByIdAsync(
  string id, 
        CancellationToken cancellationToken)
    {
        return _userService.GetAsync(id, cancellationToken);
    }

  /// <summary>
    /// Lấy danh sách roles của user
    /// Requires: Users.View permission
    /// </summary>
[HttpGet("{id}/roles")]
    [MustHavePermission(ECOAction.View, ECOFunction.User)]
    [OpenApiOperation("Get a user's roles.", "")]
    public Task<List<UserRoleDto>> GetRolesAsync(
        string id, 
        CancellationToken cancellationToken)
    {
        return _userService.GetRolesAsync(id, cancellationToken);
    }

    /// <summary>
    /// Gán roles cho user
    /// Requires: Users.Update permission
    /// </summary>
    [HttpPost("{id}/roles")]
    [MustHavePermission(ECOAction.Update, ECOFunction.User)]
    [OpenApiOperation("Update a user's assigned roles.", "")]
    public Task<string> AssignRolesAsync(
   string id, 
        UserRolesRequest request, 
        CancellationToken cancellationToken)
    {
        return _userService.AssignRolesAsync(id, request, cancellationToken);
    }

    /// <summary>
    /// Tạo user mới (Admin only)
    /// Requires: Users.Create permission
/// </summary>
    [HttpPost("create")]
[MustHavePermission(ECOAction.Create, ECOFunction.User)]
    [OpenApiOperation("Creates a new user.", "")]
    public Task<string> CreateAsync(CreateUserRequest request)
    {
   // Get origin URL cho email confirmation link
        var origin = GetOriginFromRequest();
        return _userService.CreateAsync(request, origin);
    }

    /// <summary>
    /// Self-registration - User tự tạo tài khoản (Public)
    /// Anonymous endpoint - Không cần authentication
    /// </summary>
    [HttpPost("self-register")]
[AllowAnonymous]
    [OpenApiOperation("Anonymous user creates a user.", "")]
    public Task<string> SelfRegisterAsync(CreateUserRequest request)
  {
      // TODO: Check appsetting nếu cho phép self-registration
        // TODO: Add CAPTCHA protection
     var origin = GetOriginFromRequest();
  return _userService.CreateAsync(request, origin);
    }

    /// <summary>
    /// Toggle user active status (Enable/Disable user)
    /// Requires: Users.Update permission
    /// </summary>
    [HttpPost("{id}/toggle-status")]
    [MustHavePermission(ECOAction.Update, ECOFunction.User)]
    [OpenApiOperation("Toggle a user's active status.", "")]
    public async Task<ActionResult> ToggleStatusAsync(
        string id, 
        ToggleUserStatusRequest request, 
        CancellationToken cancellationToken)
    {
        // Validate ID match
        if (id != request.UserId)
   {
         return BadRequest("ID mismatch");
        }

        await _userService.ToggleStatusAsync(request, cancellationToken);
        return Ok(new { message = "User status updated successfully" });
    }

    /// <summary>
    /// Confirm email address (Public endpoint)
    /// Called from email confirmation link
  /// </summary>
    [HttpGet("confirm-email")]
    [AllowAnonymous]
    [OpenApiOperation("Confirm email address for a user.", "")]
    public Task<string> ConfirmEmailAsync(
[FromQuery] string userId, 
        [FromQuery] string code, 
 CancellationToken cancellationToken)
    {
    return _userService.ConfirmEmailAsync(userId, code, cancellationToken);
    }

    /// <summary>
    /// Confirm phone number (Public endpoint)
    /// </summary>
    [HttpGet("confirm-phone-number")]
    [AllowAnonymous]
    [OpenApiOperation("Confirm phone number for a user.", "")]
    public Task<string> ConfirmPhoneNumberAsync(
        [FromQuery] string userId, 
        [FromQuery] string code)
    {
        return _userService.ConfirmPhoneNumberAsync(userId, code);
    }

    /// <summary>
    /// Forgot password - Request password reset email (Public)
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [OpenApiOperation("Request a password reset email for a user.", "")]
    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var origin = GetOriginFromRequest();
        return _userService.ForgotPasswordAsync(request, origin);
    }

    /// <summary>
    /// Reset password (Public endpoint)
    /// Called from password reset link
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
[OpenApiOperation("Reset a user's password.", "")]
    public Task<string> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return _userService.ResetPasswordAsync(request);
    }

    /// <summary>
    /// Helper method để lấy origin URL
    /// Format: https://localhost:7001
    /// </summary>
    private string GetOriginFromRequest() =>
        $"{Request.Scheme}://{Request.Host.Value}{Request.PathBase.Value}";
}
```

**Giải thích:**

**UsersController Endpoints:**
- **GET /api/users/list:** Danh sách users (protected)
- **GET /api/users/{id}:** Chi tiết user (protected)
- **GET /api/users/{id}/roles:** Roles của user (protected)
- **POST /api/users/{id}/roles:** Assign roles (protected)
- **POST /api/users/create:** Tạo user (Admin only)
- **POST /api/users/self-register:** Self-registration (public)
- **POST /api/users/{id}/toggle-status:** Enable/Disable user (protected)
- **GET /api/users/confirm-email:** Confirm email (public)
- **POST /api/users/forgot-password:** Request password reset (public)
- **POST /api/users/reset-password:** Reset password (public)

**Permission Protection:**
- **MustHavePermission:** Check permissions before allowing access
- **AllowAnonymous:** Override [Authorize] từ BaseApiController

**GetOriginFromRequest():**
- Build origin URL cho email links
- Format: `https://localhost:7001`
- Dùng cho email confirmation và password reset links

---

## 5. RoleController - Role & Function Management

### Bước 5.1: RoleController Implementation

**Làm gì:** Controller cho role và function management.

**Tại sao:** Expose role CRUD, permission management, function CRUD endpoints.

**File:** `src/Host/Host/Controllers/Identity/RoleController.cs`

```csharp
using ECO.WebApi.Application.Identity.Roles;
using ECO.WebApi.Infrastructure.Auth.Permissions;
using ECO.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace ECO.WebApi.Host.Controllers.Identity;

/// <summary>
/// Role Controller - Role and Function management APIs
/// Endpoints: Role CRUD, Permission management, Function CRUD
/// </summary>
public class RoleController : BaseApiController
{
    private readonly IRoleService _roleService;
    private readonly IFunctionService _functionService;

    public RoleController(
        IRoleService roleService,
        IFunctionService functionService)
    {
      _roleService = roleService;
        _functionService = functionService;
    }

    #region Role Management

    /// <summary>
    /// Lấy danh sách tất cả roles
    /// Requires: Roles.View permission
    /// </summary>
    [HttpGet]
    [MustHavePermission(ECOAction.View, ECOFunction.Role)]
    [OpenApiOperation("Get a list of all roles.", "")]
    public Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return _roleService.GetListAsync(cancellationToken);
    }

    /// <summary>
  /// Lấy chi tiết role theo ID
    /// Requires: Roles.View permission
    /// </summary>
    [HttpGet("{id}")]
    [MustHavePermission(ECOAction.View, ECOFunction.Role)]
  [OpenApiOperation("Get role details.", "")]
    public Task<RoleDto> GetByIdAsync(string id)
    {
   return _roleService.GetByIdAsync(id);
    }

    /// <summary>
    /// Lấy role với danh sách permissions
/// Requires: Roles.View permission
    /// </summary>
    [HttpGet("{id}/permissions")]
    [MustHavePermission(ECOAction.View, ECOFunction.Role)]
    [OpenApiOperation("Get role details with its permissions.", "")]
  public Task<List<FunctionDto>> GetByIdWithPermissionsAsync(
        string id, 
        CancellationToken cancellationToken)
    {
        return _roleService.GetByIdWithPermissionsAsync(id, cancellationToken);
    }

    /// <summary>
    /// Cập nhật permissions cho role
    /// Requires: Roles.Update permission
    /// </summary>
    [HttpPut("{id}/permissions")]
    [MustHavePermission(ECOAction.Update, ECOFunction.Role)]
    [OpenApiOperation("Update a role's permissions.", "")]
    public async Task<ActionResult> UpdatePermissionsAsync(
        string id, 
        UpdateRolePermissionsRequest request, 
        CancellationToken cancellationToken)
    {
        // Validate ID match
        if (id != request.RoleId)
   {
            return BadRequest("ID mismatch");
   }

        var result = await _roleService.UpdatePermissionsAsync(request, cancellationToken);
        return Ok(new { message = result });
    }

 /// <summary>
    /// Tạo hoặc cập nhật role
    /// Requires: Roles.Create hoặc Roles.Update permission
    /// </summary>
    [HttpPost("create/update")]
    [MustHavePermission(ECOAction.Create, ECOFunction.Role)]
    [OpenApiOperation("Create or update a role.", "")]
    public async Task<ActionResult> RegisterRoleAsync(CreateOrUpdateRoleRequest request)
    {
        var result = await _roleService.CreateOrUpdateAsync(request);
      return Ok(new { message = result });
    }

    /// <summary>
    /// Xóa role
    /// Requires: Roles.Delete permission
    /// </summary>
    [HttpDelete("{id}")]
    [MustHavePermission(ECOAction.Delete, ECOFunction.Role)]
    [OpenApiOperation("Delete a role.", "")]
    public async Task<ActionResult> DeleteAsync(string id)
    {
        var result = await _roleService.DeleteAsync(id);
        return Ok(new { message = result });
    }

    #endregion

    #region Function Management

 /// <summary>
    /// Lấy danh sách tất cả functions
    /// Requires: Functions.View permission
    /// </summary>
    [HttpGet("functions")]
    [MustHavePermission(ECOAction.View, ECOFunction.Role)]
    [OpenApiOperation("Get a list of all functions.", "")]
    public Task<List<FunctionDto>> GetFunctionListAsync(
 CancellationToken cancellationToken)
    {
     return _functionService.GetListAsync(cancellationToken);
    }

    /// <summary>
    /// Lấy chi tiết function theo ID
    /// Requires: Functions.View permission
    /// </summary>
    [HttpGet("function/{id}")]
    [MustHavePermission(ECOAction.View, ECOFunction.Role)]
    [OpenApiOperation("Get function details.", "")]
    public Task<FunctionDto> GetFunctionByIdAsync(Guid id)
    {
        return _functionService.GetByIdAsync(id);
    }

 /// <summary>
    /// Tạo hoặc cập nhật function
 /// Requires: Functions.Create permission
    /// </summary>
    [HttpPost("function/create/update")]
    [MustHavePermission(ECOAction.Create, ECOFunction.Role)]
    [OpenApiOperation("Create or update a function.", "")]
    public async Task<ActionResult> CreateUpdateFunctionAsync(
        CreateOrUpdateFunctionRequest request)
    {
      var result = await _functionService.CreateOrUpdateAsync(request);
      return Ok(new { message = result });
    }

    /// <summary>
    /// Xóa function
    /// Requires: Functions.Delete permission
    /// </summary>
    [HttpDelete("function/{id}")]
    [MustHavePermission(ECOAction.Delete, ECOFunction.Role)]
    [OpenApiOperation("Delete a function.", "")]
    public async Task<ActionResult> DeleteFunctionAsync(Guid id)
    {
        var result = await _functionService.DeleteAsync(id);
        return Ok(new { message = result });
    }

    #endregion
}
```

**Giải thích:**

**RoleController Endpoints:**

**Role Management:**
- **GET /api/role:** Danh sách roles
- **GET /api/role/{id}:** Chi tiết role
- **GET /api/role/{id}/permissions:** Role với permissions
- **PUT /api/role/{id}/permissions:** Update permissions
- **POST /api/role/create/update:** Create/Update role
- **DELETE /api/role/{id}:** Delete role

**Function Management:**
- **GET /api/role/functions:** Danh sách functions
- **GET /api/role/function/{id}:** Chi tiết function
- **POST /api/role/function/create/update:** Create/Update function
- **DELETE /api/role/function/{id}:** Delete function

**Why Functions in RoleController:**
- Functions liên quan đến permission management
- Functions được dùng để define permissions cho roles
- Logical grouping: Role + Function trong cùng controller

---

## 6. PersonalController - Current User Profile

### Bước 6.1: PersonalController Implementation

**Làm gì:** Controller cho current user operations.

**Tại sao:** Endpoints cho user tự quản lý profile, password, permissions.

**File:** `src/Host/Host/Controllers/Personal/PersonalController.cs`

```csharp
using ECO.WebApi.Application.Auditing;
using ECO.WebApi.Application.Identity.Users;
using ECO.WebApi.Application.Identity.Users.Password;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using System.Security.Claims;

namespace ECO.WebApi.Host.Controllers.Personal;

/// <summary>
/// Personal Controller - Current user profile management
/// Endpoints: Profile, Change password, Get permissions, Audit logs
/// </summary>
public class PersonalController : BaseApiController
{
    private readonly IUserService _userService;

    public PersonalController(IUserService userService)
    {
        _userService = userService;
  }

    /// <summary>
    /// Lấy profile của current logged-in user
  /// </summary>
    [HttpGet("profile")]
    [OpenApiOperation("Get profile details of currently logged in user.", "")]
    public async Task<ActionResult<UserDetailDto>> GetProfileAsync(
        CancellationToken cancellationToken)
    {
        // Get user ID từ JWT claims
        var userId = User.GetUserId();

  if (string.IsNullOrEmpty(userId))
        {
        return Unauthorized("User ID not found in token");
        }

        var profile = await _userService.GetAsync(userId, cancellationToken);
        return Ok(profile);
    }

    /// <summary>
    /// Cập nhật profile của current logged-in user
    /// </summary>
    [HttpPut("profile")]
    [OpenApiOperation("Update profile details of currently logged in user.", "")]
    public async Task<ActionResult> UpdateProfileAsync(UpdateUserRequest request)
    {
   // Get user ID từ JWT claims
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
   {
  return Unauthorized("User ID not found in token");
        }

        await _userService.UpdateAsync(request, userId);
        return Ok(new { message = "Profile updated successfully" });
    }

    /// <summary>
    /// Đổi password của current logged-in user
    /// </summary>
    [HttpPut("change-password")]
    [OpenApiOperation("Change password of currently logged in user.", "")]
    public async Task<ActionResult> ChangePasswordAsync(ChangePasswordRequest model)
    {
        // Get user ID từ JWT claims
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

 await _userService.ChangePasswordAsync(model, userId);
 return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>
    /// Lấy danh sách permissions của current logged-in user
 /// </summary>
    [HttpGet("permissions")]
    [OpenApiOperation("Get permissions of currently logged in user.", "")]
    public async Task<ActionResult<List<string>>> GetPermissionsAsync(
        CancellationToken cancellationToken)
    {
     // Get user ID từ JWT claims
        var userId = User.GetUserId();

    if (string.IsNullOrEmpty(userId))
        {
  return Unauthorized("User ID not found in token");
        }

        var permissions = await _userService.GetPermissionsAsync(userId, cancellationToken);
        return Ok(permissions);
    }

    /// <summary>
    /// Lấy audit logs của current logged-in user
 /// </summary>
    [HttpGet("logs")]
    [OpenApiOperation("Get audit logs of currently logged in user.", "")]
    public Task<List<AuditDto>> GetLogsAsync()
 {
      // Use MediatR để gửi query
  return Mediator.Send(new GetMyAuditLogsRequest());
    }
}
```

**Giải thích:**

**PersonalController Endpoints:**
- **GET /api/personal/profile:** Get current user profile
- **PUT /api/personal/profile:** Update current user profile
- **PUT /api/personal/change-password:** Change current user password
- **GET /api/personal/permissions:** Get current user permissions
- **GET /api/personal/logs:** Get current user audit logs

**User.GetUserId():**
- Extension method lấy user ID từ JWT claims
- Claims: `ClaimTypes.NameIdentifier`
- Defined trong `ClaimsPrincipalExtensions`

**Why Separate Personal Controller:**
- Current user operations khác với admin user management
- Không cần permission checks (user luôn có quyền manage chính mình)
- Cleaner API structure

**MediatR Usage:**
- `GetMyAuditLogsRequest`: Query audit logs bằng MediatR
- Demonstrates MediatR pattern in controllers

---

## 7. ClaimsPrincipalExtensions - Helper Methods

### Bước 7.1: GetUserId Extension Method

**Làm gì:** Extension method để lấy user ID từ JWT claims.

**Tại sao:** Reusable helper cho tất cả controllers.

**File:** `src/Core/Shared/Authorization/ClaimsPrincipalExtensions.cs`

```csharp
using System.Security.Claims;

namespace ECO.WebApi.Shared.Authorization;

/// <summary>
/// ClaimsPrincipal extension methods
/// Helper methods để extract claims từ JWT
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Lấy user ID từ JWT claims
    /// Claim name: NameIdentifier
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Lấy email từ JWT claims
    /// Claim name: Email
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
    {
      return principal.FindFirstValue(ClaimTypes.Email);
    }

    /// <summary>
    /// Lấy full name từ JWT claims
    /// Claim name: ECOClaims.Fullname
  /// </summary>
    public static string? GetFullName(this ClaimsPrincipal principal)
    {
    return principal.FindFirstValue(ECOClaims.Fullname);
    }

    /// <summary>
    /// Lấy image URL từ JWT claims
    /// Claim name: ECOClaims.ImageUrl
    /// </summary>
    public static string? GetImageUrl(this ClaimsPrincipal principal)
{
        return principal.FindFirstValue(ECOClaims.ImageUrl);
    }

    /// <summary>
    /// Lấy phone number từ JWT claims
    /// Claim name: MobilePhone
    /// </summary>
    public static string? GetPhoneNumber(this ClaimsPrincipal principal)
    {
    return principal.FindFirstValue(ClaimTypes.MobilePhone);
    }

    /// <summary>
    /// Check xem user có claim cụ thể không
    /// </summary>
    public static bool HasClaim(this ClaimsPrincipal principal, string claimType)
  {
        return principal.Claims.Any(c => c.Type == claimType);
    }

    /// <summary>
    /// Check xem user có permission cụ thể không
 /// </summary>
    public static bool HasPermission(this ClaimsPrincipal principal, string permission)
    {
        return principal.Claims
    .Any(c => c.Type == ECOClaims.Permission && c.Value == permission);
    }
}
```

**Giải thích:**

**Extension Methods:**
- **GetUserId():** Lấy user ID (primary key)
- **GetEmail():** Lấy email
- **GetFullName():** Lấy full name
- **GetImageUrl():** Lấy avatar URL
- **GetPhoneNumber():** Lấy phone number
- **HasClaim():** Check claim existence
- **HasPermission():** Check permission

**Usage in Controllers:**
```csharp
// In PersonalController
public Task<UserDetailDto> GetProfileAsync()
{
    var userId = User.GetUserId(); // Extension method
    return _userService.GetAsync(userId);
}

// Check permission
if (User.HasPermission("Permissions.Users.View"))
{
    // User có quyền Users.View
}
```

---

## 8. Testing Controllers (Kiểm thử Controllers)

### Bước 8.1: Testing với Swagger UI

**Step 1: Run API**

```bash
cd src/Host/Host
dotnet run
```

**Step 2: Open Swagger UI**

```
https://localhost:7001/swagger
```

**Step 3: Test Authentication**

**Login:**
```http
POST /api/tokens/get
{
  "email": "admin@root.com",
  "password": "123Pa$$word!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "CfDJ8O...",
  "refreshTokenExpiryTime": "2024-03-01T10:00:00Z"
}
```

**Step 4: Authorize in Swagger**

1. Click **"Authorize"** button (top right)
2. Enter: `Bearer {token}`
3. Click **"Authorize"**
4. Click **"Close"**

**Step 5: Test Protected Endpoints**

**Get Users List:**
```http
GET /api/users/list
```

**Get Current User Profile:**
```http
GET /api/personal/profile
```

**Get Roles:**
```http
GET /api/role
```

---

### Bước 8.2: Testing với Postman

**Import Collection:**

```json
{
  "info": {
    "name": "ECO.WebApi Identity APIs",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Authentication",
      "item": [
        {
        "name": "Login",
      "request": {
"method": "POST",
      "header": [],
        "body": {
            "mode": "raw",
      "raw": "{\n  \"email\": \"admin@root.com\",\n  \"password\": \"123Pa$$word!\"\n}",
     "options": {
    "raw": {
       "language": "json"
   }
      }
    },
            "url": {
   "raw": "https://localhost:7001/api/tokens/get",
        "protocol": "https",
   "host": ["localhost"],
  "port": "7001",
           "path": ["api", "tokens", "get"]
    }
          }
        },
        {
     "name": "Refresh Token",
          "request": {
     "method": "POST",
      "header": [],
            "body": {
     "mode": "raw",
    "raw": "{\n  \"token\": \"{{token}}\",\n  \"refreshToken\": \"{{refreshToken}}\"\n}",
   "options": {
 "raw": {
   "language": "json"
       }
   }
  },
          "url": {
       "raw": "https://localhost:7001/api/tokens/refresh",
        "protocol": "https",
          "host": ["localhost"],
        "port": "7001",
              "path": ["api", "tokens", "refresh"]
            }
          }
        }
      ]
    },
    {
   "name": "Users",
   "item": [
        {
 "name": "Get Users List",
          "request": {
            "method": "GET",
            "header": [
            {
         "key": "Authorization",
       "value": "Bearer {{token}}"
     }
    ],
  "url": {
              "raw": "https://localhost:7001/api/users/list",
          "protocol": "https",
          "host": ["localhost"],
          "port": "7001",
      "path": ["api", "users", "list"]
         }
     }
 },
        {
       "name": "Get User by ID",
 "request": {
     "method": "GET",
            "header": [
 {
        "key": "Authorization",
                "value": "Bearer {{token}}"
              }
 ],
     "url": {
          "raw": "https://localhost:7001/api/users/{{userId}}",
         "protocol": "https",
            "host": ["localhost"],
       "port": "7001",
    "path": ["api", "users", "{{userId}}"]
    }
  }
    }
      ]
    },
    {
      "name": "Personal",
  "item": [
        {
          "name": "Get My Profile",
        "request": {
            "method": "GET",
            "header": [
      {
            "key": "Authorization",
                "value": "Bearer {{token}}"
        }
      ],
  "url": {
     "raw": "https://localhost:7001/api/personal/profile",
    "protocol": "https",
              "host": ["localhost"],
              "port": "7001",
        "path": ["api", "personal", "profile"]
      }
 }
        },
        {
   "name": "Change Password",
      "request": {
    "method": "PUT",
   "header": [
       {
     "key": "Authorization",
           "value": "Bearer {{token}}"
      }
  ],
 "body": {
  "mode": "raw",
      "raw": "{\n  \"password\": \"123Pa$$word!\",\n  \"newPassword\": \"NewPass123!\",\n  \"confirmNewPassword\": \"NewPass123!\"\n}",
       "options": {
     "raw": {
     "language": "json"
       }
 }
       },
     "url": {
   "raw": "https://localhost:7001/api/personal/change-password",
        "protocol": "https",
        "host": ["localhost"],
   "port": "7001",
    "path": ["api", "personal", "change-password"]
    }
       }
        }
      ]
    }
  ],
  "variable": [
    {
  "key": "token",
      "value": ""
    },
    {
   "key": "refreshToken",
      "value": ""
    },
  {
      "key": "userId",
      "value": ""
    }
  ]
}
```

---

## 9. Summary (Tổng kết)

### ✅ Đã hoàn thành trong bước này:

**Base Controller:**
- ✅ BaseApiController với MediatR support
- ✅ Convention-based routing
- ✅ Default [Authorize] attribute

**Authentication Controllers:**
- ✅ TokensController (Login, Refresh token)
- ✅ AuthController (OAuth2 - Google, Facebook)

**User Management Controllers:**
- ✅ UsersController (User CRUD, Roles, Email confirmation, Password reset)
- ✅ PersonalController (Current user profile, Change password, Permissions)

**Role Management Controllers:**
- ✅ RoleController (Role CRUD, Permission management, Function CRUD)

**Helper Extensions:**
- ✅ ClaimsPrincipalExtensions (GetUserId, GetEmail, HasPermission)

**OpenAPI Documentation:**
- ✅ OpenApiOperation attributes cho tất cả endpoints
- ✅ Swagger UI integration

**Testing:**
- ✅ Swagger UI testing guide
- ✅ Postman collection

### 📊 Complete Controllers Structure (Cấu trúc Controllers Hoàn chỉnh):

```
src/Host/Host/Controllers/
├── BaseApiController.cs
├── Identity/
│   ├── TokensController.cs
│   │   ├── POST /api/tokens/get (Login)
│   │   └── POST /api/tokens/refresh (Refresh)
│   ├── AuthController.cs
│   │   ├── POST /api/auth/google (Google login)
│   │   └── POST /api/auth/facebook (Facebook login)
│   ├── UsersController.cs
│   │   ├── GET /api/users/list
│   │   ├── GET /api/users/{id}
│   │   ├── GET /api/users/{id}/roles
│   │   ├── POST /api/users/{id}/roles
│   │   ├── POST /api/users/create
│   │   ├── POST /api/users/self-register
│   │   ├── POST /api/users/{id}/toggle-status
│   │   ├── GET /api/users/confirm-email
│   │   ├── POST /api/users/forgot-password
│   │   └── POST /api/users/reset-password
│   └── RoleController.cs
│       ├── GET /api/role
│       ├── GET /api/role/{id}
│       ├── GET /api/role/{id}/permissions
│       ├── PUT /api/role/{id}/permissions
│       ├── POST /api/role/create/update
│       ├── DELETE /api/role/{id}
│       ├── GET /api/role/functions
│    ├── GET /api/role/function/{id}
│     ├── POST /api/role/function/create/update
│       └── DELETE /api/role/function/{id}
└── Personal/
    └── PersonalController.cs
        ├── GET /api/personal/profile
        ├── PUT /api/personal/profile
   ├── PUT /api/personal/change-password
        ├── GET /api/personal/permissions
        └── GET /api/personal/logs
```

### 📌 Key Concepts (Khái niệm Chính):

**Controller Responsibilities (Trách nhiệm Controller):**
- **Thin Controllers:** Chỉ route requests, không chứa business logic
- **Dependency Injection:** Inject services qua constructor
- **Model Validation:** Automatic validation với [ApiController]
- **Response Format:** Consistent JSON responses

**Authentication & Authorization:**
- **[Authorize]:** Default require authentication
- **[AllowAnonymous]:** Override cho public endpoints
- **[MustHavePermission]:** Permission-based authorization

**RESTful API Conventions:**
- **GET:** Retrieve data
- **POST:** Create data
- **PUT:** Update data
- **DELETE:** Delete data
- **HTTP Status Codes:** 200 OK, 400 Bad Request, 401 Unauthorized, 403 Forbidden

**Swagger Documentation:**
- **[OpenApiOperation]:** Endpoint description
- **XML Comments:** Detailed documentation
- **Swagger UI:** Interactive API testing

### 📁 Complete File Structure (Cấu trúc File Hoàn chỉnh):

```
src/
├── Core/
│   └── Shared/
│       └── Authorization/
│   └── ClaimsPrincipalExtensions.cs
└── Host/
 └── Host/
└── Controllers/
       ├── BaseApiController.cs
 ├── Identity/
            │   ├── TokensController.cs
            │ ├── AuthController.cs
      │   ├── UsersController.cs
       │   └── RoleController.cs
     └── Personal/
└── PersonalController.cs
```

---

## 10. Next Steps (Các Bước Tiếp theo)

**Tiếp theo:** [BUILD_17 - Permission Authorization](BUILD_17_Permission_Authorization.md)

Trong bước tiếp theo, chúng ta đã hoàn thành:
1. ✅ PermissionRequirement (IAuthorizationRequirement)
2. ✅ PermissionAuthorizationHandler (check permissions)
3. ✅ PermissionPolicyProvider (dynamic policy creation)
4. ✅ MustHavePermissionAttribute (declarative attribute)

**Sau BUILD_17:** [BUILD_18 - OAuth2 Integration](BUILD_18_OAuth2_Integration.md)

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
