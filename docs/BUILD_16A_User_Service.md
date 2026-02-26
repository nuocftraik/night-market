# User Management Service - User CRUD Operations

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 15 (JWT Authentication) đã hoàn thành

Tài liệu này hướng dẫn xây dựng User Management Service - Quản lý người dùng với đầy đủ CRUD operations.

---

## 1. Overview

**Làm gì:** Xây dựng User Management Service để quản lý người dùng (Create, Read, Update, Toggle Status).

**Tại sao cần:**
- **User Management:** CRUD operations cho user accounts
- **Self-Registration:** Cho phép users tự đăng ký tài khoản
- **Profile Management:** Users có thể update thông tin cá nhân
- **Status Management:** Admin có thể active/deactive users

**Trong bước này chúng ta sẽ:**
- ✅ Tạo IUserService interface (partial methods)
- ✅ Tạo User DTOs (UserDetailDto, CreateUserRequest, UpdateUserRequest)
- ✅ Implement UserService với các operations:
  - Search users với pagination
  - Get user details
  - Create user (admin) & Self-register (anonymous)
  - Update user profile (basic info only)
  - Toggle user status (active/inactive)
- ✅ Tạo UsersController với RESTful endpoints
- ✅ FluentValidation cho tất cả requests

**⚠️ Lưu ý về Timeline:**
- ✅ **Email Confirmation:** Interface đã có, implementation sẽ hoàn thiện trong BUILD_23 (Email Service)
- ✅ **Image Upload:** Interface đã có, implementation sẽ hoàn thiện trong BUILD_20 (File Storage)
- ✅ **Background Jobs:** Implementation sẽ hoàn thiện trong BUILD_24 (Hangfire)
- ✅ **Password Operations:** Implementation sẽ hoàn thiện sau khi có Email Service
- ✅ **Permission Operations:** Implementation sẽ hoàn thiện sau khi có Cache Service (BUILD_19)

**Real-world example:**
```csharp
// Admin creates user
var createRequest = new CreateUserRequest
{
    FirstName = "John",
    LastName = "Doe",
    Email = "john@example.com",
    UserName = "johndoe",
    Password = "SecurePass123!",
    ConfirmPassword = "SecurePass123!",
    PhoneNumber = "+84987654321"
};

var message = await _userService.CreateAsync(createRequest, origin);
// → User johndoe Registered.

// User updates profile (basic info only)
var updateRequest = new UpdateUserRequest
{
    Id = userId,
    FirstName = "John",
    LastName = "Smith",
    PhoneNumber = "+84987654322"
};

await _userService.UpdateAsync(updateRequest, userId);
// → Profile updated successfully

// Admin toggles user status
await _userService.ToggleStatusAsync(new ToggleUserStatusRequest 
{ 
    UserId = userId, 
    ActivateUser = false 
}, cancellationToken);
// → User deactivated
```

---

## 2. User DTOs

### Bước 2.1: UserDetailDto

**Làm gì:** DTO để hiển thị thông tin user.

**Tại sao:** Không expose toàn bộ ApplicationUser entity, chỉ trả về fields cần thiết.

**File:** `src/Core/Application/Identity/Users/UserDetailDto.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// User detail DTO (dùng cho responses)
/// </summary>
public class UserDetailDto
{
    /// <summary>
    /// User ID (Guid)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Username (unique)
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name
    /// </summary>
    public string? LastName { get; set; }

 /// <summary>
    /// Email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Is user active (can login)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Has email been confirmed
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Phone number
/// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Avatar image URL
    /// </summary>
    public string? ImageUrl { get; set; }
}
```

**Giải thích:**
- **Id:** User ID dạng Guid
- **IsActive:** Admin có thể deactivate users (ngăn login)
- **EmailConfirmed:** Track email confirmation status
- **ImageUrl:** Relative path to avatar image

**Tại sao không expose Password:**
- Security: Never return password hashes
- Separation of concerns: DTOs chỉ chứa display data

---

### Bước 2.2: CreateUserRequest

**Làm gì:** Request DTO để tạo user mới.

**Tại sao:** Type-safe request với validation rules.

