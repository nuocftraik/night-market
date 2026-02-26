# Validation Behavior - FluentValidation với MediatR Pipeline

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 13 (Exception Handling & Middleware) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Validation System với FluentValidation và MediatR Pipeline Behaviors để tự động validate tất cả requests.

---

## 1. Overview

**Làm gì:** Xây dựng hệ thống validation tự động với FluentValidation và MediatR pipeline behaviors.

**Tại sao cần:**
- **Automatic Validation:** Tự động validate tất cả requests trước khi vào handler
- **Centralized Validation:** Validation logic tập trung, không phải check trong mỗi handler
- **Clean Handlers:** Handlers chỉ focus vào business logic, không lo validation
- **Consistent Error Messages:** Error messages nhất quán và rõ ràng
- **Early Failure:** Fail fast - phát hiện lỗi sớm nhất có thể
- **Reusable Validators:** Validators có thể reuse cho nhiều scenarios

**Trong bước này chúng ta sẽ:**
- ✅ Setup FluentValidation với MediatR
- ✅ Tạo `ValidationBehavior<TRequest, TResponse>` (MediatR pipeline behavior)
- ✅ Tạo base validator classes
- ✅ Validation examples (CreateUserRequestValidator, UpdateProductRequestValidator)
- ✅ Auto-register validators
- ✅ Custom validation rules
- ✅ Async validation support
- ✅ Integration với ExceptionMiddleware

**Real-world example:**
```csharp
// Request
public class CreateProductRequest : IRequest<Guid>
{
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

// Validator - Tự động được gọi trước handler
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
        .NotEmpty().WithMessage("Product name is required.")
       .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0.");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.");
 }
}

// Handler - Không cần validation code
public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
   // Request đã được validate - chỉ cần focus vào business logic
        var product = Product.Create(request.Name, request.Price, request.Stock);
        await _repository.AddAsync(product, ct);
        return product.Id;
    }
}

// Nếu validation fail:
// {
//   "statusCode": 400,
//   "messages": [
//     "Product name is required.",
//     "Price must be greater than 0."
//   ],
//   "exception": "One or More Validations failed."
// }
```

---

## 2. Add Required Packages

**File:** `src/Core/Application/Application.csproj`

Packages đã có từ BUILD_04 (không cần add thêm):
- `FluentValidation` (v11.9.2)
- `FluentValidation.DependencyInjectionExtensions` (v11.9.2)
- `MediatR` (v12.4.0)

**⚠️ Lưu ý:** 
- FluentValidation đã được add trong BUILD_04
- MediatR đã được add trong BUILD_04
- Không cần thêm package mới cho bước này

---

## 3. Tạo ValidationBehavior

### Bước 3.1: ValidationBehavior Implementation

**Làm gì:** Tạo MediatR pipeline behavior để tự động validate tất cả requests.

**Tại sao:** 
- MediatR pipeline behavior chạy trước handler
- Tự động validate mọi request có validator
- Fail fast nếu validation errors
- Không cần manual validation trong handlers

**File:** `src/Core/Application/Common/Behaviors/ValidationBehavior.cs`

```csharp
using FluentValidation;
using MediatR;

namespace ECO.WebApi.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior để tự động validate requests
/// Chạy TRƯỚC handler, throw ValidationException nếu có lỗi
/// </summary>
/// <typeparam name="TRequest">Request type (IRequest<TResponse>)</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Constructor - inject tất cả validators cho TRequest
    /// </summary>
    /// <param name="validators">Danh sách validators (có thể empty)</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Handle method - được gọi bởi MediatR pipeline
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // 1. Nếu không có validators, skip validation
        if (!_validators.Any())
            {
                return await next();
            }

        // 2. Tạo validation context
        var context = new ValidationContext<TRequest>(request);

        // 3. Chạy tất cả validators song song
        var validationResults = await Task.WhenAll(
        _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // 4. Lấy tất cả validation failures
        var failures = validationResults
                            .Where(r => r.Errors.Any())
                            .SelectMany(r => r.Errors)
                             .ToList();

        // 5. Nếu có lỗi, throw ValidationException
        if (failures.Any())
        {
            throw new ValidationException(failures);
        }

        // 6. Validation passed - tiếp tục vào handler
                return await next();
    }
}
```

**Giải thích flow chi tiết:**

**Step 1: Check validators**
- Nếu không có validators cho TRequest → skip validation
- Performance optimization - không waste time nếu không cần validate

