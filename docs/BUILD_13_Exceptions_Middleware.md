# Exception Handling & Middleware

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 12 (Common Services) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Exception Handling System với custom exceptions và global exception middleware.

---

## 1. Overview

**Làm gì:** Xây dựng hệ thống xử lý exceptions toàn cục với custom exceptions và error responses nhất quán.

**Tại sao cần:**
- **Centralized Error Handling:** Xử lý tất cả exceptions ở một nơi duy nhất
- **Consistent Error Format:** Error response format chuẩn và nhất quán cho toàn bộ API
- **Better User Experience:** Error messages rõ ràng, dễ hiểu cho người dùng
- **Logging:** Tự động log errors với đầy đủ context (UserId, ErrorId, StackTrace)
- **HTTP Status Codes:** Trả đúng status code cho từng loại error
- **Production-Ready:** Không expose sensitive information trong error messages

**Trong bước này chúng ta sẽ:**
- ✅ Tạo `ErrorResult` model (error response format)
- ✅ Tạo `CustomException` base class
- ✅ Tạo các derived exceptions (NotFoundException, UnauthorizedException, etc.)
- ✅ Implement `ExceptionMiddleware` (global exception handler)
- ✅ Register middleware pipeline
- ✅ Handle FluentValidation exceptions
- ✅ Support inner exception unwrapping

**Real-world example:**
```csharp
// Trong handler - Throw exception
public class GetProductHandler : IRequestHandler<GetProductRequest, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductRequest request, CancellationToken ct)
    {
        var product = await _repository.FirstOrDefaultAsync(new ProductByIdSpec(request.Id), ct)
   ?? throw new NotFoundException($"Product with ID {request.Id} was not found.");
     
        return product.Adapt<ProductDto>();
    }
}

// Exception tự động được catch bởi ExceptionMiddleware
// Response:
// {
// "statusCode": 404,
//   "exception": "Product with ID 123 was not found.",
//   "errorId": "a1b2c3d4-...",
//   "supportMessage": "Provide the ErrorId a1b2c3d4-... to the support team for further analysis."
// }
```

---

## 2. Add Required Packages

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

Packages đã có từ bước trước (không cần add thêm):
- `Serilog` - Structured logging
- `Newtonsoft.Json` - JSON serialization
- `FluentValidation` - Validation support

**⚠️ Lưu ý:** 
- Tất cả packages cần thiết đã được add trong BUILD_12
- Không cần thêm package mới cho bước này

---

## 3. Tạo ErrorResult Model

### Bước 3.1: ErrorResult Class

**Làm gì:** Tạo model để format error responses một cách nhất quán.

**Tại sao:** Cần một format chuẩn để client biết cách parse error response.

**File:** `src/Infrastructure/Infrastructure/Middleware/ErrorResult.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Middleware;

/// <summary>
/// Model để trả về error response cho client
/// Format nhất quán cho tất cả errors
/// </summary>
public class ErrorResult
{
    /// <summary>
    /// Danh sách error messages (cho validation errors)
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Source của exception (class và method name)
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Exception message chính
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Unique error ID để tracking và debugging
    /// </summary>
    public string? ErrorId { get; set; }

    /// <summary>
  /// Support message hướng dẫn user liên hệ support team
    /// </summary>
    public string? SupportMessage { get; set; }

 /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }
}
```

**Giải thích:**
- `Messages`: List của error messages - dùng cho validation errors có nhiều lỗi
- `Source`: Class và method gây ra exception - hữu ích cho debugging
- `Exception`: Exception message chính - message hiển thị cho user
- `ErrorId`: Unique ID (Guid) - user cung cấp cho support team để tracking
- `SupportMessage`: Hướng dẫn user cách liên hệ support
- `StatusCode`: HTTP status code (400, 404, 500, etc.)

**Example response:**
```json
{
  "messages": [],
  "source": "ECO.WebApi.Application.Catalog.Products.GetProductRequestHandler.Handle",
  "exception": "Product with ID 123 was not found.",
  "errorId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "supportMessage": "Provide the ErrorId a1b2c3d4-e5f6-7890-abcd-ef1234567890 to the support team for further analysis.",
  "statusCode": 404
}
```

**Tại sao design này:**
- Consistent structure cho mọi errors
- Contains enough info để debug
- User-friendly với supportMessage
- Machine-readable với statusCode và errorId

---

## 4. Tạo Custom Exceptions

### Bước 4.1: CustomException Base Class

**Làm gì:** Tạo base exception class với HttpStatusCode và ErrorMessages properties.