**File:** `src/Core/Application/Identity/Users/CreateUserRequest.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Request để tạo user mới
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// First name (required)
  /// </summary>
    public string FirstName { get; set; } = default!;

    /// <summary>
    /// Last name (required)
    /// </summary>
    public string LastName { get; set; } = default!;

    /// <summary>
    /// Email address (required, unique)
    /// </summary>
    public string Email { get; set; } = default!;

    /// <summary>
    /// Username (required, unique, min 6 chars)
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// Password (required, min 6 chars)
    /// </summary>
    public string Password { get; set; } = default!;

    /// <summary>
    /// Confirm password (must match Password)
    /// </summary>
    public string ConfirmPassword { get; set; } = default!;

    /// <summary>
    /// Phone number (optional, unique if provided)
    /// </summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Validator cho CreateUserRequest
/// </summary>
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserService userService)
    {
        RuleFor(u => u.Email)
    .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Email is required.")
   .EmailAddress()
            .WithMessage("Invalid Email Address.")
   .MustAsync(async (email, _) => !await userService.ExistsWithEmailAsync(email))
            .WithMessage((_, email) => $"Email {email} is already registered.");

RuleFor(u => u.UserName)
         .Cascade(CascadeMode.Stop)
            .NotEmpty()
     .WithMessage("Username is required.")
  .MinimumLength(6)
    .WithMessage("Username must be at least 6 characters.")
.MustAsync(async (name, _) => !await userService.ExistsWithNameAsync(name))
            .WithMessage((_, name) => $"Username {name} is already taken.");

        RuleFor(u => u.PhoneNumber)
  .Cascade(CascadeMode.Stop)
    .MustAsync(async (phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!))
 .WithMessage((_, phone) => $"Phone number {phone} is already registered.")
  .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));

      RuleFor(p => p.FirstName)
    .Cascade(CascadeMode.Stop)
            .NotEmpty()
         .WithMessage("First name is required.");

 RuleFor(p => p.LastName)
  .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Last name is required.");

RuleFor(p => p.Password)
            .Cascade(CascadeMode.Stop)
     .NotEmpty()
     .WithMessage("Password is required.")
            .MinimumLength(6)
      .WithMessage("Password must be at least 6 characters.");

        RuleFor(p => p.ConfirmPassword)
   .Cascade(CascadeMode.Stop)
      .NotEmpty()
            .WithMessage("Confirm password is required.")
    .Equal(p => p.Password)
            .WithMessage("Password and Confirm Password must match.");
    }
}
```

**Giải thích:**

**Validation Rules:**
- **Email:** Required, valid email format, unique trong database
- **UserName:** Required, min 6 chars, unique
- **PhoneNumber:** Optional, nhưng nếu có thì phải unique
- **Password:** Required, min 6 chars
- **ConfirmPassword:** Must match Password

**Async Validation:**
- `ExistsWithEmailAsync()`: Check email đã tồn tại chưa
- `ExistsWithNameAsync()`: Check username đã tồn tại chưa
- `ExistsWithPhoneNumberAsync()`: Check phone number đã tồn tại chưa

**Tại sao Cascade(CascadeMode.Stop):**
- Stop validation chain nếu rule đầu tiên fail
- Ví dụ: Nếu Email empty, không cần check uniqueness nữa

---

### Bước 2.3: UpdateUserRequest

**Làm gì:** Request DTO để update user profile.

**Tại sao:** Cho phép users update thông tin cá nhân và avatar.

**File:** `src/Core/Application/Identity/Users/UpdateUserRequest.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Request để update user profile
/// </summary>
public class UpdateUserRequest
{
    /// <summary>
    /// User ID (required)
    /// </summary>
  public string Id { get; set; } = default!;

    /// <summary>
    /// First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
  /// Email address (unique)
  /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Avatar image upload
    /// </summary>
 public FileUploadRequest? Image { get; set; }

  /// <summary>
    /// Delete current avatar image
    /// </summary>
    public bool DeleteCurrentImage { get; set; } = false;
}

/// <summary>
/// Validator cho UpdateUserRequest
/// </summary>
public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator(IUserService userService)
    {
   RuleFor(p => p.Id)
.NotEmpty()
  .WithMessage("User ID is required.");

RuleFor(p => p.FirstName)
            .NotEmpty()
     .WithMessage("First name is required.")
            .MaximumLength(75)
   .WithMessage("First name must not exceed 75 characters.");

   RuleFor(p => p.LastName)
     .NotEmpty()
            .WithMessage("Last name is required.")
     .MaximumLength(75)
       .WithMessage("Last name must not exceed 75 characters.");

        RuleFor(p => p.Email)
      .NotEmpty()
            .WithMessage("Email is required.")
    .EmailAddress()
    .WithMessage("Invalid Email Address.")
.MustAsync(async (user, email, _) => !await userService.ExistsWithEmailAsync(email, user.Id))
.WithMessage((_, email) => $"Email {email} is already registered.");

        RuleFor(p => p.Image);

        RuleFor(u => u.PhoneNumber)
      .Cascade(CascadeMode.Stop)
      .MustAsync(async (user, phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!, user.Id))
            .WithMessage((_, phone) => $"Phone number {phone} is already registered.")
   .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));
    }
}
```

