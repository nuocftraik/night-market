# BUILD_02 - Shared Layer (Authorization Constants)

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Yêu cầu:** BUILD_01 đã hoàn thành  
> ⏱️ **Thời gian:** Khoảng 10 phút

---

## 📋 Mục tiêu

Tạo **Shared Layer** với authorization constants cơ bản.

**Kết quả:** Constants cho Actions, Functions, Roles, Claims, Permissions - foundation cho authorization system.

**Tại sao cần?**
- ✅ **Type-safe:** Tránh magic strings `"Users.View"` → `AppPermission.NameFor(AppAction.View, AppFunction.Users)`
- ✅ **Centralized:** Thêm/sửa permission ở 1 chỗ
- ✅ **Maintainable:** Dễ refactor, IDE support

> 💡 **Lưu ý:** Shared layer sẽ chứa thêm Events, Notifications sau (BUILD_09, BUILD_29). Bây giờ chỉ focus Authorization.

---

## 1. Xóa file template

```powershell
# Xóa Class1.cs (file template không dùng)
Remove-Item src\Core\Shared\Class1.cs -ErrorAction SilentlyContinue
```

---

## 2. Tạo Authorization Constants

### Bước 2.1: Tạo folder

```powershell
New-Item -ItemType Directory -Path "src\Core\Shared\Authorization" -Force
```

---

### Bước 2.2: AppAction - CRUD Actions

**Làm gì:** Định nghĩa actions cơ bản (View, Create, Update, Delete...).

**File:** `src\Core\Shared\Authorization\AppAction.cs`

```csharp
namespace MyProject.Shared.Authorization;

/// <summary>
/// Các actions cơ bản trong hệ thống
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
- `const string`: Compile-time constant
- `nameof()`: Type-safe, refactor-friendly
- **View/Search:** Read operations
- **Create/Update/Delete:** Write operations
- **Import/Export:** Batch operations
- **Clean:** Cleanup/Archive operations

---

### Bước 2.3: AppFunction - Modules

**Làm gì:** Định nghĩa modules/features.

**File:** `src\Core\Shared\Authorization\AppFunction.cs`

```csharp
namespace MyProject.Shared.Authorization;

/// <summary>
/// Các modules/features trong hệ thống
/// </summary>
public static class AppFunction
{
  public const string Dashboard = nameof(Dashboard);
    public const string Hangfire = nameof(Hangfire);
    public const string Users = nameof(Users);
    public const string Roles = nameof(Roles);
    public const string Products = nameof(Products);
    public const string Categories = nameof(Categories);
}
```

**Giải thích:**
- Mỗi function = 1 module quản lý
- Dùng để generate permissions: `"Permissions.Users.View"`, `"Permissions.Products.Create"`

---

### Bước 2.4: AppRoles - Default Roles

**Làm gì:** Định nghĩa roles mặc định.

**File:** `src\Core\Shared\Authorization\AppRoles.cs`

```csharp
using System.Collections.ObjectModel;

namespace MyProject.Shared.Authorization;

/// <summary>
/// Default roles trong hệ thống
/// </summary>
public static class AppRoles
{
    public const string Admin = nameof(Admin);
    public const string Basic = nameof(Basic);

    public static IReadOnlyList<string> DefaultRoles { get; } = new ReadOnlyCollection<string>(new[]
    {
  Admin,
        Basic
    });

    public static bool IsDefault(string roleName) => 
        DefaultRoles.Any(r => r == roleName);
}
```

**Giải thích:**
- **Admin:** Full permissions
- **Basic:** Limited permissions
- **DefaultRoles:** Dùng để seed database
- **IsDefault():** Protect default roles khỏi bị xóa

---

### Bước 2.5: AppClaims - JWT Claims

**Làm gì:** Định nghĩa JWT claim keys.

**File:** `src\Core\Shared\Authorization\AppClaims.cs`

```csharp
namespace MyProject.Shared.Authorization;

