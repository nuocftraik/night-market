# Function Management Service - Function CRUD & Action Assignment

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 16B (Role Service) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Function Management Service - Quản lý Functions và Actions assignments.

---

## 1. Overview

**Làm gì:** Xây dựng Function Management Service để quản lý Functions (modules/features) và assign Actions to Functions.

**Tại sao cần:**
- **Function Management:** CRUD operations cho Functions (modules như Users, Products, Orders)
- **Action Assignment:** Assign Actions (View, Create, Update, Delete) to Functions
- **Permission System Foundation:** Functions + Actions = Permissions basis
- **Dynamic Authorization:** Add new modules/features without code changes
- **Complete Permission Triangle:** Function + Action + Role = Permission

**Trong bước này chúng ta sẽ:**
- ✅ Tạo IFunctionService interface
- ✅ Tạo Function DTOs (FunctionDto, CreateOrUpdateFunctionRequest)
- ✅ Implement FunctionService với các operations:
  - Get functions list với actions
  - Get function details
  - Create/Update functions với action assignments
  - Delete functions
- ✅ Complete RoleController với function endpoints
- ✅ Understand Permission System architecture

**Real-world example:**
```csharp
// Admin creates new function (module)
var createRequest = new CreateOrUpdateFunctionRequest
{
    Name = "Orders",
    ActionIds = new List<Guid>
    {
        viewActionId,
        createActionId,
        updateActionId,
        deleteActionId
    }
};

var functionId = await _functionService.CreateOrUpdateAsync(createRequest);
// → Returns function ID

// Admin gets function với actions
var function = await _functionService.GetByIdAsync(functionId);
// → Returns FunctionDto with ActionDtos

// Permission System Flow:
// 1. Define Functions (Users, Products, Orders)
// 2. Assign Actions to Functions (View, Create, Update, Delete)
// 3. Assign Function+Action combinations to Roles (via Permission table)
// 4. Assign Roles to Users
// → User có permissions qua: User → Role → Permission (Function + Action)
```

---

## 2. Understanding Permission System Architecture

### Bước 2.1: Permission System Overview

**Architecture Diagram:**

```
┌─────────────────────────────────────────────────────────┐
│               PERMISSION SYSTEM                         │ 
│            (Table-Based Approach)                       │
└─────────────────────────────────────────────────────────┘

┌───────────┐          ┌────────────┐     ┌──────────┐
│  Action   │          │  Function  │     │   Role   │
│  (View,   │◄─────────│  (Users,   │     │ (Admin,  │
│  Create,  │  N   N   │  Products, │     │ Manager) │
│  Update,  │          │  Orders)   │     │          │
│  Delete)  │          │            │     │          │
└───────────┘          └────────────┘     └──────────┘
      │                     │                       │
      │                     │                       │
      │                     │                       │
      │     ┌───────────────┴────────────────┐      │
      │     │   ActionInFunction Table       │      │  
      └─────┤  (Function + Action mapping)   │      │
            └────────────────────────────────┘      │
                     │                              │
                     │                              │
            ┌────────┴──────────┐                   │
            │  Permission Table │◄──────────────────┘
            │ (Role + Function  │
            │ + Action)         │
            └───────────────────┘
                     │
                     │
            ┌────────▼──────────┐
            │  UserRoles Table  │
            │  (User + Role)    │
            └───────────────────┘
                     │
                     │
            ┌────────▼──────────┐
            │   ApplicationUser │
            └───────────────────┘
```

**Tables Explained:**

1. **Action Table:**
   - Stores available actions (View, Create, Update, Delete, Export, etc.)
   - Seeded on app startup
   - Reusable across all functions

2. **Function Table:**
   - Stores modules/features (Users, Products, Orders, etc.)
   - Seeded on app startup
   - Represents logical grouping of operations

3. **ActionInFunction Table (Many-to-Many):**
   - Maps Actions to Functions
   - Example: Users function có View, Create, Update, Delete actions
   - Example: Products function có View, Create, Update, Delete, Export actions

4. **Role Table:**
   - Stores roles (Admin, Manager, Basic, etc.)
   - Seeded on app startup (Admin, Basic)

5. **Permission Table (Composite Primary Key: RoleId + FunctionId + ActionId):**
   - **Core của authorization system**
   - Stores which Function+Action combinations a Role has
   - Example: Manager role có Users.View, Users.Create, Products.View