**Giải thích:**

**Validation với exceptId:**
- `ExistsWithEmailAsync(email, user.Id)`: Check email unique nhưng EXCLUDE current user
- `ExistsWithPhoneNumberAsync(phone, user.Id)`: Tương tự cho phone

**Image Upload:**
- **Image:** FileUploadRequest (from BUILD_20 File Storage)
- **DeleteCurrentImage:** Flag để xóa avatar hiện tại

**Tại sao cần exceptId:**
- User update profile nhưng giữ nguyên email/phone
- Không bị lỗi "email already registered" khi email là của chính họ

---

### Bước 2.4: ToggleUserStatusRequest

**Làm gì:** Request để toggle user active status.

**Tại sao:** Admin có thể activate/deactivate users.

**File:** `src/Core/Application/Identity/Users/ToggleUserStatusRequest.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Request để toggle user active status
/// </summary>
public class ToggleUserStatusRequest
{
    /// <summary>
 /// User ID to toggle
    /// </summary>
  public string UserId { get; set; } = default!;

    /// <summary>
    /// True = activate, False = deactivate
    /// </summary>
    public bool ActivateUser { get; set; }
}
```

**Giải thích:**
- **ActivateUser:** `true` = activate, `false` = deactivate
- Admin không thể deactivate chính mình

---

### Bước 2.5: UserParameterFilter

**Làm gì:** Filter cho user search với pagination.

**Tại sao:** Support search và pagination trong user list.

**File:** `src/Core/Application/Identity/Users/UserParameterFilter.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Filter cho user search với pagination
/// </summary>
public class UserParameterFilter : PaginationFilter
{
    /// <summary>
    /// Search keyword (search in name, email, username)
    /// </summary>
    public string? Keyword { get; set; }

  /// <summary>
    /// Filter by active status
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Filter by email confirmed status
    /// </summary>
    public bool? EmailConfirmed { get; set; }
}
```

**Giải thích:**
- Kế thừa `PaginationFilter` (PageNumber, PageSize từ BUILD_11)
- **Keyword:** Search trong name, email, username
- **IsActive:** Filter by active status
- **EmailConfirmed:** Filter by email confirmation status

---

## 3. User Service Interface

### Bước 3.1: IUserService Interface

**Làm gì:** Define contract cho user operations.

**Tại sao:** Abstraction, dễ test, dễ swap implementations.

**File:** `src/Core/Application/Identity/Users/IUserService.cs`

```csharp
using ECO.WebApi.Application.Identity.Users.Password;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Service xử lý user management operations
/// </summary>
public interface IUserService : ITransientService
{   
    #region Default Operations
    
    /// <summary>
    /// Search users với pagination và filters
    /// </summary>
    Task<PaginationResponse<UserDetailDto>> SearchAsync(
     UserParameterFilter filter, 
    CancellationToken cancellationToken);

    /// <summary>
    /// Check username đã tồn tại chưa
    /// </summary>
    Task<bool> ExistsWithNameAsync(string name);

    /// <summary>
/// Check email đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
    Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null);

    /// <summary>
    /// Check phone number đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
    Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null);

    /// <summary>
    /// Get full name của user
    /// </summary>
    Task<string> GetFullName(Guid userId);

    /// <summary>
    /// Get list tất cả users (không pagination)
    /// </summary>
    Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get total user count
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    Task<UserDetailDto> GetAsync(string userId, CancellationToken cancellationToken);

    #endregion

    #region Role Operations (sẽ implement trong BUILD_16B)
    
    /// <summary>
    /// Get user's assigned roles
    /// </summary>
 Task<List<UserRoleDto>> GetRolesAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Assign roles to user
    /// </summary>
    Task<string> AssignRolesAsync(
        string userId, 
        UserRolesRequest request, 
        CancellationToken cancellationToken);

    #endregion

    #region Permission Operations (sẽ implement trong BUILD_16C)
    
    /// <summary>
    /// Get user's permissions
    /// </summary>
    Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Check if user has specific permission
    /// </summary>
    Task<bool> HasPermissionAsync(
        string userId, 
        string permission, 
   CancellationToken cancellationToken = default);

    #endregion

    #region Create & Update Operations
    
    /// <summary>
    /// Toggle user active status (admin only)
    /// </summary>
    Task ToggleStatusAsync(ToggleUserStatusRequest request, CancellationToken cancellationToken);

 /// <summary>
    /// Create new user (admin hoặc self-register)
    /// </summary>
    Task<string> CreateAsync(CreateUserRequest request, string origin);

    /// <summary>
    /// Update user profile
  /// </summary>
    Task UpdateAsync(UpdateUserRequest request, string userId);

    #endregion

  #region Email Confirmation (sẽ implement chi tiết)
    
 /// <summary>
    /// Confirm email với verification code
    /// </summary>
    Task<string> ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken);

    /// <summary>
    /// Confirm phone number với verification code
    /// </summary>
    Task<string> ConfirmPhoneNumberAsync(string userId, string code);

    #endregion

    #region Password Operations (sẽ implement chi tiết)
 
    /// <summary>
    /// Send forgot password email
    /// </summary>
 Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, string origin);

    /// <summary>
    /// Reset password với reset token
    /// </summary>
    Task<string> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Change password (user đã login)
    /// </summary>
    Task ChangePasswordAsync(ChangePasswordRequest request, string userId);

    #endregion
}
```