/// <summary>
/// JWT claim keys
/// </summary>
public static class AppClaims
{
    public const string Fullname = "fullName";
    public const string Permission = "permission";
    public const string ImageUrl = "image_url";
    public const string IpAddress = "ipAddress";
    public const string Expiration = "exp";
}
```

**Giải thích:**
- Dùng trong JWT token generation/validation
- **Permission:** Multiple values (array of permission strings)

---

### Bước 2.6: AppPermission - Dynamic Generation

**Làm gì:** Generate permissions từ Actions + Functions.

**File:** `src\Core\Shared\Authorization\AppPermission.cs`

```csharp
using System.Reflection;

namespace MyProject.Shared.Authorization;

/// <summary>
/// Permission record với dynamic generation
/// Format: "Permissions.{Function}.{Action}"
/// </summary>
public record AppPermission(string Action, string Function)
{
    public string Name => NameFor(Action, Function);

    public static string NameFor(string action, string function) => 
    $"Permissions.{function}.{action}";

    /// <summary>
    /// Generate tất cả permissions cho một function
    /// </summary>
    public static List<string> GeneratePermissionsForFunction(string function)
    {
        var actions = typeof(AppAction)
       .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
.Where(field => field.IsLiteral && !field.IsInitOnly)
            .Select(field => field.GetValue(null)?.ToString())
      .Where(value => value != null)
     .Cast<string>()
       .ToList();

  return actions.Select(action => NameFor(action, function)).ToList();
    }

    /// <summary>
    /// Generate permissions với custom actions
    /// </summary>
    public static List<string> GeneratePermissionsForFunction(string function, List<string> actions)
    {
        if (actions == null || actions.Count == 0)
   throw new ArgumentException("Actions không được null hoặc empty", nameof(actions));

        return actions.Select(action => NameFor(action, function)).ToList();
    }
}
```

**Giải thích:**
- **Record:** Immutable data class
- **NameFor():** Format permission string
- **GeneratePermissionsForFunction():** Dùng reflection để lấy tất cả actions từ `AppAction`
- **Overload:** Custom actions cho special cases (ví dụ: Dashboard chỉ cần View)

**Usage example:**
```csharp
// Generate all permissions cho Users
var userPermissions = AppPermission.GeneratePermissionsForFunction(AppFunction.Users);
// Result: ["Permissions.Users.View", "Permissions.Users.Create", ...]

// Custom permissions cho Dashboard
var dashboardPermissions = AppPermission.GeneratePermissionsForFunction(
    AppFunction.Dashboard, 
    new List<string> { AppAction.View }
);
// Result: ["Permissions.Dashboard.View"]
```

---

## 3. Verify

```powershell
# Build project
dotnet build src\Core\Shared\Shared.csproj

# Kết quả mong đợi: Build succeeded
```

---

## 4. Cấu trúc thư mục sau BUILD_02

```
src\Core\Shared\
├── Shared.csproj
├── Authorization\         📁 NEW
│   ├── AppAction.cs    ⭐ CRUD actions
│   ├── AppFunction.cs     ⭐ Modules
│   ├── AppRoles.cs        ⭐ Default roles
│   ├── AppClaims.cs       ⭐ JWT claims
│   └── AppPermission.cs   ⭐ Dynamic generation
└── obj\
```

---

## 5. Tổng kết

### ✅ Đã hoàn thành:

**Authorization Foundation:**
- ✅ `AppAction` - 8 actions
- ✅ `AppFunction` - 6 modules
- ✅ `AppRoles` - 2 default roles
- ✅ `AppClaims` - 5 JWT claims
- ✅ `AppPermission` - Dynamic generation

**Chưa làm (sẽ làm sau):**
- ⏸️ Events (BUILD_09 - Domain Events)
- ⏸️ Notifications (BUILD_29 - SignalR Notifications)

---

## 6. Bước tiếp theo

**Tiếp tục:** [BUILD_03 - Domain Layer](BUILD_03_Domain_Layer.md)

Tạo Domain entities với ASP.NET Identity.

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