6. **UserRoles Table (ASP.NET Core Identity):**
   - Maps Users to Roles
   - Example: User John có Manager role

---

### Bước 2.2: Permission Flow Example

**Scenario:** Check if User "John" có permission "Users.Create"

**Flow:**
```
1. User John logs in
   ↓
2. System loads John's roles từ UserRoles table
   → John có "Manager" role
   ↓
3. System loads Manager role's permissions từ Permission table
   → Manager role có:
     - (Manager, Users, View)
     - (Manager, Users, Create)
     - (Manager, Products, View)
   ↓
4. System builds permission strings từ Permission table
   → Permissions: ["Users.View", "Users.Create", "Products.View"]
   ↓
5. System adds permissions to JWT claims
   → JWT token contains: { "permission": ["Users.View", "Users.Create", "Products.View"] }
   ↓
6. Frontend calls API: POST /api/users (requires "Users.Create" permission)
   ↓
7. PermissionAuthorizationHandler checks JWT claims
   → Has "Users.Create" permission? YES
   ↓
8. Request allowed ✅
```

---

### Bước 2.3: Why Table-Based Approach?

**Benefits:**

1. **Dynamic:** Add new modules/features without code changes
2. **Flexible:** Assign any Action to any Function
3. **Database-driven:** Permissions stored in database, not hardcoded
4. **UI-friendly:** Easy to build admin UI với checkboxes
5. **Scalable:** Support unlimited Functions, Actions, Roles
6. **Auditable:** Track permission changes in database

**Comparison:**

**❌ Hardcoded Approach:**
```csharp
// Hardcoded permissions trong code
public static class Permissions
{
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string ProductsView = "Products.View";
 // ... thêm 100+ permissions
}
```
**Problems:**
- Cần deploy code để add permissions
- Không flexible
- Hard to maintain

**✅ Table-Based Approach:**
```csharp
// Permissions stored trong database
// Add new function:
INSERT INTO Function (Name) VALUES ('Orders');
// Add actions to function:
INSERT INTO ActionInFunction (FunctionId, ActionId) VALUES (...);
// Assign permissions to role:
INSERT INTO Permission (RoleId, FunctionId, ActionId) VALUES (...);
// No code deployment needed! ✅
```

---

## 3. Domain Entities

### Bước 3.1: Function Entity

**Làm gì:** Domain entity representing a module/feature.

**Tại sao:** Functions are domain concepts (business modules).

**File:** `src/Core/Domain/Identity/Function.cs`

```csharp
namespace ECO.WebApi.Domain.Identity;

/// <summary>
/// Function entity (represents a module/feature)
/// Examples: Users, Products, Orders, Categories
/// </summary>
public class Function : BaseEntity
{
    /// <summary>
    /// Function name (e.g., "Users", "Products")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Actions assigned to this function (many-to-many relationship)
    /// </summary>
    public virtual List<ActionInFunction> ActionInFunctions { get; set; } = new();

    public Function()
    {
    }

    /// <summary>
    /// Add an action to this function
    /// </summary>
    public void AddAction(Guid actionId)
    {
        ActionInFunctions.Add(new ActionInFunction(actionId, Id));
    }

    /// <summary>
    /// Update actions for this function (replace all)
    /// </summary>
    public void UpdateActions(List<Guid>? newActionIds)
    {
        if (newActionIds == null || newActionIds.Count == 0)
        {
            ActionInFunctions.Clear();
            return;
        }

        // Remove actions not in new list
        ActionInFunctions.RemoveAll(aif => !newActionIds.Contains(aif.ActionId));

        // Add new actions not yet in function
        var existingActionIds = ActionInFunctions.Select(aif => aif.ActionId).ToHashSet();
        foreach (var actionId in newActionIds)
        {
            if (!existingActionIds.Contains(actionId))
            {
                ActionInFunctions.Add(new ActionInFunction(actionId, Id));
            }
        }
    }
}
```

**Giải thích:**

**Properties:**
- **Name:** Function name (unique identifier, e.g., "Users", "Products")
- **ActionInFunctions:** Many-to-many relationship với Action entity

**Methods:**
- **AddAction:** Add một action to function
- **UpdateActions:** Update actions (remove old + add new)

