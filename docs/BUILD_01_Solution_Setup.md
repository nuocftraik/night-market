# BUILD_01 - Tạo Solution và Cấu hình Build

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Yêu cầu:** .NET 8 SDK ([Tải về](https://dotnet.microsoft.com/download/dotnet/8.0))  
> ⏱️ **Thời gian:** Khoảng 15-20 phút

---

## 📋 Mục tiêu

Tạo solution với **Clean Architecture** (5 layers + 1 project migrations):

```
Shared → Domain → Application → Infrastructure → Host
(Layer trong không phụ thuộc layer ngoài)
```

**Kết quả:** Solution với 6 projects, cấu hình build, code quality tools hoạt động.

---

## 1. Kiểm tra Prerequisites

```powershell
# Kiểm tra .NET 8 đã cài chưa
dotnet --version
# Kết quả mong đợi: 8.0.x
```

**Nếu chưa có:** Tải tại https://dotnet.microsoft.com/download/dotnet/8.0

---

## 2. Tạo Solution

```powershell
# Di chuyển đến thư mục gốc
cd D:\MyCode\MyProject

# Tạo solution
dotnet new sln -n MyProject.WebApi
```

**✅ Checkpoint:**
```powershell
ls *.sln
# Phải thấy: MyProject.WebApi.sln
```

---

## 3. Tạo Projects (theo thứ tự dependency)

### ⚠️ Quan trọng: Phải tạo đúng thứ tự!

**Tại sao?** Layer trong phải tồn tại trước khi layer ngoài reference.

---

### 3.1: Shared (Layer 1)

```powershell
dotnet new classlib -n Shared -o src\Core\Shared
dotnet sln add src\Core\Shared\Shared.csproj
```

**Dependency:** Không phụ thuộc gì ✅

---

### 3.2: Domain (Layer 2)

```powershell
dotnet new classlib -n Domain -o src\Core\Domain
dotnet sln add src\Core\Domain\Domain.csproj
dotnet add src\Core\Domain\Domain.csproj reference src\Core\Shared\Shared.csproj
```

**Dependency:** Shared ✅

---

### 3.3: Application (Layer 3)

```powershell
dotnet new classlib -n Application -o src\Core\Application
dotnet sln add src\Core\Application\Application.csproj
dotnet add src\Core\Application\Application.csproj reference src\Core\Domain\Domain.csproj
dotnet add src\Core\Application\Application.csproj reference src\Core\Shared\Shared.csproj
```

**Dependency:** Domain + Shared ✅

---

### 3.4: Infrastructure (Layer 4)

```powershell
dotnet new classlib -n Infrastructure -o src\Infrastructure\Infrastructure
dotnet sln add src\Infrastructure\Infrastructure\Infrastructure.csproj
dotnet add src\Infrastructure\Infrastructure\Infrastructure.csproj reference src\Core\Application\Application.csproj
dotnet add src\Infrastructure\Infrastructure\Infrastructure.csproj reference src\Core\Domain\Domain.csproj
```

**Dependency:** Application + Domain ✅

---

### 3.5: Host (Layer 5)

```powershell
dotnet new webapi -n Host -o src\Host\Host
dotnet sln add src\Host\Host\Host.csproj
dotnet add src\Host\Host\Host.csproj reference src\Infrastructure\Infrastructure\Infrastructure.csproj
dotnet add src\Host\Host\Host.csproj reference src\Core\Application\Application.csproj
```

**Dependency:** Infrastructure + Application ✅

---

### 3.6: Migrators

```powershell
dotnet new classlib -n Migrators.MSSQL -o src\Migrators\Migrators.MSSQL
dotnet sln add src\Migrators\Migrators.MSSQL\Migrators.MSSQL.csproj
dotnet add src\Migrators\Migrators.MSSQL\Migrators.MSSQL.csproj reference src\Infrastructure\Infrastructure\Infrastructure.csproj
dotnet add src\Migrators\Migrators.MSSQL\Migrators.MSSQL.csproj reference src\Core\Domain\Domain.csproj
```

**Dependency:** Infrastructure + Domain ✅

---

**✅ Checkpoint:**
```powershell
dotnet build MyProject.WebApi.sln
# Kết quả: Build succeeded
```

---

## 4. Cấu hình Build

### 4.1: Directory.Build.props

**File:** `Directory.Build.props` (root - cùng cấp .sln)

```xml
<Project>
	<PropertyGroup>
		<AnalysisLevel>latest</AnalysisLevel>
		<AnalysisMode>All</AnalysisMode>
		<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
		<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
	</PropertyGroup>
	
	<ItemGroup>
		<PackageReference
			Include="StyleCop.Analyzers"
			Version="1.1.118"
			PrivateAssets="all"
			Condition="$(MSBuildProjectExtension) == '.csproj'"
		/>
		<PackageReference
			Include="SonarAnalyzer.CSharp"
			Version="9.32.0.97167"
			PrivateAssets="all"
			Condition="$(MSBuildProjectExtension) == '.csproj'"
		/>
	</ItemGroup>
	
	<ItemGroup>
		<AdditionalFiles Include="$(MSBuildThisFileDirectory)stylecop.json" Link="stylecop.json" />
	</ItemGroup>
</Project>
```

**Giải thích:**
- **StyleCop:** Kiểm tra code style
- **SonarAnalyzer:** Phát hiện bugs, security issues
- Áp dụng cho tất cả .csproj

---

### 4.2: Directory.Build.targets

**File:** `Directory.Build.targets` (root)

```xml
<Project>
	<PropertyGroup>
		<DocumentationFile>$(OutputPath)$(AssemblyName).xml</DocumentationFile>
	</PropertyGroup>
</Project>
```

**Tác dụng:** Auto-generate XML documentation cho IntelliSense.

---

### 4.3: stylecop.json

**File:** `stylecop.json` (root)

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "orderingRules": {
      "systemUsingDirectivesFirst": true,
      "usingDirectivesPlacement": "outsideNamespace"
    },
    "layoutRules": {
    "newlineAtEndOfFile": "omit"
    }
  }
}
```

**Giải thích:**
- System usings trước
- Usings ngoài namespace (C# 10+)

---

### 4.4: .editorconfig

**File:** `.editorconfig` (root)

```ini
root = true

