# Role Management Service - Role CRUD & Permission Management

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 16A (User Service) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Role Management Service - Quản lý roles và permissions với table-based approach.

---

## 1. Overview

**Làm gì:** Xây dựng Role Management Service để quản lý roles và permissions (Create, Read, Update, Delete, Assign Permissions).

**Tại sao cần:**
- **Role Management:** CRUD operations cho roles
- **Permission Management:** Assign permissions to roles với table-based approach (Permission table)
- **Flexible Authorization:** Dynamic permission assignment không cần code changes
- **Security:** Protect default roles (Admin, Basic) khỏi modifications
- **User Role Assignment:** Support UserService assign roles to users

**Trong bước này chúng ta sẽ:**
- ✅ Tạo IRoleService interface
- ✅ Tạo Role DTOs (RoleDto, CreateOrUpdateRoleRequest, UpdateRolePermissionsRequest)
- ✅ Implement RoleService với các operations:
  - Get roles list
  - Get role details với permissions
  - Create/Update roles
  - Update role permissions (table-based approach)
  - Delete roles với validation
- ✅ Tạo RoleController với RESTful endpoints
- ✅ FluentValidation cho tất cả requests
- ✅ Update UserService.Role.cs (assign roles to users)

**Real-world example:**
```csharp
// Admin creates new role
var createRequest = new CreateOrUpdateRoleRequest
{
    Name = "Manager",
    Description = "Store Manager Role"
};

var message = await _roleService.CreateOrUpdateAsync(createRequest);
// → Role Manager Created.

// Admin assigns permissions to role
var updatePermissionsRequest = new UpdateRolePermissionsRequest
{
    RoleId = roleId,
    Permissions = new List<PermissionRequest>
    {
        new() { FunctionId = usersFunction, ActionId = viewAction },
        new() { FunctionId = usersFunction, ActionId = createAction },
        new() { FunctionId = productsFunction, ActionId = viewAction }
    }
};

await _roleService.UpdatePermissionsAsync(updatePermissionsRequest, cancellationToken);
// → Permissions Updated.

// Get role with permissions
var functionsWithPermissions = await _roleService.GetByIdWithPermissionsAsync(roleId, cancellationToken);
// → Returns list of Functions with Actions marked as Selected or not
```

---

## 2. Role DTOs

### Bước 2.1: RoleDto

**Làm gì:** DTO để hiển thị thông tin role.

**Tại sao:** Không expose toàn bộ ApplicationRole entity, chỉ trả về fields cần thiết.

**File:** `src/Core/Application/Identity/Roles/RoleDto.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Role detail DTO (dùng cho responses)
/// </summary>
public class RoleDto
{
    /// <summary>
    /// Role ID (string - Identity framework)
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Role name (unique)
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of permission strings (optional, for quick display)
    /// Format: "Function.Action" (e.g., "Users.View", "Products.Create")
    /// </summary>
    public List<string>? Permissions { get; set; }
}
```

**Giải thích:**
- **Id:** Role ID dạng string (ASP.NET Core Identity convention)
- **Name:** Role name (unique, e.g., "Admin", "Manager")
- **Description:** Human-readable description
- **Permissions:** Optional list of permission strings for quick display

---

### Bước 2.2: CreateOrUpdateRoleRequest

**Làm gì:** Request DTO để tạo hoặc update role.

**Tại sao:** Single endpoint cho both Create và Update operations.

**File:** `src/Core/Application/Identity/Roles/CreateOrUpdateRoleRequest.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Request để tạo hoặc update role
/// </summary>
public class CreateOrUpdateRoleRequest
{
    /// <summary>
    /// Role ID (null = create, not null = update)
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Role name (required, unique)
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description (optional)
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Validator cho CreateOrUpdateRoleRequest
/// </summary>
public class CreateOrUpdateRoleRequestValidator : AbstractValidator<CreateOrUpdateRoleRequest>
{
    public CreateOrUpdateRoleRequestValidator(IRoleService roleService)
    {
            RuleFor(r => r.Name)
           .NotEmpty()
           .WithMessage("Role name is required.")
           .MustAsync(async (role, name, _) => !await roleService.ExistsAsync(name, role.Id))
           .WithMessage("Similar Role already exists.");
    }
}
```