**Step 2: Tạo ValidationContext**
- Context chứa request instance
- FluentValidation cần context để validate

**Step 3: Chạy validators song song**
- `Task.WhenAll()` → chạy tất cả validators cùng lúc
- Performance - không chờ từng validator tuần tự
- Support async validators

**Step 4: Collect failures**
- Lấy tất cả errors từ tất cả validators
- `SelectMany()` → flatten list of errors

**Step 5: Throw ValidationException**
- Nếu có errors → throw `FluentValidation.ValidationException`
- ExceptionMiddleware (BUILD_13) sẽ catch và trả response
- Handler KHÔNG được gọi nếu validation fail

**Step 6: Continue to handler**
- Nếu validation pass → gọi `next()` (handler)
- Handler nhận request đã validated

**Tại sao design này:**
- Automatic validation cho mọi requests
- Handlers clean - không cần validation code
- Consistent error handling
- Support multiple validators per request
- Async validation support

**Lợi ích:**
- ✅ Automatic validation
- ✅ Clean handlers
- ✅ Fail fast
- ✅ Parallel validator execution
- ✅ Consistent error messages

---

## 4. Register ValidationBehavior

### Bước 4.1: Update Application Startup

**Làm gì:** Register ValidationBehavior vào MediatR pipeline.

**Tại sao:** MediatR cần biết về behavior để execute nó trước handlers.

**File:** `src/Core/Application/Startup.cs`

```csharp
using System.Reflection;
using ECO.WebApi.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Application;

public static class Startup
{
    /// <summary>
    /// Add Application services
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        return services.AddMediatR(cfg =>
         {
            cfg.RegisterServicesFromAssembly(assembly);
                
        // Add ValidationBehavior vào pipeline
         cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            })
      
            // FluentValidation - Auto-discover validators
            .AddValidatorsFromAssembly(assembly);
    }
}
```

**Giải thích:**

**MediatR Configuration:**
- `RegisterServicesFromAssembly()`: Register tất cả handlers
- `AddBehavior()`: Add ValidationBehavior vào pipeline
- `typeof(IPipelineBehavior<,>)`: Generic interface
- `typeof(ValidationBehavior<,>)`: Generic implementation

**FluentValidation Configuration:**
- `AddValidatorsFromAssembly()`: Tự động scan và register tất cả validators trong assembly
- Validators phải implement `IValidator<T>`
- Registered as Scoped services

**Pipeline order:**
```
Request
    ↓
ValidationBehavior
    ↓ (if validation pass)
Handler
    ↓
Response
```

**Tại sao thứ tự này:**
- ValidationBehavior chạy đầu tiên
- Fail fast nếu invalid
- Handler chỉ nhận valid requests

---

## 5. Base Validator Classes

### Bước 5.1: CustomValidator Base Class

**Làm gì:** Tạo base validator class với common validation rules.

**Tại sao:** Reuse common validations, consistent error messages.

**File:** `src/Core/Application/Common/Validation/CustomValidator.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Common.Validation;

/// <summary>
/// Base validator class với common validation rules
/// </summary>
/// <typeparam name="T">Type của object cần validate</typeparam>
public abstract class CustomValidator<T> : AbstractValidator<T>
{
    /// <summary>
    /// Validate GUID không empty
    /// </summary>
    protected IRuleBuilderOptions<T, Guid> MustNotBeEmpty(IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
        .NotEmpty()
        .WithMessage("{PropertyName} is required.");
    }

    /// <summary>
    /// Validate string không empty và max length
    /// </summary>
    protected IRuleBuilderOptions<T, string> MustNotBeEmpty(
        IRuleBuilder<T, string> ruleBuilder, 
        int maxLength = 255)
    {
      return ruleBuilder
            .NotEmpty()
            .WithMessage("{PropertyName} is required.")
            .MaximumLength(maxLength)
            .WithMessage("{PropertyName} must not exceed {MaxLength} characters.");
    }

    /// <summary>
    /// Validate email format
    /// </summary>
    protected IRuleBuilderOptions<T, string> MustBeValidEmail(IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email format.")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters.");
    }

    /// <summary>
    /// Validate phone number format
    /// </summary>
    protected IRuleBuilderOptions<T, string?> MustBeValidPhoneNumber(IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .When(x => !string.IsNullOrEmpty(ruleBuilder.ToString()))
            .WithMessage("Invalid phone number format.");
    }

    /// <summary>
    /// Validate password strength
    /// </summary>
    protected IRuleBuilderOptions<T, string> MustBeStrongPassword(IRuleBuilder<T, string> ruleBuilder)
    {
     return ruleBuilder
        .NotEmpty()
        .WithMessage("Password is required.")
        .MinimumLength(8)
        .WithMessage("Password must be at least 8 characters.")
        .Matches(@"[A-Z]")
        .WithMessage("Password must contain at least one uppercase letter.")
        .Matches(@"[a-z]")
            .WithMessage("Password must contain at least one lowercase letter.")
      .Matches(@"[0-9]")
    .WithMessage("Password must contain at least one number.")
            .Matches(@"[\W_]")
       .WithMessage("Password must contain at least one special character.");
    }

    /// <summary>
    /// Validate decimal greater than zero
    /// </summary>
    protected IRuleBuilderOptions<T, decimal> MustBeGreaterThanZero(IRuleBuilder<T, decimal> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThan(0)
       .WithMessage("{PropertyName} must be greater than 0.");
    }

    /// <summary>
    /// Validate int greater than or equal to zero
    /// </summary>
    protected IRuleBuilderOptions<T, int> MustNotBeNegative(IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage("{PropertyName} cannot be negative.");
 }
}
```

