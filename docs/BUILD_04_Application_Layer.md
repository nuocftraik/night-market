# Xây dựng Application Layer

> 📖 [Quay lại Mục lục](BUILD_INDEX.md)

Tài liệu này hướng dẫn xây dựng Application Layer - chứa application services, DTOs, handlers.

---

## Bước 4.1: Setup Application Project

**Làm gì:** Tạo project chứa application services, DTOs, handlers.

**File:** `src/Core/Application/Application.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<RootNamespace>ECO.WebApi.Application</RootNamespace>
		<AssemblyName>ECO.WebApi.Application</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="..\Domain\Domain.csproj" />
		<ProjectReference Include="..\Shared\Shared.csproj" />
	</ItemGroup>
	<ItemGroup>
		<PackageReference Include="Ardalis.Specification" Version="8.0.0" />
		<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.2" />
		<PackageReference Include="Mapster" Version="7.4.0" />
		<PackageReference Include="MediatR" Version="12.4.0" />
		<PackageReference Include="Microsoft.Extensions.Caching.Abstractions" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Localization" Version="8.0.0" />
	</ItemGroup>
</Project>
```

**Key Packages:**
- **MediatR** - CQRS pattern
- **FluentValidation** - Request validation
- **Mapster** - Object mapping (không phải AutoMapper)
- **Ardalis.Specification** - Specification pattern

---

## Bước 4.2: Tạo Application Startup

**Làm gì:** Đăng ký MediatR và FluentValidation.

**File:** `src/Core/Application/Startup.cs`

```csharp
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Application;
public static class Startup
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        return services
            .AddValidatorsFromAssembly(assembly)
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
    }
}
```

**Tại sao:**
- Tự động scan và đăng ký tất cả validators
- Tự động scan và đăng ký tất cả MediatR handlers
- Không cần đăng ký từng cái một

**Cách hoạt động:**
1. `AddValidatorsFromAssembly()` - Tìm tất cả class kế thừa `AbstractValidator<T>`
2. `AddMediatR()` - Tìm tất cả class implement `IRequestHandler<TRequest, TResponse>`

---

**Tiếp theo:** [Xây dựng Infrastructure Layer](BUILD_05_Infrastructure_Layer.md)