**Giải thích:**

**Validation Rules:**
- **Name:** Required, unique (exclude current role nếu update)

**Create vs Update Logic:**
- **Id == null:** Create new role
- **Id != null:** Update existing role

**Tại sao single endpoint:**
- Simplified API (one endpoint cho both operations)
- Frontend không cần biết create/update logic
- RESTful pattern (POST /api/roles/create/update)

---

### Bước 2.3: UpdateRolePermissionsRequest

**Làm gì:** Request DTO để update permissions của role (table-based approach).

**Tại sao:** Separate endpoint cho permission management (complex operation).

**File:** `src/Core/Application/Identity/Roles/UpdateRolePermissionsRequest.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Request để update permissions của role (table-based approach)
/// </summary>
public class UpdateRolePermissionsRequest
{
    /// <summary>
    /// Role ID (required)
    /// </summary>
    public string RoleId { get; set; } = default!;

    /// <summary>
    /// List of permissions (Function + Action combinations)
    /// </summary>
    public List<PermissionRequest> Permissions { get; set; } = default!;
}

/// <summary>
/// Permission request (Function + Action combination)
/// Represents a row in Permission table
/// </summary>
public class PermissionRequest
{
    /// <summary>
    /// Function ID (e.g., Users, Products, Orders)
    /// </summary>
    public Guid FunctionId { get; set; } = default!;

    /// <summary>
    /// Action ID (e.g., View, Create, Update, Delete)
    /// </summary>
    public Guid ActionId { get; set; } = default!;
}

/// <summary>
/// Validator cho UpdateRolePermissionsRequest
/// </summary>
public class UpdateRolePermissionsRequestValidator : AbstractValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
         RuleFor(r => r.RoleId)
            .NotEmpty()
            .WithMessage("Role ID is required.");

        RuleFor(r => r.Permissions)
            .NotNull()
            .WithMessage("Permissions list is required.");
    }
}
```

**Giải thích:**

**Table-Based Approach:**
- **Permission Table:** Stores (RoleId, FunctionId, ActionId) combinations
- **Flexible:** Add/remove permissions dynamically without code changes
- **Database-driven:** Permissions stored in database, not hardcoded

**UpdateRolePermissionsRequest:**
- **RoleId:** Target role
- **Permissions:** List of Function+Action combinations

**PermissionRequest:**
- Represents một permission entry trong Permission table
- **FunctionId:** Module/Feature (e.g., Users, Products)
- **ActionId:** Operation (e.g., View, Create, Update, Delete)

**Update Flow:**
1. Remove all current permissions for role
2. Add new permissions từ request
3. Return success message

---

### Bước 2.4: FunctionDto và ActionDto

**Làm gì:** DTOs để hiển thị Functions với Actions (for permission UI).

**Tại sao:** Frontend cần biết available Functions và Actions để display checkboxes.

**File:** `src/Core/Application/Identity/Roles/FunctionDto.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Function DTO (represents a module/feature)
/// </summary>
public class FunctionDto
{
    /// <summary>
    /// Function ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Function name (e.g., "Users", "Products", "Orders")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// List of actions available for this function
    /// </summary>
    public List<ActionDto> ActionDtos { get; set; } = default!;
}
```

**File:** `src/Core/Application/Identity/Roles/ActionDto.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Action DTO (represents an operation)
/// </summary>
public class ActionDto
{
  /// <summary>
    /// Action ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Is this action selected for current role (checkbox state)
    /// </summary>
    public bool Selected { get; set; }
}
```

**Giải thích:**

**FunctionDto:**
- Represents một module/feature trong system
- **ActionDtos:** List các actions có thể thực hiện trên function này

**ActionDto:**
- Represents một operation (View, Create, Update, Delete...)
- **Selected:** Checkbox state cho UI (true = role has this permission)

**UI Example:**
```
Users Function
  ☑ View
  ☑ Create
  ☐ Update
  ☐ Delete

Products Function
  ☑ View
  ☐ Create
  ☐ Update
  ☐ Delete
```

---

## 3. Role Service Interface

### Bước 3.1: IRoleService Interface

**Làm gì:** Define contract cho role operations.

**Tại sao:** Abstraction, dễ test, dễ swap implementations.