**Giải thích:**

**Common Rules:**
- `MustNotBeEmpty(Guid)`: Validate GUID không empty
- `MustNotBeEmpty(string, maxLength)`: String required với max length
- `MustBeValidEmail()`: Email format validation
- `MustBeValidPhoneNumber()`: Phone number format (E.164)
- `MustBeStrongPassword()`: Password strength rules
- `MustBeGreaterThanZero()`: Decimal > 0
- `MustNotBeNegative()`: Int >= 0

**Usage:**
```csharp
public class CreateUserValidator : CustomValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
 .MustBeValidEmail(RuleFor(x => x.Email));

        RuleFor(x => x.Password)
            .MustBeStrongPassword(RuleFor(x => x.Password));
    }
}
```

**Lợi ích:**
- ✅ Reusable validation rules
- ✅ Consistent error messages
- ✅ Less boilerplate code
- ✅ Easy to maintain

---

## 6. Validator Examples

### Bước 6.1: CreateUserRequestValidator

**Làm gì:** Validator cho CreateUserRequest.

**File:** `src/Core/Application/Identity/Users/CreateUserRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Request DTO để tạo user mới
/// </summary>
public class CreateUserRequest : IRequest<Guid>
{
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Validator cho CreateUserRequest
/// Tự động được gọi bởi ValidationBehavior
/// </summary>
public class CreateUserRequestValidator : CustomValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
    // Email validation
        RuleFor(x => x.Email)
        .MustBeValidEmail(RuleFor(x => x.Email));

        // FirstName validation
        RuleFor(x => x.FirstName)
            .MustNotBeEmpty(RuleFor(x => x.FirstName), maxLength: 100);

        // LastName validation
  RuleFor(x => x.LastName)
      .MustNotBeEmpty(RuleFor(x => x.LastName), maxLength: 100);

 // Password validation
     RuleFor(x => x.Password)
            .MustBeStrongPassword(RuleFor(x => x.Password));

   // Confirm password validation
    RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
    .WithMessage("Confirm password is required.")
            .Equal(x => x.Password)
       .WithMessage("Password and confirmation password do not match.");

        // Phone number validation (optional)
   When(x => !string.IsNullOrEmpty(x.PhoneNumber), () =>
      {
RuleFor(x => x.PhoneNumber)
    .MustBeValidPhoneNumber(RuleFor(x => x.PhoneNumber));
        });
 }
}
```

**Giải thích:**

**Email validation:**
- Required, valid format, max 255 chars

**Name validation:**
- FirstName và LastName required
- Max 100 characters each

**Password validation:**
- Strong password rules:
  - Min 8 characters
- 1 uppercase letter
  - 1 lowercase letter
  - 1 number
  - 1 special character

**Confirm password:**
- Must match password

**Phone number (optional):**
- Only validate if provided
- E.164 format