**Tại sao:** Base class để tất cả custom exceptions kế thừa, đảm bảo có đủ properties cần thiết.

**File:** `src/Core/Application/Common/Exceptions/CustomException.cs`

```csharp
using System.Net;

namespace ECO.WebApi.Application.Common.Exceptions;

/// <summary>
/// Base exception class cho tất cả custom exceptions trong application
/// Kế thừa từ Exception để có đầy đủ properties (Message, StackTrace, etc.)
/// </summary>
public class CustomException : Exception
{
    /// <summary>
    /// Danh sách error messages (cho validation hoặc multiple errors)
 /// </summary>
    public List<string>? ErrorMessages { get; }

    /// <summary>
    /// HTTP status code tương ứng với exception này
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Constructor với message, errors, và status code
    /// </summary>
    /// <param name="message">Exception message chính</param>
    /// <param name="errors">Danh sách error messages (optional)</param>
    /// <param name="statusCode">HTTP status code (default: 500)</param>
  public CustomException(
     string message,
        List<string>? errors = default,
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(message)
    {
        ErrorMessages = errors;
        StatusCode = statusCode;
    }
}
```

**Giải thích:**
- Kế thừa `Exception` để có sẵn `Message`, `StackTrace`, `InnerException`, etc.
- `ErrorMessages`: List errors - useful cho validation errors có nhiều lỗi
- `StatusCode`: HTTP status code tương ứng - middleware sẽ dùng để set response status
- Default status code: 500 (InternalServerError) - safe default cho unknown errors

**Tại sao kế thừa Exception:**
- Có đầy đủ exception properties (Message, StackTrace, InnerException)
- Có thể throw và catch như normal exceptions
- Framework support (try-catch, logging, etc.)

**Lợi ích:**
- ✅ Type-safe exceptions
- ✅ HTTP status code embedded
- ✅ Support multiple error messages
- ✅ Easy to extend

---

### Bước 4.2: NotFoundException

**Làm gì:** Exception khi không tìm thấy entity/resource.

**Tại sao:** Cần một exception type riêng cho "not found" để trả đúng HTTP 404.

**File:** `src/Core/Application/Common/Exceptions/NotFoundException.cs`

```csharp
using System.Net;

namespace ECO.WebApi.Application.Common.Exceptions;

/// <summary>
/// Exception khi không tìm thấy entity/resource
/// HTTP Status Code: 404 Not Found
/// </summary>
public class NotFoundException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
  /// <param name="message">Message mô tả entity nào không tìm thấy</param>
    public NotFoundException(string message)
        : base(message, null, HttpStatusCode.NotFound)
 {
    }
}
```

**Giải thích:**
- Kế thừa `CustomException`
- Hardcode `HttpStatusCode.NotFound` (404)
- Không cần `ErrorMessages` list (chỉ có 1 message)

**Usage:**
```csharp
// Trong handler
var product = await _repository.FirstOrDefaultAsync(spec, ct)
  ?? throw new NotFoundException($"Product with ID {request.Id} was not found.");

// Trong service
var user = await _userManager.FindByIdAsync(userId)
    ?? throw new NotFoundException($"User with ID {userId} not found.");
```

**Lợi ích:**
- ✅ Clear semantic meaning (not found)
- ✅ Automatic HTTP 404 status code
- ✅ Consistent error handling

---

### Bước 4.3: UnauthorizedException

**Làm gì:** Exception khi user chưa authenticate (chưa login).

**Tại sao:** Cần phân biệt giữa "chưa login" (401) và "không có permission" (403).

**File:** `src/Core/Application/Common/Exceptions/UnauthorizedException.cs`

```csharp
using System.Net;

namespace ECO.WebApi.Application.Common.Exceptions;

/// <summary>
/// Exception khi user chưa authenticate (chưa login)
/// HTTP Status Code: 401 Unauthorized
/// </summary>
public class UnauthorizedException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
    /// <param name="message">Message yêu cầu user login</param>
    public UnauthorizedException(string message)
        : base(message, null, HttpStatusCode.Unauthorized)
    {
    }
}
```

**Giải thích:**
- Hardcode `HttpStatusCode.Unauthorized` (401)
- Dùng khi user chưa authenticate (chưa có JWT token)

**Usage:**
```csharp
// Check authentication
if (!_currentUser.IsAuthenticated())
    throw new UnauthorizedException("You must be logged in to access this resource.");

// Invalid token
if (!await _tokenService.ValidateTokenAsync(token))
    throw new UnauthorizedException("Invalid or expired token.");
```