**File:** `src/Core/Application/Identity/Roles/IRoleService.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Service xử lý role management operations
/// </summary>
public interface IRoleService : ITransientService
{
    /// <summary>
    /// Get list tất cả roles
    /// </summary>
    Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get total role count
    /// </summary>
  Task<int> GetCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Check role name đã tồn tại chưa (exclude excludeId nếu có)
 /// </summary>
    Task<bool> ExistsAsync(string roleName, string? excludeId);

    /// <summary>
    /// Get role details by ID
    /// </summary>
    Task<RoleDto> GetByIdAsync(string id);

    /// <summary>
    /// Get role details với permissions (Functions + Actions)
 /// Returns list of Functions with Actions marked as Selected or not
    /// </summary>
    Task<List<FunctionDto>> GetByIdWithPermissionsAsync(string roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Create hoặc update role
    /// </summary>
    Task<string> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request);

    /// <summary>
    /// Update permissions của role (table-based approach)
    /// Replaces all current permissions with new ones
    /// </summary>
    Task<string> UpdatePermissionsAsync(
        UpdateRolePermissionsRequest request, 
     CancellationToken cancellationToken);

    /// <summary>
    /// Delete role với validation
    /// Cannot delete default roles (Admin, Basic)
    /// Cannot delete roles đang được users sử dụng
    /// </summary>
    Task<string> DeleteAsync(string id);
}
```

**Giải thích:**

**Core Operations:**
- **GetListAsync:** Get all roles (for dropdown, list display)
- **GetCountAsync:** Total count
- **ExistsAsync:** Check uniqueness (for validation)
- **GetByIdAsync:** Get role details
- **GetByIdWithPermissionsAsync:** Get role WITH permissions (for permission UI)

**Create & Update:**
- **CreateOrUpdateAsync:** Single method cho both create/update

**Permission Management:**
- **UpdatePermissionsAsync:** Update permissions (replace all current permissions)

**Delete:**
- **DeleteAsync:** Delete với validation rules

**Tại sao ITransientService:**
- Role operations không có state
- Short-lived service per request
- Thread-safe

---

## 4. Role Service Implementation

### Bước 4.1: RoleService Implementation

**Làm gì:** Implement role management operations.

**Tại sao:** Business logic cho role và permission management.

**File:** `src/Infrastructure/Infrastructure/Identity/RoleService.cs`