**Usage:**
```csharp
// Handler - Không cần validation code
public class CreateUserHandler : IRequestHandler<CreateUserRequest, Guid>
{
    public async Task<Guid> Handle(CreateUserRequest request, CancellationToken ct)
    {
   // Request đã validated - focus vào business logic
        var user = ApplicationUser.Create(
          request.Email,
   request.FirstName,
  request.LastName);

        await _userManager.CreateAsync(user, request.Password);
        return user.Id;
    }
}
```

---

### Bước 6.2: UpdateProductRequestValidator

**Làm gì:** Validator cho UpdateProductRequest với async validation.

**File:** `src/Core/Application/Catalog/Products/UpdateProductRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Application.Common.Specification;
using ECO.WebApi.Application.Common.Validation;
using ECO.WebApi.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Request DTO để update product
/// </summary>
public class UpdateProductRequest : IRequest<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public Guid CategoryId { get; set; }
}

/// <summary>
/// Validator cho UpdateProductRequest
/// Có async validation để check product và category tồn tại
/// </summary>
public class UpdateProductRequestValidator : CustomValidator<UpdateProductRequest>
{
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Category> _categoryRepository;

 public UpdateProductRequestValidator(
        IRepository<Product> productRepository,
        IRepository<Category> categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;

        // Id validation
        RuleFor(x => x.Id)
            .MustNotBeEmpty(RuleFor(x => x.Id))
         .MustAsync(ProductMustExist)
            .WithMessage("Product with ID {PropertyValue} does not exist.");

    // Name validation
    RuleFor(x => x.Name)
            .MustNotBeEmpty(RuleFor(x => x.Name), maxLength: 200);

        // Description validation
        RuleFor(x => x.Description)
       .MustNotBeEmpty(RuleFor(x => x.Description), maxLength: 2000);

        // Price validation
   RuleFor(x => x.Price)
     .MustBeGreaterThanZero(RuleFor(x => x.Price))
     .LessThan(1000000)
    .WithMessage("Price must be less than 1,000,000.");

        // Stock validation
        RuleFor(x => x.Stock)
            .MustNotBeNegative(RuleFor(x => x.Stock))
   .LessThan(100000)
        .WithMessage("Stock must be less than 100,000.");

        // CategoryId validation
        RuleFor(x => x.CategoryId)
      .MustNotBeEmpty(RuleFor(x => x.CategoryId))
.MustAsync(CategoryMustExist)
   .WithMessage("Category with ID {PropertyValue} does not exist.");
    }

    /// <summary>
    /// Async validation: Check product tồn tại
    /// </summary>
private async Task<bool> ProductMustExist(Guid id, CancellationToken ct)
    {
  var product = await _productRepository.GetByIdAsync(id, ct);
        return product != null;
    }

    /// <summary>
    /// Async validation: Check category tồn tại
    /// </summary>
    private async Task<bool> CategoryMustExist(Guid id, CancellationToken ct)
    {
    var category = await _categoryRepository.GetByIdAsync(id, ct);
  return category != null;
  }
}
```

**Giải thích:**

**Async Validation:**
- `MustAsync()` → async validation rule
- Call repository để check existence
- Run parallel với các validators khác

**ProductMustExist:**
- Check product với ID tồn tại trong database
- Return `false` nếu không tồn tại → validation fail

**CategoryMustExist:**
- Check category với ID tồn tại
- Prevent foreign key constraint errors

**Business Rules:**
- Price < 1,000,000 (business limit)
- Stock < 100,000 (warehouse limit)

**Tại sao design này:**
- Catch errors sớm (before handler)
- Prevent database constraint errors
- User-friendly error messages
- Async validation support

---

### Bước 6.3: SearchProductsRequestValidator

**Làm gì:** Validator cho search/filter requests.