**Phân biệt với 403 Forbidden:**
- **401 Unauthorized:** Chưa login (cần authenticate)
- **403 Forbidden:** Đã login nhưng không có permission (cần authorization)

---

### Bước 4.4: ForbiddenException

**Làm gì:** Exception khi user không có permission để thực hiện action.

**Tại sao:** User đã login nhưng không có quyền - cần trả HTTP 403.

**File:** `src/Core/Application/Common/Exceptions/ForbiddenException.cs`

```csharp
using System.Net;

namespace ECO.WebApi.Application.Common.Exceptions;

/// <summary>
/// Exception khi user không có permission để thực hiện action
/// HTTP Status Code: 403 Forbidden
/// </summary>
public class ForbiddenException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
    /// <param name="message">Message mô tả permission nào bị thiếu</param>
  public ForbiddenException(string message)
        : base(message, null, HttpStatusCode.Forbidden)
 {
    }
}
```

**Giải thích:**
- Hardcode `HttpStatusCode.Forbidden` (403)
- Dùng khi user đã authenticate nhưng không có permission

**Usage:**
```csharp
// Check permission
if (!_currentUser.IsInRole("Admin"))
    throw new ForbiddenException("You do not have permission to access this resource.");

// Check specific permission
if (!await _authorizationService.HasPermissionAsync("Products.Delete"))
    throw new ForbiddenException("You do not have permission to delete products.");
```

**Phân biệt với 401 Unauthorized:**
- **401:** Chưa login → cần authenticate
- **403:** Đã login nhưng không đủ quyền → cần permission

---

### Bước 4.5: ConflictException

**Làm gì:** Exception khi có conflict (duplicate resource, business rule violation).

**Tại sao:** Cần một exception type cho conflict cases - trả HTTP 409.

**File:** `src/Core/Application/Common/Exceptions/ConflictException.cs`

```csharp
using System.Net;

namespace ECO.WebApi.Application.Common.Exceptions;

/// <summary>
/// Exception khi có conflict (duplicate entity, business rule violation, etc.)
/// HTTP Status Code: 409 Conflict
/// </summary>
public class ConflictException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
    /// <param name="message">Message mô tả conflict gì</param>
    public ConflictException(string message)
        : base(message, null, HttpStatusCode.Conflict)
    {
    }
}
```

**Giải thích:**
- Hardcode `HttpStatusCode.Conflict` (409)
- Dùng cho duplicate resources hoặc business rule violations

**Usage:**
```csharp
// Duplicate email
if (await _userManager.FindByEmailAsync(request.Email) != null)
    throw new ConflictException($"Email {request.Email} is already registered.");

// Duplicate role
if (await _roleManager.RoleExistsAsync(request.Name))
    throw new ConflictException($"Role {request.Name} already exists.");

// Business rule
if (product.Stock < request.Quantity)
    throw new ConflictException("Insufficient stock for this order.");
```

**Lợi ích:**
- ✅ Clear semantic meaning (conflict)
- ✅ Appropriate HTTP status code (409)
- ✅ Used for duplicate checks

---

### Bước 4.6: InternalServerException

**Làm gì:** Exception cho internal server errors hoặc unexpected errors.

**Tại sao:** Cần một exception type cho errors không expected - trả HTTP 500.

**File:** `src/Core/Application/Common/Exceptions/InternalServerException.cs`

```csharp
using System.Net;

namespace ECO.WebApi.Application.Common.Exceptions;

/// <summary>
/// Exception cho internal server errors hoặc unexpected errors
/// HTTP Status Code: 500 Internal Server Error
/// </summary>
public class InternalServerException : CustomException
{
    /// <summary>
    /// Constructor với message và optional error list
    /// </summary>
    /// <param name="message">Error message chính</param>
    /// <param name="errors">Danh sách detailed errors (optional)</param>
    public InternalServerException(string message, List<string>? errors = default)
        : base(message, errors, HttpStatusCode.InternalServerError)
    {
    }
}
```

**Giải thích:**
- Hardcode `HttpStatusCode.InternalServerError` (500)
- Support `errors` list để include detailed error messages
- Dùng cho unexpected errors hoặc system errors

**Usage:**
```csharp
// Identity operation failed
var result = await _roleManager.CreateAsync(role);
if (!result.Succeeded)
    throw new InternalServerException(
    "Register role failed",
        result.Errors.Select(e => e.Description).ToList());

// Database connection failed
try
{
 await _db.SaveChangesAsync();
}
catch (Exception ex)
{
    throw new InternalServerException("Database operation failed", new List<string> { ex.Message });
}

// External service failed
if (!response.IsSuccessStatusCode)
    throw new InternalServerException("External service call failed");
```