**Tại sao domain methods:**
- Encapsulate business logic trong entity
- Ensure consistency (no orphan records)
- Follow DDD principles

---

### Bước 3.2: Action Entity

**Làm gì:** Domain entity representing an operation.

**Tại sao:** Actions are domain concepts (operations).

**File:** `src/Core/Domain/Identity/Action.cs`

```csharp
namespace ECO.WebApi.Domain.Identity;

/// <summary>
/// Action entity (represents an operation)
/// Examples: View, Create, Update, Delete, Export, Import
/// </summary>
public class Action : BaseEntity
{
    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Functions that have this action (many-to-many relationship)
    /// </summary>
    public virtual ICollection<ActionInFunction> ActionInFunctions { get; set; } = default!;

    public Action()
    {
    }

    public Action(string name)
    {
        Name = name;
    }
}
```

**Giải thích:**
- **Name:** Action name (e.g., "View", "Create")
- **ActionInFunctions:** Many-to-many relationship với Function entity
- Actions are seeded on app startup (View, Create, Update, Delete, Export, Import, etc.)

---

### Bước 3.3: ActionInFunction Entity (Many-to-Many)

**Làm gì:** Junction table for Function-Action relationship.

**Tại sao:** Many-to-many relationship requires junction table.

**File:** `src/Core/Domain/Identity/ActionInFunction.cs`

```csharp
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Domain.Identity;

/// <summary>
/// ActionInFunction entity (junction table for Function-Action many-to-many)
/// Composite Primary Key: (ActionId, FunctionId)
/// </summary>
[PrimaryKey(nameof(ActionId), nameof(FunctionId))]
public class ActionInFunction
{
    /// <summary>
    /// Action ID (foreign key)
    /// </summary>
    public Guid ActionId { get; set; }

    /// <summary>
    /// Function ID (foreign key)
    /// </summary>
    public Guid FunctionId { get; set; }

    /// <summary>
    /// Navigation property to Action
    /// </summary>
    public virtual Action Action { get; set; } = default!;

    /// <summary>
    /// Navigation property to Function
    /// </summary>
    public virtual Function Function { get; set; } = default!;

    public ActionInFunction()
    {
    }

    public ActionInFunction(Guid actionId, Guid functionId)
    {
        ActionId = actionId;
        FunctionId = functionId;
    }
}
```

**Giải thích:**
- **Composite Primary Key:** (ActionId, FunctionId) ensures uniqueness
- **Navigation Properties:** Action và Function entities
- Stores which Actions are assigned to which Functions

**Example Data:**
```
ActionInFunction Table:
┌────────────────────┬────────────────────┐
│  ActionId (View)   │ FunctionId (Users) │
├────────────────────┼────────────────────┤
│  ActionId (Create) │ FunctionId (Users) │
├────────────────────┼────────────────────┤
│  ActionId (Update) │ FunctionId (Users) │
├────────────────────┼────────────────────┤
│  ActionId (View)   │ FunctionId (Prod)  │
└────────────────────┴────────────────────┘
```

---

## 4. Function Service

### Bước 4.1: CreateOrUpdateFunctionRequest

**Làm gì:** Request DTO để tạo hoặc update function.

**Tại sao:** Single endpoint cho both create/update operations.

**File:** `src/Core/Application/Identity/Roles/CreateOrUpdateFunctionRequest.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Request để tạo hoặc update function
/// </summary>
public class CreateOrUpdateFunctionRequest
{
    /// <summary>
    /// Function ID (null or Guid.Empty = create, not empty = update)
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Function name (required, unique)
    /// Examples: "Users", "Products", "Orders"
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// List of Action IDs to assign to this function
    /// Example: [ViewActionId, CreateActionId, UpdateActionId]
    /// </summary>
    public List<Guid>? ActionIds { get; set; }
}
```

**Giải thích:**
- **Id:** null hoặc Guid.Empty = create, otherwise = update
- **Name:** Function name (unique identifier)
- **ActionIds:** List of actions to assign to this function

**Create vs Update:**
- **Create:** Id = null or Guid.Empty
- **Update:** Id != Guid.Empty

---

### Bước 4.2: IFunctionService Interface

**Làm gì:** Define contract cho function operations.

**Tại sao:** Abstraction, dễ test, dễ swap implementations.