```csharp
using ECO.WebApi.Application.Common.Events;
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Application.Identity.Roles;
using ECO.WebApi.Domain.Identity;
using ECO.WebApi.Infrastructure.Persistence.Context;
using ECO.WebApi.Shared.Authorization;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Infrastructure.Identity;

/// <summary>
/// Service xử lý role management operations
/// </summary>
internal class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IEventPublisher _events;
    private readonly IFunctionService _functionService;

    public RoleService(
        RoleManager<ApplicationRole> roleManager,
     UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ICurrentUser currentUser,
        IEventPublisher events,
        IFunctionService functionService)
    {
        _roleManager = roleManager;
       _userManager = userManager;
       _db = db;
        _currentUser = currentUser;
        _events = events;
        _functionService = functionService;
    }

    /// <summary>
    /// Get list tất cả roles
    /// </summary>
    public async Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return (await _roleManager.Roles.ToListAsync(cancellationToken))
            .Adapt<List<RoleDto>>();
    }

    /// <summary>
    /// Get total role count
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return await _roleManager.Roles.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Check role name đã tồn tại chưa (exclude excludeId nếu update)
    /// </summary>
    public async Task<bool> ExistsAsync(string roleName, string? excludeId)
    {
        return await _roleManager.FindByNameAsync(roleName)
               is ApplicationRole existingRole
               && existingRole.Id != excludeId;
    }

    /// <summary>
    /// Get role details by ID
    /// </summary>
    public async Task<RoleDto> GetByIdAsync(string id)
    {
        return await _db.Roles.SingleOrDefaultAsync(x => x.Id == id) is { } role
         ? role.Adapt<RoleDto>()
         : throw new NotFoundException("Role Not Found");
    }

    /// <summary>
    /// Get role details với permissions (Functions + Actions)
    /// Returns list of Functions with Actions marked as Selected or not
    /// </summary>
    public async Task<List<FunctionDto>> GetByIdWithPermissionsAsync(
        string roleId, 
     CancellationToken cancellationToken)
    {
        // Get all functions với actions
        var functions = await _db.Functions
            .Include(f => f.ActionInFunctions)
            .ThenInclude(x => x.Action)
            .ToListAsync(cancellationToken);

        // Get permissions cho role này (từ Permission table)
        var permissions = await _db.Permissions
            .Where(p => p.RoleId == roleId)
            .ToListAsync(cancellationToken);

        // Build FunctionDto list với Selected flags
        var functionDtos = new List<FunctionDto>();

        foreach (var function in functions)
        {
            var functionDto = new FunctionDto
            {
                Id = function.Id,
                Name = function.Name,
                ActionDtos = function.ActionInFunctions.Select(aif => new ActionDto
                    {
                         Id = aif.Action.Id,
                         Name = aif.Action.Name,
                        // Check nếu permission exists trong Permission table
                         Selected = permissions.Any(p => 
                         p.FunctionId == function.Id && 
                         p.ActionId == aif.Action.Id)
                     }).ToList()
             };
            functionDtos.Add(functionDto);
         }

        return functionDtos;
    }

    /// <summary>
    /// Create hoặc update role
    /// </summary>
    public async Task<string> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
             {
                // Create new role
                var role = new ApplicationRole(request.Name, request.Description);
                var result = await _roleManager.CreateAsync(role);

                if (!result.Succeeded)
                {
                    throw new InternalServerException(
                        "Register role failed", 
                        result.Errors.Select(e => e.Description).ToList());
                 }

        return $"Role {request.Name} Created.";
        }
        else
        {
            // Update existing role
            var role = await _roleManager.FindByIdAsync(request.Id);

            _ = role ?? throw new NotFoundException("Role Not Found");

            // Cannot update default roles
            if (ECORoles.IsDefault(role.Name!))
            {
                throw new ConflictException($"Not allowed to modify {role.Name} Role.");
            }

            role.Name = request.Name;
            role.NormalizedName = request.Name.ToUpperInvariant();
            role.Description = request.Description;

            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                throw new InternalServerException(
                "Update role failed", 
                result.Errors.Select(e => e.Description).ToList());
            }

            return $"Role {role.Name} Updated.";
        }
    }

    /// <summary>
    /// Update permissions của role (table-based approach)
    /// Replaces all current permissions with new ones
    /// </summary>
    public async Task<string> UpdatePermissionsAsync(
        UpdateRolePermissionsRequest request, 
        CancellationToken cancellationToken)
    {
      var role = await _roleManager.FindByIdAsync(request.RoleId);
      _ = role ?? throw new NotFoundException("Role Not Found");

        // Cannot update Admin role permissions
        if (role.Name == ECORoles.Admin)
        {
          throw new ConflictException("Not allowed to modify Permissions for this Role.");
        }

  // Remove all current permissions
        var currentPermissions = await _db.Permissions
                                        .Where(p => p.RoleId == role.Id)
                                        .ToListAsync(cancellationToken);

   _db.Permissions.RemoveRange(currentPermissions);
        await _db.SaveChangesAsync(cancellationToken);

        // Add new permissions từ request
        foreach (var permissionRequest in request.Permissions)
        {
             if (permissionRequest.FunctionId != Guid.Empty && 
             permissionRequest.ActionId != Guid.Empty)
             {
                _db.Permissions.Add(new Permission(
                                 role.Id, 
                                 permissionRequest.FunctionId, 
                                 permissionRequest.ActionId));
             }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return "Permissions Updated.";
    }

    /// <summary>
    /// Delete role với validation
    /// </summary>
    public async Task<string> DeleteAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);

         _ = role ?? throw new NotFoundException("Role Not Found");

        // Cannot delete default roles
        if (ECORoles.IsDefault(role.Name!))
        {
             throw new ConflictException($"Not allowed to delete {role.Name} Role.");
        }

        // Cannot delete role đang được users sử dụng
        if ((await _userManager.GetUsersInRoleAsync(role.Name!)).Count > 0)
        {
             throw new ConflictException(
             $"Not allowed to delete {role.Name} Role as it is being used.");
    }

      await _roleManager.DeleteAsync(role);

      return $"Role {role.Name} Deleted.";
    }
}
```