**Lợi ích:**
- ✅ Support detailed error list
- ✅ Used for unexpected errors
- ✅ Clear semantic meaning

---

## 5. Implement ExceptionMiddleware

### Bước 5.1: ExceptionMiddleware Class

**Làm gì:** Middleware để catch tất cả exceptions và trả về error responses nhất quán.

**Tại sao:** 
- Centralized error handling
- Consistent error format
- Automatic logging với context
- Proper HTTP status codes

**File:** `src/Infrastructure/Infrastructure/Middleware/ExceptionMiddleware.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Context;
using System.Net;

namespace ECO.WebApi.Infrastructure.Middleware;

/// <summary>
/// Middleware để catch và handle tất cả exceptions
/// Phải đặt đầu tiên trong middleware pipeline
/// </summary>
internal class ExceptionMiddleware : IMiddleware
{
    private readonly ICurrentUser _currentUser;
    private readonly ISerializerService _jsonSerializer;

 public ExceptionMiddleware(
 ICurrentUser currentUser,
        ISerializerService jsonSerializer)
    {
      _currentUser = currentUser;
      _jsonSerializer = jsonSerializer;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
     {
        // Continue với request pipeline
         await next(context);
     }
 catch (Exception exception)
     {
        // 1. Lấy user context
        string email = _currentUser.GetUserEmail() is string userEmail ? userEmail : "Anonymous";
        var userId = _currentUser.GetUserId();

        // 2. Push context vào Serilog
        if (userId != Guid.Empty)
        LogContext.PushProperty("UserId", userId);
        LogContext.PushProperty("UserEmail", email);

        // 3. Generate unique error ID
        string errorId = Guid.NewGuid().ToString();
        LogContext.PushProperty("ErrorId", errorId);
        LogContext.PushProperty("StackTrace", exception.StackTrace);

        // 4. Tạo ErrorResult
        var errorResult = new ErrorResult
        {
            Source = exception.TargetSite?.DeclaringType?.FullName,
            Exception = exception.Message.Trim(),
            ErrorId = errorId,
            SupportMessage = $"Provide the ErrorId {errorId} to the support team for further analysis."
        };

        // 5. Handle inner exception (unwrap)
        if (exception is not CustomException && exception.InnerException != null)
        {
           while (exception.InnerException != null)
           {
                exception = exception.InnerException;
           }
        }

        // 6. Handle FluentValidation exceptions
        if (exception is FluentValidation.ValidationException fluentException)
        {
            errorResult.Exception = "One or More Validations failed.";
            foreach (var error in fluentException.Errors)
            {
                errorResult.Messages.Add(error.ErrorMessage);
            }
         }

         // 7. Set status code dựa trên exception type
         switch (exception)
            {
            case CustomException e:
            errorResult.StatusCode = (int)e.StatusCode;
                if (e.ErrorMessages is not null)
                {
                    errorResult.Messages = e.ErrorMessages;
                }
            break;

            case KeyNotFoundException:
                errorResult.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            case FluentValidation.ValidationException:
                errorResult.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            default:
                errorResult.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
                }

            // 8. Log error
            Log.Error($"{errorResult.Exception} Request failed with Status Code {errorResult.StatusCode} and Error Id {errorId}.");

            // 9. Write error response
            var response = context.Response;
            if (!response.HasStarted)
            {
                 response.ContentType = "application/json";
                 response.StatusCode = errorResult.StatusCode;
                await response.WriteAsync(_jsonSerializer.Serialize(errorResult));
            }
            else
            {
                Log.Warning("Can't write error response. Response has already started.");
            }
        }
    }
}
```

**Giải thích flow chi tiết:**

**Step 1: Lấy user context**
- Lấy user email (hoặc "Anonymous" nếu chưa login)
- Lấy user ID

**Step 2: Push context vào Serilog**
- UserId, UserEmail, ErrorId, StackTrace
- Để logs có đủ context khi debug

**Step 3: Generate unique error ID**
- Dùng Guid để có unique ID
- User có thể cung cấp ErrorId cho support team

**Step 4: Tạo ErrorResult**
- Source: Class và method gây ra exception
- Exception: Exception message
- ErrorId: Unique ID
- SupportMessage: Hướng dẫn user

**Step 5: Handle inner exception (unwrap)**
- Unwrap inner exceptions để lấy root cause
- Lấy exception message gốc

**Step 6: Handle FluentValidation**
- Validation errors từ FluentValidation
- Add tất cả error messages vào `Messages` list