**Giải thích:**

**Default Operations:**
- Search, Get, Count, Exists checks
- Core CRUD operations

**Role Operations:**
- GetRolesAsync, AssignRolesAsync
- Sẽ implement chi tiết trong BUILD_16B

**Permission Operations:**
- GetPermissionsAsync, HasPermissionAsync
- Sẽ implement chi tiết trong BUILD_16C

**Create & Update:**
- CreateAsync, UpdateAsync, ToggleStatusAsync
- Implement trong bước này

**Email Confirmation:**
- ConfirmEmailAsync, ConfirmPhoneNumberAsync
- Implement trong bước này

**Password Operations:**
- ForgotPasswordAsync, ResetPasswordAsync, ChangePasswordAsync
- Sẽ implement trong phần riêng

**Tại sao partial interface:**
- Interface có nhiều methods (20+ methods)
- Chia nhỏ implementations thành nhiều partial classes
- Dễ maintain và navigate code

---

## 4. User Service Implementation

### Bước 4.1: UserService - Main Class

**Làm gì:** Implement core user operations.

**Tại sao:** Business logic cho user management.

**File:** `src/Infrastructure/Infrastructure/Identity/UserService.cs`

```csharp
using Ardalis.Specification.EntityFrameworkCore;
using ECO.WebApi.Application.Common.Events;
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.Models;
using ECO.WebApi.Application.Common.Specification;
using ECO.WebApi.Application.Identity.Users;
using ECO.WebApi.Domain.Identity;
using ECO.WebApi.Infrastructure.Auth;
using ECO.WebApi.Infrastructure.Persistence.Context;
using ECO.WebApi.Shared.Authorization;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECO.WebApi.Infrastructure.Identity;

/// <summary>
/// Service xử lý user management operations
/// (Partial class - implementation chia thành nhiều files)
/// </summary>
internal partial class UserService : IUserService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly SecuritySettings _securitySettings;
    private readonly IEventPublisher _events;

    public UserService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
     RoleManager<ApplicationRole> roleManager,
  ApplicationDbContext db,
        IOptions<SecuritySettings> securitySettings,
        IEventPublisher events)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    _roleManager = roleManager;
        _db = db;
        _securitySettings = securitySettings.Value;
        _events = events;
    }

    #region Default Operations

    /// <summary>
    /// Search users với pagination và filters
    /// </summary>
    public async Task<PaginationResponse<UserDetailDto>> SearchAsync(
        UserParameterFilter filter, 
     CancellationToken cancellationToken)
    {
 // Build specification từ filter
 var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(filter);

        // Query với specification và project to DTO (efficient query)
     var users = await _userManager.Users
            .WithSpecification(spec)
            .ProjectToType<UserDetailDto>() // Mapster projection (chỉ select cần thiết)
  .ToListAsync(cancellationToken);

        // Get total count
        int count = await _userManager.Users.CountAsync(cancellationToken);

      return new PaginationResponse<UserDetailDto>(
        users, 
   count, 
          filter.PageNumber, 
            filter.PageSize);
    }

    /// <summary>
    /// Check username đã tồn tại chưa
    /// </summary>
    public async Task<bool> ExistsWithNameAsync(string name)
    {
   return await _userManager.FindByNameAsync(name) is not null;
    }

  /// <summary>
    /// Check email đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
    public async Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null)
    {
        return await _userManager.FindByEmailAsync(email.Normalize()) is ApplicationUser user 
     && user.Id != exceptId;
    }

    /// <summary>
    /// Check phone number đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
  public async Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null)
    {
        return await _userManager.Users
    .FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber) is ApplicationUser user 
            && user.Id != exceptId;
    }

    /// <summary>
    /// Get full name của user
    /// </summary>
    public async Task<string> GetFullName(Guid userId)
    {
        var user = await GetAsync(userId.ToString(), CancellationToken.None);
        return string.Join(" ", user.FirstName, user.LastName);
    }

    /// <summary>
    /// Get list tất cả users (không pagination)
    /// </summary>
    public async Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken) =>
        (await _userManager.Users
.AsNoTracking()
            .ToListAsync(cancellationToken))
.Adapt<List<UserDetailDto>>();

    /// <summary>
    /// Get total user count
    /// </summary>
    public Task<int> GetCountAsync(CancellationToken cancellationToken) =>
      _userManager.Users.AsNoTracking().CountAsync(cancellationToken);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    public async Task<UserDetailDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
         .AsNoTracking()
       .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

    _ = user ?? throw new NotFoundException("User Not Found.");

        return user.Adapt<UserDetailDto>();
    }

    /// <summary>
    /// Toggle user active status (admin only)
    /// </summary>
    public async Task ToggleStatusAsync(
        ToggleUserStatusRequest request, 
        CancellationToken cancellationToken)
    {
 var user = await _userManager.Users
 .Where(u => u.Id == request.UserId)
  .FirstOrDefaultAsync(cancellationToken);

 _ = user ?? throw new NotFoundException("User Not Found.");

   // Không cho phép deactivate admin
        bool isAdmin = await _userManager.IsInRoleAsync(user, ECORoles.Admin);
        if (isAdmin)
        {
 throw new ConflictException("Administrators Profile's Status cannot be toggled");
        }

        user.IsActive = request.ActivateUser;

        await _userManager.UpdateAsync(user);
    }

    #endregion
}
```