**File:** `src/Core/Application/Catalog/Products/SearchProductsRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Models;
using ECO.WebApi.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Request DTO để search products
/// </summary>
public class SearchProductsRequest : PaginationFilter, IRequest<PaginatedResult<ProductDto>>
{
    public string? Keyword { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public Guid? CategoryId { get; set; }
}

/// <summary>
/// Validator cho SearchProductsRequest
/// Validate pagination và price range
/// </summary>
public class SearchProductsRequestValidator : CustomValidator<SearchProductsRequest>
{
    public SearchProductsRequestValidator()
    {
        // PageNumber validation
        RuleFor(x => x.PageNumber)
      .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be at least 1.");

        // PageSize validation
 RuleFor(x => x.PageSize)
    .GreaterThanOrEqualTo(1)
      .WithMessage("Page size must be at least 1.")
            .LessThanOrEqualTo(100)
     .WithMessage("Page size must not exceed 100.");

   // MinPrice validation (nếu có)
When(x => x.MinPrice.HasValue, () =>
        {
            RuleFor(x => x.MinPrice!.Value)
          .GreaterThanOrEqualTo(0)
          .WithMessage("Minimum price cannot be negative.");
 });

        // MaxPrice validation (nếu có)
        When(x => x.MaxPrice.HasValue, () =>
      {
       RuleFor(x => x.MaxPrice!.Value)
       .GreaterThanOrEqualTo(0)
                .WithMessage("Maximum price cannot be negative.");
    });

   // Price range validation (nếu có cả min và max)
        When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.MinPrice!.Value <= x.MaxPrice!.Value)
   .WithMessage("Minimum price must be less than or equal to maximum price.");
        });

    // Keyword validation (nếu có)
        When(x => !string.IsNullOrEmpty(x.Keyword), () =>
        {
            RuleFor(x => x.Keyword)
                .MaximumLength(100)
         .WithMessage("Keyword must not exceed 100 characters.");
    });
    }
}
```

**Giải thích:**

**Pagination Validation:**
- PageNumber >= 1
- PageSize: 1-100 (prevent large queries)

**Price Range:**
- MinPrice >= 0 (nếu có)
- MaxPrice >= 0 (nếu có)
- MinPrice <= MaxPrice (nếu có cả 2)

**Keyword:**
- Max 100 characters (nếu có)

**Conditional Validation:**
- `When()` → chỉ validate nếu condition true
- Không validate optional fields nếu null

**Lợi ích:**
- ✅ Prevent invalid queries
- ✅ Protect database performance
- ✅ User-friendly error messages

---

## 7. Custom Validation Rules

### Bước 7.1: Custom Validator Extensions

**Làm gì:** Tạo custom validation rules có thể reuse.

**File:** `src/Core/Application/Common/Validation/ValidatorExtensions.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Common.Validation;

/// <summary>
/// Extension methods cho custom validation rules
/// </summary>
public static class ValidatorExtensions
{
  /// <summary>
    /// Validate list không empty
    /// </summary>
    public static IRuleBuilderOptions<T, IList<TElement>> NotEmptyList<T, TElement>(
        this IRuleBuilder<T, IList<TElement>> ruleBuilder)
    {
   return ruleBuilder
            .NotNull()
   .WithMessage("{PropertyName} is required.")
   .Must(list => list.Any())
      .WithMessage("{PropertyName} must contain at least one item.");
 }

    /// <summary>
    /// Validate list max count
    /// </summary>
    public static IRuleBuilderOptions<T, IList<TElement>> MaximumCount<T, TElement>(
        this IRuleBuilder<T, IList<TElement>> ruleBuilder, 
        int max)
  {
        return ruleBuilder
            .Must(list => list == null || list.Count <= max)
     .WithMessage($"{{PropertyName}} must not exceed {max} items.");
    }

    /// <summary>
    /// Validate date không trong quá khứ
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> NotInThePast<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder)
    {
     return ruleBuilder
        .Must(date => date >= DateTime.UtcNow)
     .WithMessage("{PropertyName} must not be in the past.");
    }

    /// <summary>
    /// Validate date không trong tương lai
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> NotInTheFuture<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder)
    {
  return ruleBuilder
    .Must(date => date <= DateTime.UtcNow)
         .WithMessage("{PropertyName} must not be in the future.");
    }

    /// <summary>
  /// Validate date range
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> WithinRange<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder,
        DateTime min,
        DateTime max)
    {
        return ruleBuilder
            .Must(date => date >= min && date <= max)
      .WithMessage($"{{PropertyName}} must be between {min:yyyy-MM-dd} and {max:yyyy-MM-dd}.");
    }

    /// <summary>
    /// Validate URL format
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidUrl<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
      return ruleBuilder
      .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
  .When(x => !string.IsNullOrEmpty(ruleBuilder.ToString()))
         .WithMessage("{PropertyName} must be a valid URL.");
    }

    /// <summary>
    /// Validate file extension
    /// </summary>
    public static IRuleBuilderOptions<T, string> HasValidExtension<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        params string[] allowedExtensions)
    {
        return ruleBuilder
            .Must(fileName =>
            {
                if (string.IsNullOrEmpty(fileName)) return false;
   var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return allowedExtensions.Contains(extension);
        })
            .WithMessage($"{{PropertyName}} must have one of the following extensions: {string.Join(", ", allowedExtensions)}");
    }

    /// <summary>
/// Validate unique items trong list
    /// </summary>
    public static IRuleBuilderOptions<T, IList<TElement>> MustHaveUniqueItems<T, TElement>(
        this IRuleBuilder<T, IList<TElement>> ruleBuilder)
  {
        return ruleBuilder
       .Must(list => list == null || list.Distinct().Count() == list.Count)
     .WithMessage("{PropertyName} must not contain duplicate items.");
    }
}
```