**Step 7: Set status code**
- `CustomException`: Lấy `StatusCode` từ exception
- `KeyNotFoundException`: 404 Not Found
- `ValidationException`: 400 Bad Request
- Default: 500 Internal Server Error

**Step 8: Log error**
- Log với Serilog
- Include ErrorId để tracking

**Step 9: Write error response**
- Set `ContentType` = "application/json"
- Set `StatusCode`
- Serialize `ErrorResult` và write vào response
- Check `response.HasStarted` để tránh lỗi

**Tại sao design này:**
- Catch all exceptions trong một nơi
- Consistent error format
- Rich logging với context
- User-friendly error messages
- Secure (không expose sensitive info)

**Lợi ích:**
- ✅ Centralized error handling
- ✅ Consistent response format
- ✅ Automatic logging
- ✅ Support validation errors
- ✅ Unwrap inner exceptions
- ✅ Unique error tracking

---

## 6. Register Middleware

### Bước 6.1: Middleware Registration

**Làm gì:** Tạo extension methods để register và use ExceptionMiddleware.

**Tại sao:** Modular và clean registration pattern.

**File:** `src/Infrastructure/Infrastructure/Middleware/Startup.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Middleware;

internal static class Startup
{
    /// <summary>
    /// Add middleware services vào DI container
    /// </summary>
    internal static IServiceCollection AddExceptionMiddleware(this IServiceCollection services) =>
        services.AddScoped<ExceptionMiddleware>();

    /// <summary>
  /// Use exception middleware trong request pipeline
    /// </summary>
    internal static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder app) =>
     app.UseMiddleware<ExceptionMiddleware>();
}
```

**Giải thích:**
- `AddExceptionMiddleware()`: Register middleware as Scoped service
- `UseExceptionMiddleware()`: Add middleware vào pipeline
- Extension methods để code gọn và consistent

**Tại sao Scoped:**
- Mỗi request có instance riêng
- Access được ICurrentUser (cũng là Scoped)
- Thread-safe

---

### Bước 6.2: Update Infrastructure Startup

**Làm gì:** Update Infrastructure Startup để register ExceptionMiddleware.

**Tại sao:** Centralized registration trong Infrastructure layer.

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Auth;
using ECO.WebApi.Infrastructure.Common;
using ECO.WebApi.Infrastructure.Middleware;
using ECO.WebApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure;

public static class Startup
{
    public static IServiceCollection AddInfrastructure(
this IServiceCollection services,
      IConfiguration config)
 {
        return services
        .AddPersistence()
     .AddCurrentUser()
       .AddCommonServices()
  .AddExceptionMiddleware()  // ← Add này
            .AddRouting(options => options.LowercaseUrls = true);
    }

    public static IApplicationBuilder UseInfrastructure(
        this IApplicationBuilder builder,
    IConfiguration config)
    {
        return builder
         .UseExceptionMiddleware()  // ← PHẢI ĐẦU TIÊN
    .UseRouting()
        .UseCurrentUserMiddleware()
       .UseHttpsRedirection()
            .UseAuthentication()
  .UseAuthorization();
    }
}
```

**⚠️ LƯU Ý THỨ TỰ MIDDLEWARE (QUAN TRỌNG!):**

```
1. UseExceptionMiddleware()  ← PHẢI ĐẦU TIÊN (catch tất cả exceptions)
2. UseRouting()
3. UseCurrentUserMiddleware()
4. UseHttpsRedirection()
5. UseAuthentication()
6. UseAuthorization()
7. MapControllers() / MapEndpoints()
```

**Tại sao thứ tự này:**
- `UseExceptionMiddleware()` đầu tiên → catch tất cả exceptions từ các middleware sau
- `UseRouting()` → xác định endpoint
- `UseCurrentUserMiddleware()` → set current user từ JWT
- `UseAuthentication()` → authenticate user (JWT middleware)
- `UseAuthorization()` → check permissions
- Endpoints cuối cùng

**Lợi ích:**
- ✅ Centralized registration
- ✅ Modular và maintainable
- ✅ Clear middleware order

---

## 7. Testing

### Bước 7.1: Test NotFoundException

**Làm gì:** Test exception khi không tìm thấy entity.

**File:** `src/Core/Application/Catalog/Products/GetProductRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Application.Common.Specification;
using ECO.WebApi.Domain.Catalog;
using Mapster;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

public class GetProductRequest : IRequest<ProductDto>
{
    public Guid Id { get; set; }
}

public class GetProductHandler : IRequestHandler<GetProductRequest, ProductDto>
{
    private readonly IRepository<Product> _repository;

