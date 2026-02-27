---
description: "Phase 4: Authentication & Authorization — JWT, Identity Services, Permissions, OAuth2"
---

# Phase 4: Authentication & Authorization

**Mục tiêu**: JWT Authentication + Permission-based Authorization hoàn chỉnh
**Prerequisites**: Phase 3 hoàn thành (Core Services OK)

---

## Bước 15: JWT Authentication

```
Đọc file: docs/BUILD_15_JWT_Authentication.md
```

**Việc cần làm:**
- `JwtSettings` configuration
- `ITokenService` interface
- `TokenService` implementation (generate tokens, refresh tokens)
- JWT authentication middleware setup
- `TokenRequest`, `TokenResponse`, `RefreshTokenRequest` DTOs

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: Login endpoint trả về JWT token

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 15** trước khi sang bước tiếp theo.

---

## Bước 16A: User Service

```
Đọc file: docs/BUILD_16A_User_Service.md
```

**Việc cần làm:**
- `UserService` — Complete user management với TẤT CẢ operations
- User DTOs (`UserDetailDto`, `CreateUserRequest`, `UpdateUserRequest`)
- User CRUD operations (Search, Get, Create, Update, Toggle Status)
- Email/Phone confirmation
- Password operations (Forgot, Reset, Change)
- Permission operations (GetPermissions, HasPermission với caching)
- FluentValidation cho tất cả requests

**Verify:**
// turbo
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 16A** trước khi sang bước tiếp theo.

---

## Bước 16B: Role Service

```
Đọc file: docs/BUILD_16B_Role_Service.md
```

**Việc cần làm:**
- `RoleService` — CRUD roles, manage permissions
- Role DTOs (`RoleDto`, `CreateOrUpdateRoleRequest`, `UpdateRolePermissionsRequest`)
- Role specifications (`RoleByNameSpec`, `RoleByIdSpec`)
- Permission management (Get, Update permissions for roles)

**Verify:**
// turbo
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 16B** trước khi sang bước tiếp theo.

---

## Bước 16C: Function Service

```
Đọc file: docs/BUILD_16C_Function_Service.md
```

**Việc cần làm:**
- `FunctionService` — CRUD functions (Permission modules)
- Function DTOs (`FunctionDto`, `CreateOrUpdateFunctionRequest`)
- Function specifications (`FunctionByIdSpec`, `FunctionByNameSpec`)
- Action management (Get actions for functions)

**Verify:**
// turbo
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 16C** trước khi sang bước tiếp theo.

---

## Bước 16D: Identity Controllers

```
Đọc file: docs/BUILD_16D_Identity_Controllers.md
```

**Việc cần làm:**
- `TokensController` — Login, Refresh token endpoints
- `UsersController` — User management REST APIs
- `RoleController` — Role & Function management APIs
- `PersonalController` — Current user profile APIs
- `ClaimsPrincipalExtensions` — Helper methods (GetUserId, GetEmail)
- Swagger documentation với OpenAPI attributes

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: APIs visible trong Swagger UI

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 16D** trước khi sang bước tiếp theo.

---

## Bước 17: Permission Authorization

```
Đọc file: docs/BUILD_17_Permission_Authorization.md
```

**Việc cần làm:**
- `PermissionRequirement` (`IAuthorizationRequirement`)
- `PermissionAuthorizationHandler` (check permissions from claims)
- `PermissionPolicyProvider` (dynamic policy creation)
- `MustHavePermissionAttribute` (`[MustHavePermission("Users.View")]`)
- Permission seeding trong `ApplicationDbSeeder`

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: Endpoints yêu cầu permission mới access được

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 17** trước khi sang bước tiếp theo.

---

## Bước 18: OAuth2 Integration

```
Đọc file: docs/BUILD_18_OAuth2_Integration.md
```

**Việc cần làm:**
- Google OAuth2 setup (`GoogleAuthSettings`, configuration)
- Facebook OAuth2 setup (`FacebookAuthSettings`, configuration)
- `IAuthenticationService` interface
- `AuthenticationService` implementation
- OAuth2 middleware configuration

**Verify:**
// turbo
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 18** trước khi sang bước tiếp theo.

---

## ✅ Phase 4 Checkpoint

**Kiểm tra:**
- [ ] `dotnet build` — 0 errors
- [ ] Login `admin@root.com` / `123Pa$$word!` trả về JWT token
- [ ] CRUD users hoạt động qua Swagger
- [ ] CRUD roles hoạt động qua Swagger
- [ ] Permission authorization chặn unauthorized access
- [ ] OAuth2 compile OK
- [ ] Thực hiện chỉnh sửa docs sau khi đã hoàn thiện phase này cho phù hợp (cập nhật docs nếu trong quá trình implement có thay đổi/tối ưu so với docs gốc).

**⏸️ DỪNG: Notify user review Phase 4 trước khi tiếp tục Phase 5 (`/build-phase5`)**
