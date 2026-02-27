# Xây dựng Domain Layer

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)

Tài liệu này hướng dẫn xây dựng Domain Layer - chứa domain entities và business logic.

---

## Bước 3.1: Setup Domain Project

**Làm gì:** Tạo project chứa domain entities.

**File:** `src/Core/Domain/Domain.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<RootNamespace>ECO.WebApi.Domain</RootNamespace>
		<AssemblyName>ECO.WebApi.Domain</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="..\Shared\Shared.csproj" />
	</ItemGroup>
	<ItemGroup>
		<PackageReference Include="NewId" Version="4.0.1" />
		<PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.1.39" />
		<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />
	</ItemGroup>
</Project>
```

**Dependencies:**
- `Shared` - Sử dụng constants và interfaces
- `NewId` - Generate unique IDs
- `Microsoft.AspNetCore.Identity` - Identity entities

---

## Bước 3.2: Tạo Identity Entities

**Làm gì:** Tạo custom Identity entities.

**File:** `src/Core/Domain/Identity/ApplicationUser.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace ECO.WebApi.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ObjectId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}
```

**File:** `src/Core/Domain/Identity/ApplicationRole.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace ECO.WebApi.Domain.Identity;

public class ApplicationRole : IdentityRole
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName, string description) : base(roleName)
    {
        Description = description;
    }
    public string? Description { get; set; }
}
```

**File:** `src/Core/Domain/Identity/ApplicationRoleClaim.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace ECO.WebApi.Domain.Identity;

public class ApplicationRoleClaim : IdentityRoleClaim<string>
{
    public string? CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
}
```

**File:** `src/Core/Domain/Identity/Action.cs`

```csharp
namespace ECO.WebApi.Domain.Identity;

public class Action
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
```

**File:** `src/Core/Domain/Identity/Function.cs`

```csharp
namespace ECO.WebApi.Domain.Identity;

public class Function
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
```

**File:** `src/Core/Domain/Identity/Permission.cs`

```csharp
namespace ECO.WebApi.Domain.Identity;

public class Permission
{
    public string RoleId { get; set; } = default!;
    public string FunctionId { get; set; } = default!;
    public string ActionId { get; set; } = default!;
    public virtual ApplicationRole Role { get; set; } = default!;
    public virtual Function Function { get; set; } = default!;
    public virtual Action Action { get; set; } = default!;
}
```

**File:** `src/Core/Domain/Identity/ActionInFunction.cs`

```csharp
namespace ECO.WebApi.Domain.Identity;

public class ActionInFunction
{
    public string ActionId { get; set; } = default!;
    public string FunctionId { get; set; } = default!;
    public virtual Action Action { get; set; } = default!;
    public virtual Function Function { get; set; } = default!;
}
```

**Tại sao:** Extend Identity để thêm custom properties và hỗ trợ role-based authorization mở rộng với Functions và Actions.

---

**Tiếp theo:** [Xây dựng Application Layer](BUILD_04_Application_Layer.md)