    public GetProductHandler(IRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto> Handle(GetProductRequest request, CancellationToken ct)
    {
        // Throw NotFoundException nếu không tìm thấy
var product = await _repository.FirstOrDefaultAsync(
            new ProductByIdSpec(request.Id), ct)
            ?? throw new NotFoundException($"Product with ID {request.Id} was not found.");

        return product.Adapt<ProductDto>();
    }
}
```

**API Request:**
```bash
curl -X GET https://localhost:7001/api/products/00000000-0000-0000-0000-000000000001
```

**Expected Response (404):**
```json
{
  "messages": [],
  "source": "ECO.WebApi.Application.Catalog.Products.GetProductHandler.Handle",
  "exception": "Product with ID 00000000-0000-0000-0000-000000000001 was not found.",
  "errorId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "supportMessage": "Provide the ErrorId a1b2c3d4-... to the support team for further analysis.",
  "statusCode": 404
}
```

---

### Bước 7.2: Test ValidationException

**Làm gì:** Test FluentValidation exceptions với multiple errors.

**File:** `src/Core/Application/Catalog/Products/CreateProductRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

public class CreateProductRequest : IRequest<Guid>
{
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
}

public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
     .NotEmpty().WithMessage("Product name is required.")
      .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

     RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0.");
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    private readonly IRepository<Product> _repository;

    public CreateProductHandler(IRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
        var product = Product.Create(request.Name, request.Price);
        await _repository.AddAsync(product, ct);
        await _repository.SaveChangesAsync(ct);

        return product.Id;
    }
}
```

**API Request:**
```bash
curl -X POST https://localhost:7001/api/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "",
    "price": -100
  }'
```

**Expected Response (400):**
```json
{
  "messages": [
    "Product name is required.",
    "Price must be greater than 0."
  ],
  "source": null,
  "exception": "One or More Validations failed.",
  "errorId": "b2c3d4e5-f6g7-8901-bcde-f12345678901",
  "supportMessage": "Provide the ErrorId b2c3d4e5-... to the support team for further analysis.",
  "statusCode": 400
}
```

---

### Bước 7.3: Test UnauthorizedException

**Làm gì:** Test exception khi user chưa authenticate.

**API Request (without token):**
```bash
curl -X GET https://localhost:7001/api/users/me
```

**Expected Response (401):**
```json
{
  "messages": [],
  "source": null,
  "exception": "You must be logged in to access this resource.",
  "errorId": "c3d4e5f6-g7h8-9012-cdef-123456789012",
  "supportMessage": "Provide the ErrorId c3d4e5f6-... to the support team for further analysis.",
  "statusCode": 401
}
```

---

### Bước 7.4: Test ConflictException

**Làm gì:** Test exception khi có duplicate resource.

**File:** `src/Core/Application/Identity/Roles/CreateRoleRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace ECO.WebApi.Application.Identity.Roles;

public class CreateRoleRequest : IRequest<Guid>
{
    public string Name { get; set; } = default!;
}

public class CreateRoleHandler : IRequestHandler<CreateRoleRequest, Guid>
{
    private readonly RoleManager<ApplicationRole> _roleManager;

  public CreateRoleHandler(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task<Guid> Handle(CreateRoleRequest request, CancellationToken ct)
    {
 // Check duplicate
        if (await _roleManager.RoleExistsAsync(request.Name))
 throw new ConflictException($"Role {request.Name} already exists.");

        var role = new ApplicationRole
   {
    Name = request.Name,
            NormalizedName = request.Name.ToUpperInvariant()
   };

      await _roleManager.CreateAsync(role);
  return role.Id;
    }
}
```

**API Request:**
```bash
curl -X POST https://localhost:7001/api/roles \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Admin"
  }'