[*]
charset = utf-8
indent_style = space
insert_final_newline = false
trim_trailing_whitespace = true

[*.cs]
indent_size = 4

# PascalCase cho classes
dotnet_naming_rule.types_should_be_pascal_case.severity = warning
dotnet_naming_rule.types_should_be_pascal_case.symbols = types
dotnet_naming_rule.types_should_be_pascal_case.style = pascal_case

dotnet_naming_symbols.types.applicable_kinds = class, struct, interface, enum
dotnet_naming_style.pascal_case.capitalization = pascal_case

# _camelCase cho private fields
dotnet_naming_rule.private_fields_should_be_camel_case_with_underscore.severity = warning
dotnet_naming_rule.private_fields_should_be_camel_case_with_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case_with_underscore.style = camel_case_with_underscore

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_with_underscore.required_prefix = _
dotnet_naming_style.camel_case_with_underscore.capitalization = camel_case

# var preferences
csharp_style_var_for_built_in_types = false:warning
csharp_style_var_when_type_is_apparent = true:suggestion

# File-scoped namespaces
csharp_style_namespace_declarations = file_scoped:warning

# Always use braces
csharp_prefer_braces = true:warning

[*.{xml,csproj,props,targets}]
indent_size = 2

[*.json]
indent_size = 2
```

**Giải thích:**
- **Naming:** PascalCase classes, _camelCase private fields
- **var:** Chỉ dùng khi type rõ ràng
- **Namespaces:** File-scoped (modern C#)
- **Braces:** Luôn dùng {} (safety)

---

**✅ Checkpoint:**
```powershell
dotnet clean
dotnet build MyProject.WebApi.sln -v detailed | Select-String "analyzer"