**Usage Examples:**
```csharp
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        // List validation
        RuleFor(x => x.Items)
            .NotEmptyList()
            .MaximumCount(100)
 .MustHaveUniqueItems();

        // Date validation
        RuleFor(x => x.DeliveryDate)
   .NotInThePast()
  .WithinRange(DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));

        // URL validation
     RuleFor(x => x.WebsiteUrl)
          .MustBeValidUrl();

        // File extension validation
        RuleFor(x => x.FileName)
  .HasValidExtension(".jpg", ".png", ".pdf");
    }
}
```

---

## 8. Testing

### Bước 8.1: Test Successful Validation

**Request với valid data:**
```bash
curl -X POST https://localhost:7001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "password": "SecureP@ssw0rd",
    "confirmPassword": "SecureP@ssw0rd",
 "phoneNumber": "+84123456789"
  }'
```

**Expected Response (201):**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

---

### Bước 8.2: Test Validation Failure - Single Error

**Request với invalid email:**
```bash
curl -X POST https://localhost:7001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "invalid-email",
    "firstName": "John",
    "lastName": "Doe",
    "password": "SecureP@ssw0rd",
    "confirmPassword": "SecureP@ssw0rd"
  }'
```

**Expected Response (400):**
```json
{
  "statusCode": 400,
  "messages": [
    "Invalid email format."
  ],
  "exception": "One or More Validations failed.",
  "errorId": "b2c3d4e5-f6g7-8901-bcde-f12345678901",
  "supportMessage": "Provide the ErrorId b2c3d4e5-... to the support team for further analysis."
}
```

---

### Bước 8.3: Test Validation Failure - Multiple Errors

**Request với nhiều lỗi:**
```bash
curl -X POST https://localhost:7001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "invalid-email",
 "firstName": "",
    "lastName": "A very long last name that exceeds the maximum length of 100 characters which will cause a validation error",
    "password": "weak",
    "confirmPassword": "different"
  }'
```

**Expected Response (400):**
```json
{
  "statusCode": 400,
  "messages": [
    "Invalid email format.",
    "FirstName is required.",
  "LastName must not exceed 100 characters.",
    "Password must be at least 8 characters.",
  "Password must contain at least one uppercase letter.",
    "Password must contain at least one number.",
    "Password must contain at least one special character.",
    "Password and confirmation password do not match."
  ],
  "exception": "One or More Validations failed.",
  "errorId": "c3d4e5f6-g7h8-9012-cdef-123456789012",
  "supportMessage": "Provide the ErrorId c3d4e5f6-... to the support team for further analysis."
}
```

---

### Bước 8.4: Test Async Validation

**Request với non-existent product:**
```bash
curl -X PUT https://localhost:7001/api/products/00000000-0000-0000-0000-000000000001 \
  -H "Content-Type: application/json" \
  -d '{
    "id": "00000000-0000-0000-0000-000000000001",
    "name": "Updated Product",
    "description": "Updated description",
    "price": 99.99,
    "stock": 50,
    "categoryId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }'
```

**Expected Response (400):**
```json
{
  "statusCode": 400,
  "messages": [
    "Product with ID 00000000-0000-0000-0000-000000000001 does not exist."
  ],
"exception": "One or More Validations failed.",
  "errorId": "d4e5f6g7-h8i9-0123-defg-234567890123",
  "supportMessage": "Provide the ErrorId d4e5f6g7-... to the support team for further analysis."
}
```

---

## 9. Best Practices

### ✅ Do's (Nên làm)