**Giải thích:**

**Dependencies (Chỉ những gì đã có tại BUILD_16A):**
- **UserManager:** ASP.NET Core Identity user management (BUILD_15)
- **SignInManager:** Sign in/out operations (BUILD_15)
- **RoleManager:** Role management (BUILD_15)
- **ApplicationDbContext:** Direct database access (BUILD_11)
- **SecuritySettings:** JWT settings (BUILD_15)
- **IEventPublisher:** Domain events (BUILD_12)

**⚠️ Dependencies sẽ thêm sau:**
- **IJobService:** BUILD_24 (Background Jobs)
- **IMailService, IEmailTemplateService:** BUILD_23 (Email Service)
- **IFileStorageService:** BUILD_20 (File Storage)
- **ICacheService:** BUILD_19 (Caching)

**SearchAsync:**
- Use `EntitiesByPaginationFilterSpec` (from BUILD_11)
- `ProjectToType<UserDetailDto>()`: Mapster projection (efficient, chỉ select fields cần thiết)
- Return `PaginationResponse` với total count

**Exists Methods:**
- **ExistsWithEmailAsync:** Check email unique, exclude current user nếu có
- **ExistsWithNameAsync:** Check username unique
- **ExistsWithPhoneNumberAsync:** Check phone unique, exclude current user

**ToggleStatusAsync:**
- Admin có thể activate/deactivate users
- KHÔNG cho phép toggle admin accounts
- Security check trước khi update

**Tại sao partial class:**
- UserService có nhiều methods (20+ methods)
- Chia thành nhiều files: UserService.cs, UserService.CreateUpdate.cs, UserService.Password.cs (sau), UserService.Role.cs, UserService.Permission.cs (sau), UserService.Confirm.cs (sau)
- Dễ maintain và navigate

### Bước 4.2: UserService - Create & Update Operations

**Làm gì:** Implement create và update user operations.

**Tại sao:** Separate file cho create/update logic (partial class pattern).