**Giải thích:**

**Dependencies:**
- **RoleManager:** ASP.NET Core Identity role management
- **UserManager:** Check users in role
- **ApplicationDbContext:** Direct database access cho Permission table
- **IFunctionService:** Get functions list (sẽ implement trong BUILD_16C)

**GetByIdWithPermissionsAsync:**
1. Get all functions với actions (from Function + ActionInFunction tables)
2. Get permissions for role (from Permission table)
3. Build FunctionDto list với Selected flags
4. Selected = true nếu permission exists trong Permission table

**UpdatePermissionsAsync (Table-Based Approach):**
1. Validate role exists và không phải Admin
2. Remove ALL current permissions (clear Permission table entries)
3. Add new permissions từ request (insert new rows vào Permission table)
4. Return success message

**CreateOrUpdateAsync:**
- **Create:** `_roleManager.CreateAsync()`
- **Update:** Update Name, NormalizedName, Description
- Protect default roles (Admin, Basic)

**DeleteAsync:**
- Validate role exists
- Cannot delete default roles
- Cannot delete roles being used by users
- `_roleManager.DeleteAsync()`

**Tại sao Table-Based Approach:**
- **Flexible:** Add/remove permissions without code changes
- **Dynamic:** Permissions stored in database
- **UI-friendly:** Easy to display checkboxes
- **Scalable:** Supports custom permissions per role

---

## 5. User Service - Role Operations

### Bước 5.1: UserService.Role.cs (Partial Class)

**Làm gì:** Implement user role operations (assign roles to users).

**Tại sao:** Users cần roles để access resources.

**File:** `src/Infrastructure/Infrastructure/Identity/UserService.Role.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Identity.Users;
using ECO.WebApi.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Role Operations (Partial Class)
/// </summary>
internal partial class UserService
{
    /// <summary>
    /// Get user's assigned roles
    /// </summary>
    public async Task<List<UserRoleDto>> GetRolesAsync(
        string userId, 
        CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
  .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Get user's roles
var userRoles = await _userManager.GetRolesAsync(user);

        // Get all available roles
        var allRoles = await _roleManager.Roles.ToListAsync(cancellationToken);

      var roleDtos = allRoles.Select(role => new UserRoleDto
     {
       RoleId = role.Id,
  RoleName = role.Name!,
          Description = role.Description,
        Enabled = userRoles.Contains(role.Name!) // Check if user has this role
        }).ToList();

    return roleDtos;
    }

    /// <summary>
    /// Assign roles to user
    /// Replaces all current roles with new ones
    /// </summary>
    public async Task<string> AssignRolesAsync(
        string userId, 
        UserRolesRequest request, 
        CancellationToken cancellationToken)
  {
   ArgumentNullException.ThrowIfNull(request, nameof(request));

    var user = await _userManager.Users
 .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Check if Admin role is being assigned/removed for current user
        if (await _userManager.IsInRoleAsync(user, ECORoles.Admin)
      && (request.UserRoles.FirstOrDefault(r => r.RoleName == ECORoles.Admin) is not { Enabled: true }))
    {
     throw new ConflictException("Admin users cannot remove their own Admin role.");
     }

        // Remove all current roles
        var currentRoles = await _userManager.GetRolesAsync(user);
  foreach (var role in currentRoles)
    {
  await _userManager.RemoveFromRoleAsync(user, role);
     }

        // Add new roles từ request (where Enabled = true)
    foreach (var roleRequest in request.UserRoles.Where(r => r.Enabled))
  {
      var role = await _roleManager.FindByNameAsync(roleRequest.RoleName);
if (role != null)
     {
                await _userManager.AddToRoleAsync(user, role.Name!);
    }
        }

        return "User Roles Updated Successfully.";
    }
}
```

**Giải thích:**

**GetRolesAsync:**
1. Find user
2. Get user's current roles (`_userManager.GetRolesAsync()`)
3. Get all available roles
4. Build UserRoleDto list với Enabled flags
5. **Enabled = true** nếu user has role

**AssignRolesAsync:**
1. Validate user exists
2. Check Admin protection (cannot remove own Admin role)
3. Remove ALL current roles
4. Add new roles where Enabled = true
5. Return success message