**1. Validate trong validators, không trong handlers:**
```csharp
// ✅ Đúng - Validation trong validator
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
 RuleFor(x => x.Price).GreaterThan(0);
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
        // Không cần validation code - focus vào business logic
      var product = Product.Create(request.Name, request.Price);
        await _repository.AddAsync(product, ct);
return product.Id;
 }
}

// ❌ Sai - Validation trong handler
public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
        // Không nên validate trong handler
        if (string.IsNullOrEmpty(request.Name))
     throw new ValidationException("Name is required");
        
        if (request.Price <= 0)
            throw new ValidationException("Price must be greater than 0");
     
        // ...
    }
}
```

**2. Use descriptive error messages:**
```csharp
// ✅ Đúng - Clear message
RuleFor(x => x.Email)
    .EmailAddress()
    .WithMessage("Invalid email format. Please provide a valid email address.");

// ❌ Sai - Vague message
RuleFor(x => x.Email)
    .EmailAddress()
    .WithMessage("Invalid");
```

**3. Validate business rules:**
```csharp
// ✅ Đúng - Validate business rules
RuleFor(x => x.Price)
    .GreaterThan(0)
    .WithMessage("Price must be greater than 0.")
    .LessThan(1000000)
 .WithMessage("Price exceeds maximum allowed value.");

RuleFor(x => x.DiscountPercent)
    .InclusiveBetween(0, 100)
    .WithMessage("Discount must be between 0% and 100%.");
```

**4. Use async validation cho database checks:**
```csharp
// ✅ Đúng - Async validation
RuleFor(x => x.Email)
    .MustAsync(EmailMustBeUnique)
    .WithMessage("Email {PropertyValue} is already registered.");

private async Task<bool> EmailMustBeUnique(string email, CancellationToken ct)
{
    var exists = await _userRepository.AnyAsync(new UserByEmailSpec(email), ct);
    return !exists;
}
```

---

### ❌ Don'ts (Không nên làm)

**1. Validate quá chi tiết:**
```csharp
// ❌ Sai - Quá chi tiết, không cần thiết
RuleFor(x => x.FirstName)
    .NotEmpty()
    .WithMessage("First name is required.")
    .MinimumLength(2)
    .WithMessage("First name must be at least 2 characters.")
    .MaximumLength(50)
    .WithMessage("First name must not exceed 50 characters.")
    .Matches(@"^[a-zA-Z\s]+$")
    .WithMessage("First name can only contain letters and spaces.")
    .Must(name => !name.Contains("  "))
    .WithMessage("First name cannot contain consecutive spaces.")
  .Must(name => char.IsUpper(name[0]))
    .WithMessage("First name must start with uppercase letter.");

// ✅ Đúng - Đủ validation
RuleFor(x => x.FirstName)
    .NotEmpty().WithMessage("First name is required.")
    .MaximumLength(50).WithMessage("First name must not exceed 50 characters.");
```

**2. Return technical error messages:**
```csharp
// ❌ Sai - Technical message
RuleFor(x => x.Price)
 .GreaterThan(0)
    .WithMessage("Price failed validation: decimal.Parse() > 0");

// ✅ Đúng - User-friendly message
RuleFor(x => x.Price)
    .GreaterThan(0)
    .WithMessage("Price must be greater than 0.");
```

**3. Validate trong multiple places:**
```csharp
// ❌ Sai - Duplicate validation
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
RuleFor(x => x.Name).NotEmpty();
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
        // Duplicate validation - Sai!
        if (string.IsNullOrEmpty(request.Name))
            throw new ValidationException("Name is required");
        
    // ...
    }
}

// ✅ Đúng - Chỉ validate một nơi (validator)
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken ct)
    {
   // Không cần validation - đã validated rồi
        var product = Product.Create(request.Name, request.Price);
        await _repository.AddAsync(product, ct);
        return product.Id;
    }
}
```

---

### 💡 Tips

**1. Organize validators by feature:**
```
src/Core/Application/
├── Identity/
│   └── Users/
│       ├── CreateUserRequest.cs
│       ├── CreateUserRequestValidator.cs  ← Cùng file với request
│       └── CreateUserHandler.cs
├── Catalog/
│   └── Products/
│       ├── CreateProductRequest.cs
│       ├── CreateProductRequestValidator.cs
│       └── CreateProductHandler.cs
```

**2. Reuse validators:**
```csharp
// Base validator
public class ProductValidator : AbstractValidator<ProductDto>
{
    public ProductValidator()
    {
 RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
}
}

// Reuse trong request validators
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        // Include base validator
        Include(new ProductValidator());
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductValidator()
    {
        // Reuse same rules
    Include(new ProductValidator());
      
     // Add specific rules cho update
        RuleFor(x => x.Id).NotEmpty();
    }
}
```