**File:** `src/Infrastructure/Infrastructure/Identity/UserService.CreateUpdate.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Identity.Users;
using ECO.WebApi.Domain.Identity;
using ECO.WebApi.Shared.Authorization;

namespace ECO.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Create & Update Operations (Partial Class)
/// </summary>
internal partial class UserService
{
    /// <summary>
    /// Create new user (admin hoặc self-register)
    /// </summary>
    public async Task<string> CreateAsync(CreateUserRequest request, string origin)
    {
        // Create ApplicationUser entity
        var user = new ApplicationUser
        {
        Email = request.Email,
     FirstName = request.FirstName,
    LastName = request.LastName,
     UserName = request.UserName,
       PhoneNumber = request.PhoneNumber,
            IsActive = true
        };

      // Create user với password (ASP.NET Core Identity)
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InternalServerException(
"Validation Errors Occurred.", 
                result.GetErrors());
        }

        // Assign "Basic" role by default
        await _userManager.AddToRoleAsync(user, ECORoles.Basic);

        var message = $"User {user.UserName} Registered.";

     // TODO: Email confirmation sẽ implement trong BUILD_23 (Email Service)
        // if (_securitySettings.RequireConfirmedAccount && !string.IsNullOrEmpty(user.Email))
        // {
    //     string emailVerificationUri = await GetEmailVerificationUriAsync(user, origin);
        //     var emailModel = new RegisterUserEmailModel { ... };
    //     var mailRequest = new MailRequest(...);
        //  _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
        //     message += $"\nPlease check {user.Email} to verify your account!";
        // }

        return message;
    }

    /// <summary>
    /// Update user profile (basic info only - no image upload yet)
    /// </summary>
    public async Task UpdateAsync(UpdateUserRequest request, string userId)
    {
      var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // TODO: Image upload sẽ implement trong BUILD_20 (File Storage)
   // if (request.Image != null || request.DeleteCurrentImage)
        // {
      //     user.ImageUrl = await _fileStorage.UploadAsync<ApplicationUser>(request.Image, FileType.Image);
        //     if (request.DeleteCurrentImage && !string.IsNullOrEmpty(currentImage))
        //     {
        //         _fileStorage.Remove(Path.Combine(root, currentImage));
        //     }
        // }

        // Update basic info
        user.FirstName = request.FirstName;
  user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.Email = request.Email;

        // Update phone number nếu changed
    string? phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        if (request.PhoneNumber != phoneNumber)
     {
     await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
    }

        // Update user trong database
        var result = await _userManager.UpdateAsync(user);

        // Refresh sign in (update claims)
        await _signInManager.RefreshSignInAsync(user);

     if (!result.Succeeded)
  {
            throw new InternalServerException("Update profile failed", result.GetErrors());
        }
    }
}
```

**Giải thích:**

**CreateAsync (Simplified):**
1. Create `ApplicationUser` entity từ request
2. `_userManager.CreateAsync(user, password)`: Create user với password hashing (Identity)
3. Assign "Basic" role by default
4. Return success message
5. **TODO:** Email confirmation sẽ implement khi có IMailService, IJobService, IEmailTemplateService (BUILD_23, BUILD_24)

**UpdateAsync (Simplified):**
1. Find user by ID
2. **TODO:** Image upload sẽ implement khi có IFileStorageService (BUILD_20)
3. Update basic info (FirstName, LastName, PhoneNumber, Email)
4. `SetPhoneNumberAsync`: Update phone number (Identity method)
5. `RefreshSignInAsync`: Update claims trong current session
6. Return errors nếu update failed

**Tại sao simplified:**
- Email và Background Jobs chưa có (BUILD_23, BUILD_24)
- File Storage chưa có (BUILD_20)
- Giữ code clean, không inject dependencies chưa tồn tại
- Dễ extend sau khi các services available

### Bước 4.3: Email Confirmation Operations (Placeholder)

**Làm gì:** Define email confirmation interface (implementation sau).

**Tại sao:** Prepare interface cho BUILD_23 (Email Service).