```

**Expected Response (409):**
```json
{
  "messages": [],
  "source": "ECO.WebApi.Application.Identity.Roles.CreateRoleHandler.Handle",
  "exception": "Role Admin already exists.",
  "errorId": "d4e5f6g7-h8i9-0123-defg-234567890123",
  "supportMessage": "Provide the ErrorId d4e5f6g7-... to the support team for further analysis.",
  "statusCode": 409
}
```

---

## 8. Common Issues & Solutions

### Issue 1: "Response has already started"

**Triệu chứng:**
```
Can't write error response. Response has already started.
```

**Nguyên nhân:**
Response đã được gửi một phần (headers hoặc body) trước khi exception xảy ra.

**Giải pháp:**
- Đảm bảo exception xảy ra TRƯỚC khi `await next()` gửi response
- Code đã handle case này với `response.HasStarted` check:
```csharp
if (!response.HasStarted)
{
    // Safe to write response
}
else
{
    Log.Warning("Can't write error response. Response has already started.");
}
```

---

### Issue 2: Inner exceptions không được log

**Triệu chứng:**
Exception message không rõ ràng, thiếu details.

**Nguyên nhân:**
Inner exception không được unwrap.

**Giải pháp:**
Code đã handle unwrap inner exceptions:
```csharp
if (exception is not CustomException && exception.InnerException != null)
{
    while (exception.InnerException != null)
    {
      exception = exception.InnerException;
    }
}
```

**Lợi ích:**
- Lấy được root cause exception
- Message rõ ràng hơn

---

### Issue 3: ValidationException không có messages

**Triệu chứng:**
Validation errors không hiện trong response.

**Nguyên nhân:**
FluentValidation exceptions không được handle đúng.

**Giải pháp:**
Code đã handle FluentValidation:
```csharp
if (exception is FluentValidation.ValidationException fluentException)
{
    errorResult.Exception = "One or More Validations failed.";
    foreach (var error in fluentException.Errors)
    {
        errorResult.Messages.Add(error.ErrorMessage);
    }
}
```

---

### Issue 4: Sensitive information exposed

**Triệu chứng:**
Error messages expose database connection strings, stack traces, etc.

**Giải pháp:**
- Custom exceptions chỉ chứa user-friendly messages
- Stack traces chỉ log, không trả về client
- Database errors được wrap trong InternalServerException với generic message

**Example:**
```csharp
// ❌ Wrong - Expose connection string
throw new Exception($"Database connection failed: Server={server};Database={db}");

// ✅ Right - Generic message
throw new InternalServerException("Database connection failed");
```

---

## 9. Best Practices

### ✅ Do's (Nên làm)

**1. Throw specific exceptions:**
```csharp
// ✅ Đúng - Specific exception
throw new NotFoundException($"Product {id} not found.");

// ❌ Sai - Generic exception
throw new Exception("Not found");
```

**2. Include context in message:**
```csharp
// ✅ Đúng - Include ID/name
throw new NotFoundException($"Product with ID {request.Id} was not found.");

// ❌ Sai - Generic message
throw new NotFoundException("Product not found");
```

**3. Use proper status codes:**
```csharp
// ✅ Đúng
NotFoundException → 404
UnauthorizedException → 401
ForbiddenException → 403
ConflictException → 409
InternalServerException → 500

// ❌ Sai - Dùng sai status code
throw new CustomException("Not found", null, HttpStatusCode.OK); // 200
```

**4. Let middleware handle exceptions:**
```csharp
// ✅ Đúng - Throw exception, để middleware handle
public async Task<ProductDto> Handle(...)
{
    var product = await _repository.FirstOrDefaultAsync(spec)
  ?? throw new NotFoundException($"Product {id} not found.");
    
    return product.Adapt<ProductDto>();
}

// ❌ Sai - Catch và return null
public async Task<ProductDto> Handle(...)
{
    try
    {
        var product = await _repository.FirstOrDefaultAsync(spec);
     if (product == null) return null; // Client không biết lỗi gì
    }
    catch (Exception ex)
    {
 // Swallow exception - Sai!
        return null;
    }
}
```

---

### ❌ Don'ts (Không nên làm)

**1. Catch exceptions trong handlers:**
```csharp
// ❌ Sai - Catch trong handler
public async Task<ProductDto> Handle(...)
{
    try
    {
        var product = await _repository.FirstOrDefaultAsync(spec);
        if (product == null) return null;
    }
    catch (Exception ex)
    {
        // Log và swallow exception - Sai!
        _logger.LogError(ex, "Error");
        return null;
    }
}
```

**2. Return null thay vì throw exception:**
```csharp
// ❌ Sai
public async Task<ProductDto> GetProduct(Guid id)
{
    var product = await _repository.GetByIdAsync(id);
    if (product == null) return null; // Client không biết lỗi gì
}

// ✅ Đúng
public async Task<ProductDto> GetProduct(Guid id)
{
    var product = await _repository.GetByIdAsync(id)
        ?? throw new NotFoundException($"Product {id} not found.");
    
  return product.Adapt<ProductDto>();
}
```

**3. Expose sensitive information:**
```csharp
// ❌ Sai - Expose connection string
throw new Exception($"Database connection failed: {connectionString}");

// ❌ Sai - Expose internal paths
throw new Exception($"File not found at {internalPath}");