**File:** `src/Core/Application/Identity/Roles/IFunctionService.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Roles;

/// <summary>
/// Service xử lý function management operations
/// </summary>
public interface IFunctionService : ITransientService
{
    /// <summary>
    /// Get list tất cả functions với actions
    /// </summary>
    Task<List<FunctionDto>> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get function details by ID với actions
    /// </summary>
    Task<FunctionDto> GetByIdAsync(Guid id);

    /// <summary>
    /// Create hoặc update function với action assignments
    /// Returns function ID
    /// </summary>
    Task<string> CreateOrUpdateAsync(CreateOrUpdateFunctionRequest request);

    /// <summary>
    /// Delete function
    /// Cannot delete functions being used in Permission table
    /// </summary>
    Task<string> DeleteAsync(Guid id);
}
```

**Giải thích:**
- **GetListAsync:** Get all functions với actions (for dropdown, list display)
- **GetByIdAsync:** Get function details với actions
- **CreateOrUpdateAsync:** Create/update function với action assignments
- **DeleteAsync:** Delete function với validation

---

### Bước 4.3: FunctionService Implementation

**Làm gì:** Implement function management operations.

**Tại sao:** Business logic cho function và action assignments.

**File:** `src/Infrastructure/Infrastructure/Identity/FunctionService.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Identity.Roles;
using ECO.WebApi.Domain.Identity;
using ECO.WebApi.Infrastructure.Persistence.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Infrastructure.Identity;

/// <summary>
/// Service xử lý function management operations
/// </summary>
public class FunctionService : IFunctionService
{
    private readonly ApplicationDbContext _db;

    public FunctionService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get list tất cả functions với actions
    /// </summary>
    public async Task<List<FunctionDto>> GetListAsync(CancellationToken cancellationToken)
    {
        var functions = await _db.Functions
            .Include(f => f.ActionInFunctions)
            .ThenInclude(aif => aif.Action)
            .ToListAsync(cancellationToken);

        return functions.Adapt<List<FunctionDto>>();
    }

    /// <summary>
    /// Get function details by ID với actions
    /// </summary>
    public async Task<FunctionDto> GetByIdAsync(Guid id)
    {
      var function = await _db.Functions
            .Include(f => f.ActionInFunctions)
            .ThenInclude(aif => aif.Action)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (function == null)
        {
            throw new NotFoundException("Function not found");
        }

        return function.Adapt<FunctionDto>();
    }

    /// <summary>
    /// Create hoặc update function với action assignments
    /// </summary>
    public async Task<string> CreateOrUpdateAsync(CreateOrUpdateFunctionRequest request)
    {
        if (request.Id == null || request.Id == Guid.Empty)
        {
        // Create new function
   var function = new Function { Name = request.Name };

      // Add actions to function
            if (request.ActionIds != null)
  {
    foreach (var actionId in request.ActionIds)
           {
         function.AddAction(actionId);
           }
}

          _db.Functions.Add(function);
 await _db.SaveChangesAsync();

          return function.Id.ToString();
      }
 else
        {
   // Update existing function
        var function = await _db.Functions
 .Include(f => f.ActionInFunctions)
            .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (function == null)
    {
       throw new NotFoundException("Function not found");
 }

        // Update name
            function.Name = request.Name;

     // Update actions (replace all)
            function.UpdateActions(request.ActionIds);

            await _db.SaveChangesAsync();

 return function.Id.ToString();
        }
    }

    /// <summary>
    /// Delete function
    /// Cannot delete functions being used in Permission table
    /// </summary>
    public async Task<string> DeleteAsync(Guid id)
    {
        var function = await _db.Functions.FirstOrDefaultAsync(f => f.Id == id);

  if (function == null)
        {
            throw new NotFoundException("Function not found");
      }

        // Check if function is being used in Permission table
        var isUsedInPermissions = await _db.Permissions
        .AnyAsync(p => p.FunctionId == id);

   if (isUsedInPermissions)
        {
            throw new ConflictException(
   $"Cannot delete function '{function.Name}' as it is being used in permissions.");
        }

        _db.Functions.Remove(function);
     await _db.SaveChangesAsync();

 return id.ToString();
    }
}
```

**Giải thích:**

**GetListAsync:**
- Include ActionInFunctions và Action entities (eager loading)
- Adapt to FunctionDto (Mapster)