**File:** `src/Infrastructure/Infrastructure/Identity/UserService.Confirm.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace ECO.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Email/Phone Confirmation Operations (Partial Class)
/// </summary>
internal partial class UserService
{
    /// <summary>
    /// Confirm email với verification code
    /// TODO: Full implementation trong BUILD_23 (Email Service)
    /// </summary>
  public async Task<string> ConfirmEmailAsync(
  string userId, 
        string code, 
        CancellationToken cancellationToken)
    {
    var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

     // Decode code từ query string
   code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

        // Confirm email với Identity
 var result = await _userManager.ConfirmEmailAsync(user, code);

        if (result.Succeeded)
   {
return "Email confirmed successfully!";
   }

      throw new InternalServerException("An error occurred while confirming email.");
    }

    /// <summary>
 /// Confirm phone number với verification code
    /// TODO: Full implementation trong BUILD_23 (Email Service)
    /// </summary>
    public async Task<string> ConfirmPhoneNumberAsync(string userId, string code)
    {
   var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

      // Confirm phone với Identity
        var result = await _userManager.ChangePhoneNumberAsync(user, user.PhoneNumber!, code);

        if (result.Succeeded)
        {
return "Phone number confirmed successfully!";
}

        throw new InternalServerException("An error occurred while confirming phone number.");
    }

    // TODO: GetEmailVerificationUriAsync sẽ implement trong BUILD_23
    // private async Task<string> GetEmailVerificationUriAsync(ApplicationUser user, string origin)
    // {
    //  string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    //   code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
    //  const string route = "api/users/confirm-email";
    //     var endpointUri = new Uri(string.Concat($"{origin}/", route));
    //     string verificationUri = QueryHelpers.AddQueryString(endpointUri.ToString(), ...);
    //     return verificationUri;
    // }
}
```

**Giải thích:**

**ConfirmEmailAsync:**
- Core logic đã có (using Identity)
- Decode code từ Base64Url
- `_userManager.ConfirmEmailAsync(user, code)`: Confirm email
- **TODO:** Email sending logic trong BUILD_23

**ConfirmPhoneNumberAsync:**
- Tương tự ConfirmEmailAsync
- Use `ChangePhoneNumberAsync` với verification code

**GetEmailVerificationUriAsync (Commented):**
- Helper method để generate verification URI
- Sẽ implement trong BUILD_23 khi có Email Service

**Tại sao placeholder:**
- Interface đã define (IUserService)
- Core confirmation logic works (Identity)
- Email sending sẽ thêm sau (BUILD_23)
- Clean separation of concerns

---

## 5. User Controller

### Bước 5.1: UsersController Implementation

**Làm gì:** Tạo UsersController để xử lý HTTP requests cho user management.

**Tại sao:** RESTful API cho client ứng dụng.

**File:** `src/Host/Host/Controllers/Identity/UsersController.cs`

```csharp
using ECO.WebApi.Application.Identity.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers.Identity;

/// <summary>
/// Controller cho user management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Tìm kiếm users với pagination
    /// </summary>
    [HttpGet("search")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] UserParameterFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _userService.SearchAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get user details by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> GetUserById(string id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin tạo user mới
    /// </summary>
    [HttpPost("create")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request, 
        CancellationToken cancellationToken)
    {
        var message = await _userService.CreateAsync(request, Request.GetOrigin());
        return Ok(message);
    }

    /// <summary>
    /// Self-register tài khoản mới
    /// </summary>
    [HttpPost("self-register")]
    public async Task<IActionResult> SelfRegister(
        [FromBody] CreateUserRequest request, 
        CancellationToken cancellationToken)
    {
        var message = await _userService.CreateAsync(request, Request.GetOrigin());
        return Ok(message);
    }

    /// <summary>
    /// Cập nhật user profile (basic info)
    /// </summary>
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateUser(
        string id, 
        [FromBody] UpdateUserRequest request, 
        CancellationToken cancellationToken)
    {
        await _userService.UpdateAsync(request, id);
        return Ok();
    }

    /// <summary>
    /// Admin kích hoạt/deactivate user
    /// </summary>
    [HttpPost("toggle-status")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> ToggleUserStatus(
        [FromBody] ToggleUserStatusRequest request, 
        CancellationToken cancellationToken)
    {
        await _userService.ToggleStatusAsync(request, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Xác nhận email
    /// TODO: Implement in BUILD_23
    /// </summary>
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
    // Placeholder - реализация будет в BUILD_23
        return Ok("Email confirmation logic будет реализована в BUILD_23.");
    }

    /// <summary>
    /// Quên mật khẩu - gửi email xác nhận
    /// TODO: Implement in BUILD_23
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
    // Placeholder - реализacija будет в BUILD_23
        return Ok("Forgot password logic будет реализована в BUILD_23.");
    }
}
```

**Giải thích:**