# Kết quả mong đợi:
# Using analyzer: StyleCop.Analyzers
# Using analyzer: SonarAnalyzer.CSharp
```

---

## 5. Cấu trúc thư mục

```
D:\MyCode\MyProject\
├── MyProject.WebApi.sln       ⭐ Solution
├── Directory.Build.props ⭐ Build config
├── Directory.Build.targets       ⭐ XML docs
├── stylecop.json      ⭐ StyleCop rules
├── .editorconfig   ⭐ Editor format
│
└── src\
    ├── Core\
    │   ├── Shared\
    │   │   ├── Shared.csproj   ⭐ Layer 1
    │   │   └── Class1.cs      (có thể xóa)
  │   ├── Domain\
    │   │   ├── Domain.csproj     ⭐ Layer 2
    │   │   └── Class1.cs
    │   └── Application\
    │  ├── Application.csproj ⭐ Layer 3
    │       └── Class1.cs
    ├── Infrastructure\
    │   └── Infrastructure\
    │     ├── Infrastructure.csproj ⭐ Layer 4
    │  └── Class1.cs
    ├── Host\
    │   └── Host\
    │       ├── Host.csproj     ⭐ Layer 5
    │       ├── Program.cs
    │       └── Controllers\
    └── Migrators\
        └── Migrators.MSSQL\
        ├── Migrators.MSSQL.csproj ⭐ DB tool
    └── Class1.cs
```

**Cleanup (optional):**
```powershell
# Xóa Class1.cs template files
Remove-Item src\Core\Shared\Class1.cs -ErrorAction SilentlyContinue
Remove-Item src\Core\Domain\Class1.cs -ErrorAction SilentlyContinue
Remove-Item src\Core\Application\Class1.cs -ErrorAction SilentlyContinue
Remove-Item src\Infrastructure\Infrastructure\Class1.cs -ErrorAction SilentlyContinue
Remove-Item src\Migrators\Migrators.MSSQL\Class1.cs -ErrorAction SilentlyContinue
```

---

## 6. Tổng kết

### ✅ Đã hoàn thành:

**Solution:**
- ✅ 6 projects theo Clean Architecture
- ✅ Dependencies đúng thứ tự
- ✅ Build thành công

**Build Config:**
- ✅ StyleCop + SonarAnalyzer
- ✅ XML documentation
- ✅ Code style rules
- ✅ Editor formatting

---

## 7. Bước tiếp theo

**Tiếp tục:** [BUILD_02 - Shared Layer](BUILD_02_Shared_Layer.md)

Tạo authorization constants (Actions, Functions, Roles, Permissions).

---

## 💡 Quick Setup Script

Tạo `setup-solution.ps1`:

```powershell
$root = "D:\MyCode\MyProject"
cd $root

dotnet new sln -n MyProject.WebApi

dotnet new classlib -n Shared -o src\Core\Shared
dotnet new classlib -n Domain -o src\Core\Domain
dotnet new classlib -n Application -o src\Core\Application
dotnet new classlib -n Infrastructure -o src\Infrastructure\Infrastructure
dotnet new webapi -n Host -o src\Host\Host
dotnet new classlib -n Migrators.MSSQL -o src\Migrators\Migrators.MSSQL

Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName }

dotnet add src\Core\Domain\Domain.csproj reference src\Core\Shared\Shared.csproj
dotnet add src\Core\Application\Application.csproj reference src\Core\Domain\Domain.csproj src\Core\Shared\Shared.csproj
dotnet add src\Infrastructure\Infrastructure\Infrastructure.csproj reference src\Core\Application\Application.csproj src\Core\Domain\Domain.csproj
dotnet add src\Host\Host\Host.csproj reference src\Infrastructure\Infrastructure\Infrastructure.csproj src\Core\Application\Application.csproj
dotnet add src\Migrators\Migrators.MSSQL\Migrators.MSSQL.csproj reference src\Infrastructure\Infrastructure\Infrastructure.csproj src\Core\Domain\Domain.csproj

dotnet build

Write-Host "✅ Setup hoàn thành!"
```

**Chạy:**
```powershell
.\setup-solution.ps1
```

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