**3. Conditional validation:**
```csharp
// Chỉ validate nếu có value
When(x => !string.IsNullOrEmpty(x.PhoneNumber), () =>
{
    RuleFor(x => x.PhoneNumber)
     .Matches(@"^\+?[1-9]\d{1,14}$")
        .WithMessage("Invalid phone number format.");
});

// Validate dựa trên property khác
RuleFor(x => x.DiscountPrice)
    .LessThan(x => x.Price)
    .When(x => x.DiscountPrice.HasValue)
  .WithMessage("Discount price must be less than regular price.");
```

---

## 10. Summary

### ✅ Đã hoàn thành trong bước này:

**Core Components:**
- ✅ `ValidationBehavior<TRequest, TResponse>` (MediatR pipeline behavior)
- ✅ Auto-register validators với FluentValidation
- ✅ `CustomValidator<T>` base class với common rules
- ✅ Custom validator extensions
- ✅ Integration với ExceptionMiddleware

**Validator Examples:**
- ✅ `CreateUserRequestValidator` (user registration)
- ✅ `UpdateProductRequestValidator` (với async validation)
- ✅ `SearchProductsRequestValidator` (pagination và filters)

**Features:**
- ✅ Automatic validation cho mọi requests
- ✅ Parallel validator execution
- ✅ Async validation support
- ✅ Conditional validation
- ✅ Custom validation rules
- ✅ Reusable validators
- ✅ User-friendly error messages

### 🎯 Key Concepts:

**ValidationBehavior Flow:**
```
MediatR Request
    ↓
ValidationBehavior
    ↓
Check validators
    ↓
Run all validators (parallel)
    ↓
Collect failures
    ↓
If errors → throw ValidationException
    ↓ (caught by ExceptionMiddleware)
Return 400 with error messages
    
If no errors → continue
    ↓
Handler
    ↓
Response
```

**Validator Lifecycle:**
```
Application Startup
    ↓
AddValidatorsFromAssembly()
    ↓
Scan assembly
    ↓
Register all IValidator<T> implementations
    ↓
Scoped lifetime

Request comes in
  ↓
MediatR resolves validators for request type
    ↓
Inject into ValidationBehavior
    ↓
Execute validation
```

**Error Response Format:**
```json
{
  "statusCode": 400,
  "messages": [
    "Email is required.",
  "Password must be at least 8 characters."
  ],
  "exception": "One or More Validations failed.",
  "errorId": "guid",
  "supportMessage": "Contact support message"
}
```

### 📁 File Structure:

```
src/Core/Application/
├── Common/
│   ├── Behaviors/
│ │   └── ValidationBehavior.cs
│   └── Validation/
│       ├── CustomValidator.cs
│     └── ValidatorExtensions.cs
├── Identity/
│   └── Users/
│       ├── CreateUserRequest.cs
│       └── CreateUserRequestValidator.cs  ← Cùng file
├── Catalog/
│   └── Products/
│    ├── UpdateProductRequest.cs
│       ├── UpdateProductRequestValidator.cs
│       ├── SearchProductsRequest.cs
│       └── SearchProductsRequestValidator.cs
└── Startup.cs  ← Register ValidationBehavior
```

### 🔑 Important Points:

1. **Automatic Validation:** ValidationBehavior tự động validate mọi requests
2. **Clean Handlers:** Handlers không cần validation code
3. **Fail Fast:** Validation errors phát hiện trước khi vào handler
4. **Async Support:** Support async validators (database checks)
5. **Parallel Execution:** Tất cả validators chạy song song
6. **Integration:** Tích hợp hoàn hảo với ExceptionMiddleware (BUILD_13)

---

## 11. Next Steps

**Tiếp theo:** [BUILD_15 - JWT Authentication](BUILD_15_JWT_Authentication.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Setup JWT Authentication
2. ✅ Tạo `ITokenService` interface
3. ✅ Implement `TokenService` (generate access/refresh tokens)
4. ✅ JWT middleware configuration
5. ✅ `TokenRequest`, `TokenResponse`, `RefreshTokenRequest` DTOs
6. ✅ Login endpoint
7. ✅ Refresh token endpoint
8. ✅ JWT token validation

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