**Endpoints:**
- `GET /search`: Tìm kiếm users (admin chỉ)
- `GET /{id}`: Lấy thông tin user theo ID (admin chỉ)
- `POST /create`: Admin tạo user mới
- `POST /self-register`: Cho phép user tự đăng ký tài khoản
- `PUT /update/{id}`: Cập nhật thông tin user (self-service)
- `POST /toggle-status`: Admin kích hoạt/deactivate user
- `POST /confirm-email`: Xác nhận email (chưa thực hiện)
- `POST /forgot-password`: Quên mật khẩu - gửi email xác nhận (chưa thực hiện)

**Authorization:**
- Sử dụng policy "admin" cho các hành động nhạy cảm (tìm kiếm, tạo, cập nhật, kích hoạt/deactivate user)

**Placeholder actions:**
- Một số actions như xác nhận email và quên mật khẩu chưa được implement chi tiết trong bước này. Chúng sẽ được hoàn thiện trong các BUILD sau.

---

## 6. Testing User Service

### Bước 6.1: Test Self-Register API

**API Call:**
```bash
curl -X POST https://localhost:7001/api/users/self-register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "Doe",
    "email": "john.doe@example.com",
    "userName": "johndoe",
    "password": "SecurePass123!",
  "confirmPassword": "SecurePass123!",
    "phoneNumber": "+84987654321"
  }'
```

**Expected Response:**
```text
User johndoe Registered.
```

**⚠️ Note:** Email confirmation sẽ có sau BUILD_23 (Email Service).

---

### Bước 6.2: Test Get User Details

**API Call:**
```bash
curl -X GET https://localhost:7001/api/users/<user_id> \
  -H "Authorization: Bearer <your_jwt_token>"
```

**Expected Response:**
```json
{
  "id": "<user_id>",
  "userName": "johndoe",
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe@example.com",
  "isActive": true,
  "emailConfirmed": true,
  "phoneNumber": "+84987654321",
  "imageUrl": null
}
```

**Giải thích:**
- Thay thế `<user_id>` bằng ID thực tế của user trong database.
- Thay thế `<your_jwt_token>` bằng token hợp lệ của admin.
- Kiểm tra thông tin trả về trong response body.

---

### Bước 6.3: Test Admin Toggle User Status

**API Call:**
```bash
curl -X POST https://localhost:7001/api/users/toggle-status \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your_jwt_token>" \
  -d '{
    "userId": "<user_id>",
    "activateUser": false
  }'
```

**Expected Response:**
```text
User deactivated.
```

**Giải thích:**
- Thay thế `<user_id>` bằng ID thực tế của user cần deactivate.
- Thay thế `<your_jwt_token>` bằng token hợp lệ của admin.
- Kiểm tra trạng thái user trong database sau khi thực hiện request.

---

## 7. Summary

Trong tài liệu này, chúng ta đã tìm hiểu về User Management Service với các chức năng CRUD cơ bản cho người dùng. Chúng ta đã định nghĩa các DTOs cần thiết, tạo ra IUserService interface cho các operation liên quan đến người dùng, và implement UserService với các phương thức tìm kiếm, tạo, cập nhật và kích hoạt/deactivate người dùng. Cuối cùng, chúng ta đã xây dựng UsersController để xử lý các HTTP requests liên quan đến quản lý người dùng.

Tài liệu này sẽ được cập nhật trong các BUILD sau để hoàn thiện các chức năng còn thiếu như xác nhận email, quên mật khẩu, và phân quyền người dùng.

---

## 8. Next Steps

Trong các bước tiếp theo, chúng ta sẽ tập trung vào việc hoàn thiện các chức năng còn thiếu của User Management Service, bao gồm:

- Xác nhận email người dùng sau khi đăng ký (hoàn thiện trong BUILD_23)
- Quên mật khẩu và đặt lại mật khẩu thông qua email (hoàn thiện trong BUILD_23)
- Tích hợp dịch vụ lưu trữ file cho avatar người dùng (hoàn thiện trong BUILD_20)
- Tích hợp dịch vụ cache để tăng tốc độ truy xuất dữ liệu (hoàn thiện trong BUILD_19)
- Phân quyền người dùng và các quyền hạn tương ứng (hoàn thiện trong BUILD_16B và BUILD_16C)

Chúng ta cũng sẽ viết test tự động cho các chức năng mới được thêm vào, đảm bảo rằng tất cả các tính năng hoạt động đúng như mong đợi và không gây ra lỗi trong quá trình phát triển tiếp theo.

Hẹn gặp lại trong các BUILD tiếp theo!