// ✅ Đúng - Generic message
throw new InternalServerException("Database connection failed");
throw new NotFoundException("File not found");
```

---

### 💡 Tips

**1. Use ErrorId for debugging:**
- User báo lỗi → Cung cấp ErrorId
- Support team search logs theo ErrorId
- Có đủ context để debug (UserId, StackTrace, Request, etc.)

**Example flow:**
```
1. User gặp error → Copy ErrorId từ response
2. User report cho support: "ErrorId: a1b2c3d4-..."
3. Support team search logs: Log.ForContext("ErrorId", "a1b2c3d4-...")
4. Có đầy đủ context: UserId, UserEmail, StackTrace, Request
```

**2. Localization:**
Có thể extend để support multiple languages:
```csharp
// Future enhancement
public class NotFoundException : CustomException
{
    public NotFoundException(string messageKey, params object[] args)
 : base(_localizer[messageKey, args], null, HttpStatusCode.NotFound)
    {
  }
}

// Usage
throw new NotFoundException("Product.NotFound", productId);
```

**3. Custom error codes:**
Có thể thêm error codes ngoài HTTP status codes:
```csharp
public class ErrorResult
{
    public string? ErrorCode { get; set; } // "PRODUCT_NOT_FOUND", "INVALID_PAYMENT"
    // ... existing properties
}

// Usage
var errorResult = new ErrorResult
{
    ErrorCode = "PRODUCT_NOT_FOUND",
    StatusCode = 404,
    // ...
};
```

---

## 10. Summary

### ✅ Đã hoàn thành trong bước này:

**Exception Models:**
- ✅ `ErrorResult` model (error response format)
- ✅ `CustomException` base class
- ✅ `NotFoundException` (404)
- ✅ `UnauthorizedException` (401)
- ✅ `ForbiddenException` (403)
- ✅ `ConflictException` (409)
- ✅ `InternalServerException` (500)

**Middleware:**
- ✅ `ExceptionMiddleware` implementation
- ✅ Global exception handling
- ✅ Logging với context (UserId, ErrorId, StackTrace)
- ✅ Proper HTTP status codes
- ✅ FluentValidation support
- ✅ Inner exception unwrapping

**Features:**
- ✅ Centralized error handling
- ✅ Consistent error format
- ✅ User-friendly error messages
- ✅ Secure (no sensitive info exposure)
- ✅ Unique ErrorId for tracking

### 🎯 Key Concepts:

**CustomException Hierarchy:**
```
Exception (System)
    └─ CustomException (Base)
   ├─ NotFoundException (404)
├─ UnauthorizedException (401)
        ├─ ForbiddenException (403)
        ├─ ConflictException (409)
        └─ InternalServerException (500)
```

**ExceptionMiddleware Flow:**
```
Request → try { await next() } → Response
      ↓ (exception)
          catch (Exception)
     ↓
    1. Get user context
 2. Push to Serilog
    3. Generate ErrorId
    4. Create ErrorResult
    5. Unwrap inner exception
    6. Handle FluentValidation
    7. Set status code
    8. Log error
    9. Write JSON response
```

**Error Response Format:**
```json
{
  "messages": [],
  "source": "Namespace.Class.Method",
  "exception": "Error message",
  "errorId": "guid",
  "supportMessage": "Contact support with ErrorId",
  "statusCode": 404
}
```

### 📁 File Structure:

```
src/Core/Application/Common/Exceptions/
├── CustomException.cs
├── NotFoundException.cs
├── UnauthorizedException.cs
├── ForbiddenException.cs
├── ConflictException.cs
└── InternalServerException.cs

src/Infrastructure/Infrastructure/Middleware/
├── ErrorResult.cs
├── ExceptionMiddleware.cs
└── Startup.cs
```

### 🔑 Important Points:

1. **Middleware Order:** ExceptionMiddleware phải đầu tiên
2. **Status Codes:** Mỗi exception type có HTTP status riêng
3. **Logging:** Tự động log với context (UserId, ErrorId)
4. **Security:** Không expose sensitive information
5. **Validation:** Support FluentValidation exceptions
6. **Tracking:** Unique ErrorId cho mỗi error

---

## 11. Next Steps

**Tiếp theo:** [BUILD_14 - Validation Behavior](BUILD_14_Validation_Behavior.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Setup FluentValidation
2. ✅ Tạo `ValidationBehavior` (MediatR pipeline behavior)
3. ✅ Validator examples (CreateUserRequestValidator, UpdateProductRequestValidator)
4. ✅ Auto-register validators
5. ✅ Validation error handling
6. ✅ Custom validation rules

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