**Tại sao replace all roles:**
- Simpler logic (clear + add)
- No need to diff current vs new
- Matches UI pattern (checkboxes)

---

### Bước 5.2: UserRolesRequest và UserRoleDto

**File:** `src/Core/Application/Identity/Users/UserRolesRequest.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Request để assign roles to user
/// </summary>
public class UserRolesRequest
{
    /// <summary>
    /// List of roles với Enabled flags
    /// </summary>
    public List<UserRoleDto> UserRoles { get; set; } = default!;
}
```

**File:** `src/Core/Application/Identity/Users/UserRoleDto.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// User role DTO (for assign roles UI)
/// </summary>
public class UserRoleDto
{
    /// <summary>
    /// Role ID
    /// </summary>
    public string RoleId { get; set; } = default!;

    /// <summary>
    /// Role name
    /// </summary>
    public string RoleName { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Is this role assigned to user (checkbox state)
    /// </summary>
    public bool Enabled { get; set; }
}
```

**Giải thích:**
- **UserRoleDto:** Represents một role với checkbox state
- **Enabled:** true = user has this role, false = user doesn't have

**UI Example:**
```
☑ Admin
☐ Manager
☑ Basic
☐ Customer
```

---

## 6. Role Controller

### Bước 6.1: RoleController Implementation

**Làm gì:** Expose role management APIs.

**Tại sao:** RESTful endpoints cho role operations.

**File:** `src/Host/Host/Controllers/Identity/RoleController.cs`

```csharp
using ECO.WebApi.Application.Identity.Roles;
using NSwag.Annotations;

namespace ECO.WebApi.Host.Controllers.Identity;

/// <summary>
/// Role management APIs
/// </summary>
public class RoleController : BaseApiController
{
    private readonly IRoleService _roleService;
    private readonly IFunctionService _functionService;

    public RoleController(IRoleService roleService, IFunctionService functionService)
    {
        _roleService = roleService;
        _functionService = functionService;
 }

    /// <summary>
    /// Get list of all roles
    /// </summary>
    [HttpGet]
    [OpenApiOperation("Get a list of all roles.", "")]
    public Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken)
    {
     return _roleService.GetListAsync(cancellationToken);
    }

    /// <summary>
  /// Get role details by ID
    /// </summary>
    [HttpGet("{id}")]
    [OpenApiOperation("Get role details.", "")]
    public Task<RoleDto> GetByIdAsync(string id)
    {
   return _roleService.GetByIdAsync(id);
    }

    /// <summary>
    /// Get role details với permissions (for permission UI)
    /// Returns list of Functions with Actions marked as Selected or not
    /// </summary>
    [HttpGet("{id}/permissions")]
    [OpenApiOperation("Get role details with its permissions.", "")]
    public Task<List<FunctionDto>> GetByIdWithPermissionsAsync(
        string id, 
        CancellationToken cancellationToken)
    {
        return _roleService.GetByIdWithPermissionsAsync(id, cancellationToken);
    }

    /// <summary>
    /// Update role's permissions (table-based approach)
    /// </summary>
    [HttpPut("{id}/permissions")]
    [OpenApiOperation("Update a role's permissions.", "")]
    public async Task<ActionResult> UpdatePermissionsAsync(
        string id, 
   UpdateRolePermissionsRequest request, 
      CancellationToken cancellationToken)
    {
        if (id != request.RoleId)
        {
 return BadRequest();
  }

        var result = await _roleService.UpdatePermissionsAsync(request, cancellationToken);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Create hoặc update role
    /// </summary>
    [HttpPost("create/update")]
    [OpenApiOperation("Create or update a role.", "")]
    public async Task<ActionResult> RegisterRoleAsync(CreateOrUpdateRoleRequest request)
    {
        var result = await _roleService.CreateOrUpdateAsync(request);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Delete role
    /// </summary>
    [HttpDelete("{id}")]
    [OpenApiOperation("Delete a role.", "")]
public async Task<ActionResult> DeleteAsync(string id)
    {
        var result = await _roleService.DeleteAsync(id);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Get list of all functions (for permission UI)
    /// </summary>
    [HttpGet("functions")]
    [OpenApiOperation("Get a list of all functions.", "")]
    public Task<List<FunctionDto>> GetFunctionListAsync(CancellationToken cancellationToken)
  {
        return _functionService.GetListAsync(cancellationToken);
    }

    /// <summary>
    /// Get function details by ID
    /// </summary>
    [HttpGet("function/{id}")]
  [OpenApiOperation("Get function details.", "")]
    public Task<FunctionDto> GetFunctionByIdAsync(Guid id)
    {
        return _functionService.GetByIdAsync(id);
    }

    /// <summary>
    /// Create hoặc update function
    /// </summary>
    [HttpPost("function/create/update")]
  [OpenApiOperation("Create or update a function.", "")]
    public async Task<ActionResult> CreateUpdateFunctionAsync(CreateOrUpdateFunctionRequest request)
    {
      var result = await _functionService.CreateOrUpdateAsync(request);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Delete function
    /// </summary>
    [HttpDelete("function/{id}")]
    [OpenApiOperation("Delete a function.", "")]
    public async Task<ActionResult> DeleteFunctionAsync(Guid id)
    {
        var result = await _functionService.DeleteAsync(id);
     return Ok(new { message = result });
    }
}
```