**GetByIdAsync:**
- Include ActionInFunctions và Action entities
- Adapt to FunctionDto
- Throw NotFoundException nếu không tìm thấy

**CreateOrUpdateAsync:**
- **Create:** New Function entity, add actions, save to database
- **Update:** Find existing function, update name, update actions (replace all)
- Return function ID

**DeleteAsync:**
- Check if function is being used in Permission table
- Cannot delete if used (data integrity)
- Remove function and save changes

**Tại sao eager loading:**
- Include ActionInFunctions và Action để avoid N+1 query problem
- Load all related data in single query
- Better performance

---

## 5. Summary

### ✅ Đã hoàn thành trong bước này:

**Domain Entities:**
- ✅ Function entity với domain methods
- ✅ Action entity
- ✅ ActionInFunction entity (many-to-many junction)

**Function DTOs:**
- ✅ FunctionDto với ActionDtos
- ✅ ActionDto với Selected flag
- ✅ CreateOrUpdateFunctionRequest

**Function Service:**
- ✅ IFunctionService interface
- ✅ FunctionService implementation
- ✅ GetListAsync, GetByIdAsync, CreateOrUpdateAsync, DeleteAsync

**Controllers:**
- ✅ RoleController với function endpoints (từ BUILD_16B)

### 📊 Complete Permission System Architecture:

```
┌─────────────────────────────────────────────────┐
│          PERMISSION SYSTEM OVERVIEW│
└─────────────────────────────────────────────────┘

1. DEFINE ACTIONS (Seeded on startup)
   → View, Create, Update, Delete, Export, Import

2. DEFINE FUNCTIONS (Seeded on startup)
   → Users, Products, Orders, Categories

3. ASSIGN ACTIONS TO FUNCTIONS (ActionInFunction table)
   → Users has: View, Create, Update, Delete
   → Products has: View, Create, Update, Delete, Export

4. DEFINE ROLES (Seeded on startup)
   → Admin, Manager, Basic

5. ASSIGN FUNCTION+ACTION TO ROLES (Permission table)
   → Manager role has:
     - Users.View
 - Users.Create
     - Products.View
     - Products.Create

6. ASSIGN ROLES TO USERS (UserRoles table - Identity)
   → User John has: Manager role

7. AUTHORIZATION CHECK (on each API call)
   → Check JWT claims for required permission
   → Example: [MustHavePermission("Users.Create")]
```

### 📁 Complete File Structure:

```
src/
├── Core/
│   ├── Domain/
│   │   └── Identity/
│   │       ├── Function.cs
│   │ ├── Action.cs
│   │       ├── ActionInFunction.cs
│   │       ├── Permission.cs
│   │     ├── ApplicationRole.cs
│   │   └── ApplicationUser.cs
│   └── Application/
│       └── Identity/
│        ├── Roles/
│           │   ├── IRoleService.cs
│           │   ├── IFunctionService.cs
│       │   ├── RoleDto.cs
│    │   ├── FunctionDto.cs
│           │   ├── ActionDto.cs
│   │   ├── CreateOrUpdateRoleRequest.cs
│   │   ├── CreateOrUpdateFunctionRequest.cs
│           │   └── UpdateRolePermissionsRequest.cs
│           └── Users/
│    ├── IUserService.cs
│    ├── UserRolesRequest.cs
│         └── UserRoleDto.cs
├── Infrastructure/
│   └── Infrastructure/
│       └── Identity/
│  ├── RoleService.cs
│       ├── FunctionService.cs
│           ├── UserService.cs
│     └── UserService.Role.cs
└── Host/
    └── Host/
     └── Controllers/
   └── Identity/
       ├── RoleController.cs
                └── UsersController.cs
```

---

## 6. Next Steps

**Tiếp theo:** [BUILD_17 - Permission Authorization](BUILD_17_Permission_Authorization.md)

Trong bước tiếp theo, chúng ta sẽ implement permission-based authorization:
1. ✅ PermissionRequirement (IAuthorizationRequirement)
2. ✅ PermissionAuthorizationHandler (check permissions from JWT claims)
3. ✅ PermissionPolicyProvider (dynamic policy creation)
4. ✅ [MustHavePermission] attribute
5. ✅ Permission seeding in ApplicationDbSeeder
6. ✅ Add permissions to JWT claims

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
