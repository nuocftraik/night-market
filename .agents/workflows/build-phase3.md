---
description: "Phase 3: Core Services — CurrentUser, Serializer, Exceptions, Validation"
---

# Phase 3: Core Services

**Mục tiêu**: Core services foundation hoạt động
**Prerequisites**: Phase 2 hoàn thành (DB + Repository OK)

---

## Bước 12: Common Services

```
Đọc file: docs/BUILD_12_Common_Services.md
```

**Việc cần làm:**

**12.1 CurrentUser:**
- `ICurrentUser` interface
- `ICurrentUserInitializer` interface
- `CurrentUser` implementation
- `CurrentUserMiddleware`

**12.2 Serializer:**
- `ISerializerService` interface
- `NewtonSoftService` implementation

**12.3 Event Publisher:**
- `IEventPublisher` interface
- `EventPublisher` implementation (MediatR integration)

**Verify:**
// turbo
```bash
dotnet build
```

---

## Bước 13: Exception Handling & Middleware

```
Đọc file: docs/BUILD_13_Exceptions_Middleware.md
```

**Việc cần làm:**

**13.1 Exception Hierarchy:**
- `CustomException` (base)
- `NotFoundException`
- `UnauthorizedException`
- `ForbiddenException`
- `ConflictException`
- `InternalServerException`

**13.2 Error Response:**
- `ErrorResult` model

**13.3 Middleware:**
- `ExceptionMiddleware` (global exception handler)
- Register middleware pipeline

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: API trả về proper HTTP status codes cho exceptions

---

## Bước 14: Validation Behavior

```
Đọc file: docs/BUILD_14_Validation_Behavior.md
```

**Việc cần làm:**
- FluentValidation setup
- `ValidationBehavior<TRequest, TResponse>` (MediatR pipeline behavior)
- Validation examples (CreateUserRequestValidator, UpdateProductRequestValidator)
- Auto-register validators

**Verify:**
// turbo
```bash
dotnet build
```
Expect: Validation errors trả về 422 Unprocessable Entity

---

## ✅ Phase 3 Checkpoint

**Kiểm tra:**
- [ ] `dotnet build` — 0 errors
- [ ] `CurrentUserMiddleware` registered trong pipeline
- [ ] `ExceptionMiddleware` trả proper HTTP status codes
- [ ] Validation hoạt động cho MediatR requests
- [ ] Thực hiện chỉnh sửa docs sau khi đã hoàn thiện phase này cho phù hợp (cập nhật docs nếu trong quá trình implement có thay đổi/tối ưu so với docs gốc).

**⏸️ DỪNG: Notify user review Phase 3 trước khi tiếp tục Phase 4 (`/build-phase4`)**