**Giải thích:**

**GET /api/role:**
- Get all roles

**GET /api/role/{id}:**
- Get role details

**GET /api/role/{id}/permissions:**
- Get role WITH permissions (Functions + Actions với Selected flags)
- Dùng cho permission UI

**PUT /api/role/{id}/permissions:**
- Update permissions
- Check id match request.RoleId

**POST /api/role/create/update:**
- Single endpoint cho create/update

**DELETE /api/role/{id}:**
- Delete role với validation

**Function Endpoints:**
- Nested under /api/role/functions (for organization)
- Sẽ implement trong BUILD_16C

---

## 7. Summary

### ✅ Đã hoàn thành trong bước này:

**Role DTOs:**
- ✅ RoleDto (display role info)
- ✅ CreateOrUpdateRoleRequest với FluentValidation
- ✅ UpdateRolePermissionsRequest (table-based approach)
- ✅ FunctionDto và ActionDto (for permission UI)

**Role Service:**
- ✅ IRoleService interface
- ✅ RoleService implementation với table-based permission management

**User Service - Role Operations:**
- ✅ UserService.Role.cs (partial class)
- ✅ GetRolesAsync, AssignRolesAsync
- ✅ UserRolesRequest, UserRoleDto

**Controllers:**
- ✅ RoleController với RESTful endpoints

### 📊 Permission Management Flow:

```
┌─────────────┐
│   Admin     │
└──────┬──────┘
       │ GET /api/role/{id}/permissions
       ▼
┌──────────────┐
│ RoleService  │
└──────┬───────┘
       │ Query Permission table
       │ Build FunctionDto với Selected flags
  ▼
┌─────────────┐
│  Frontend   │
│  Checkboxes │
└──────┬──────┘
       │ PUT /api/role/{id}/permissions
       ▼
┌──────────────┐
│ RoleService  │
└──────┬───────┘
    │ DELETE old permissions
       │ INSERT new permissions
       ▼
┌─────────────┐
│   Success   │
└─────────────┘
```

### 📁 File Structure:

```
src/
├── Core/
│   └── Application/
│       └── Identity/
│           ├── Roles/
│           │   ├── IRoleService.cs
│         │   ├── RoleDto.cs
│   │   ├── CreateOrUpdateRoleRequest.cs
│           │   ├── UpdateRolePermissionsRequest.cs
│       │   ├── FunctionDto.cs
│        │   └── ActionDto.cs
│    └── Users/
│    ├── UserRolesRequest.cs
│     └── UserRoleDto.cs
├── Infrastructure/
│   └── Infrastructure/
│       └── Identity/
│    ├── RoleService.cs
│           └── UserService.Role.cs
└── Host/
    └── Host/
        └── Controllers/
            └── Identity/
                └── RoleController.cs
```

---

## 8. Next Steps

**Tiếp theo:** [BUILD_16C - Function Service](BUILD_16C_Function_Service.md)

Trong bước tiếp theo:
1. ✅ Function CRUD operations
2. ✅ Manage ActionInFunction relationships
3. ✅ Complete Permission system

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
