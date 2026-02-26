# File Storage - Local File Upload/Download Service

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 19 (Caching Services) đã hoàn thành

Tài liệu này hướng dẫn xây dựng File Storage Service - hệ thống quản lý file upload/download với local disk storage.

---

## 1. Overview

**Làm gì:** Xây dựng file storage service để:
- Upload files (images, documents) lên local disk
- Serve files qua static file middleware
- Organize files theo entity type (automatic folder structure)
- Validate file size, extension, format
- Remove files khi cần

**Tại sao cần:**
- **User Content:** User avatars, product images, attachments
- **File Management:** Centralized file handling logic
- **Security:** Validate file types, prevent malicious uploads
- **Organization:** Auto-organize files by entity type (User, Product, Category...)
- **Performance:** Serve static files efficiently (không qua controller)

**Trong bước này chúng ta sẽ:**
- ✅ Tạo `FileType` enum (Domain layer)
- ✅ Tạo `FileUploadRequest` DTO với validation
- ✅ Tạo `IFileStorageService` interface (Application layer)
- ✅ Implement `LocalFileStorageService` (Infrastructure layer)
- ✅ Setup static file middleware
- ✅ Tạo extension methods (EnumExtensions, RegexExtensions)
- ✅ Usage examples với Controllers

**Real-world example:**
```csharp
// Upload user avatar
public class UpdateUserAvatarHandler : IRequestHandler<UpdateAvatarRequest, string>
{
    private readonly IFileStorageService _fileStorage;
    private readonly IUserService _userService;

    public async Task<string> Handle(UpdateAvatarRequest request, CancellationToken cancellationToken)
    {
        // Upload file - auto-organized to Files/Images/ApplicationUser/{filename}
        var filePath = await _fileStorage.UploadAsync<ApplicationUser>(
         request.Avatar, 
         FileType.Image, 
 cancellationToken);

// Update user avatar path in database
        await _userService.UpdateAvatarAsync(request.UserId, filePath);

        // Return accessible URL: https://localhost:7001/Files/Images/ApplicationUser/avatar.jpg
        return $"{_baseUrl}/{filePath}";
    }
}
```

---

## 2. Domain Layer - FileType Enum

### Bước 2.1: FileType Enum

**Làm gì:** Tạo enum để define supported file types và extensions.

**Tại sao:** 
- Type-safe file type checking
- Centralized extension whitelist
- Easy to extend (add Video, Document types...)

**File:** `src/Core/Domain/Common/FileType.cs`

```csharp
using System.ComponentModel;

namespace ECO.WebApi.Domain.Common;

/// <summary>
/// Supported file types với extensions whitelist
/// </summary>
public enum FileType
{
    /// <summary>
    /// Image files: .jpg, .png, .jpeg
    /// </summary>
    [Description(".jpg,.png,.jpeg")]
 Image
}
```

**Giải thích:**
- **Description attribute:** Chứa comma-separated extensions
- **Image:** Chỉ accept .jpg, .png, .jpeg files
- **Extensible:** Có thể thêm Document, Video, Audio... sau này

**Tại sao dùng Description attribute:**
- Lưu metadata ngay trong enum
- Dễ dàng get extensions bằng reflection
- Không cần hardcode extensions ở nhiều nơi

**⚠️ Future extensions:**
```csharp
public enum FileType
{
[Description(".jpg,.png,.jpeg,.gif,.bmp")]
    Image,
    
    [Description(".pdf,.doc,.docx,.xls,.xlsx")]
  Document,
    
    [Description(".mp4,.avi,.mov,.wmv")]
    Video,
    
    [Description(".mp3,.wav,.flac")]
    Audio
}
```

---

## 3. Application Layer - Interfaces & DTOs

### Bước 3.1: FileUploadRequest DTO

**Làm gì:** Tạo DTO cho file upload request với validation.

**Tại sao:** 
- Type-safe request model
- FluentValidation integration
- Base64 data format (standard for API uploads)

**File:** `src/Core/Application/Common/FileStorage/FileUploadRequest.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Common.FileStorage;

/// <summary>
/// File upload request DTO
/// </summary>
public class FileUploadRequest
{
    /// <summary>
  /// File name (without extension)
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
/// File extension (e.g., ".jpg", ".png")
    /// </summary>
    public string Extension { get; set; } = default!;

    /// <summary>
    /// Base64-encoded file data
    /// Format: "data:image/png;base64,iVBORw0KG..."
    /// </summary>
    public string Data { get; set; } = default!;
}

/// <summary>
/// Validator cho FileUploadRequest
/// </summary>
public class FileUploadRequestValidator : AbstractValidator<FileUploadRequest>
{
    public FileUploadRequestValidator()
    {
        RuleFor(p => p.Name)
        .NotEmpty()
                .WithMessage("Image Name cannot be empty!")
            .MaximumLength(150);

      RuleFor(p => p.Extension)
         .NotEmpty()
        .WithMessage("Image Extension cannot be empty!")
            .MaximumLength(5);

        RuleFor(p => p.Data)
    .NotEmpty()
       .WithMessage("Image Data cannot be empty!");
    }
}
```

**Giải thích:**

**Base64 Data Format:**
- Standard format: `data:image/png;base64,{base64_string}`
- Frontend gửi file qua JSON (không multipart/form-data)
- Dễ dàng handle trong API

**Validation rules:**
- **Name:** Required, max 150 characters
- **Extension:** Required, max 5 characters (e.g., ".jpeg")
- **Data:** Required, chứa base64 string

**Tại sao tách Name và Extension:**
- Sanitize filename (remove special characters)
- Add version suffix nếu file exists (-1, -2...)
- Flexible filename handling

---

### Bước 3.2: IFileStorageService Interface

**Làm gì:** Tạo interface cho file storage operations.

**Tại sao:** Abstraction để dễ dàng switch storage providers (Local → S3 → Azure Blob).

**File:** `src/Core/Application/Common/FileStorage/IFileStorageService.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Domain.Common;

namespace ECO.WebApi.Application.Common.FileStorage;

/// <summary>
/// File storage service interface
/// </summary>
public interface IFileStorageService : ITransientService
{
    /// <summary>
    /// Upload file lên storage
    /// </summary>
    /// <typeparam name="T">Entity type (dùng để organize folders)</typeparam>
    /// <param name="request">File upload request</param>
    /// <param name="supportedFileType">Supported file type (Image, Document...)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File path (relative URL)</returns>
    Task<string> UploadAsync<T>(
        FileUploadRequest? request, 
   FileType supportedFileType, 
     CancellationToken cancellationToken = default)
    where T : class;

    /// <summary>
    /// Remove file từ storage
    /// </summary>
    /// <param name="path">File path để xóa</param>
 void Remove(string? path);
}
```

**Giải thích:**

**Generic `<T>` parameter:**
- Dùng để organize folders: `Files/Images/ApplicationUser/`, `Files/Images/Product/`
- Type-safe (compile-time check)
- Auto-create folder structure

**UploadAsync return value:**
- Return relative path: `Files/Images/ApplicationUser/avatar.jpg`
- Dùng để save vào database
- Serve qua static file middleware: `https://localhost:7001/Files/Images/ApplicationUser/avatar.jpg`

**Remove method:**
- Sync method (file deletion is fast)
- Null-safe (check file exists)
- Use khi delete entity hoặc update file

**⚠️ Lưu ý:**
- Interface kế thừa `ITransientService` → auto-register
- Generic constraint `where T : class` → chỉ accept reference types

---

## 4. Infrastructure Layer - Extension Methods

### Bước 4.1: EnumExtensions (GetDescriptionList)

**Làm gì:** Extension method để get extensions từ FileType enum.

**Tại sao:** Validate file extension against whitelist từ enum Description.

**File:** `src/Infrastructure/Infrastructure/Common/Extensions/EnumExtensions.cs`

```csharp
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ECO.WebApi.Infrastructure.Common.Extensions;

/// <summary>
/// Extension methods cho Enum
/// </summary>
public static class EnumExtensions
{
    /// <summary>
/// Get Description attribute value từ enum
    /// </summary>
    public static string GetDescription(this Enum enumValue)
    {
        // Get Description attribute từ enum field
        object[] attr = enumValue.GetType()
        .GetField(enumValue.ToString())!
     .GetCustomAttributes(typeof(DescriptionAttribute), false);

        if (attr.Length > 0)
 return ((DescriptionAttribute)attr[0]).Description;

        // Fallback: convert enum name to readable string
        // Example: "MyEnumValue" → "My Enum Value"
        string result = enumValue.ToString();
 result = Regex.Replace(result, "([a-z])([A-Z])", "$1 $2");
        result = Regex.Replace(result, "([A-Za-z])([0-9])", "$1 $2");
        result = Regex.Replace(result, "([0-9])([A-Za-z])", "$1 $2");
        result = Regex.Replace(result, "(?<!^)(?<! )([A-Z][a-z])", " $1");
 return result;
    }

    /// <summary>
    /// Get Description value as List (split by comma)
 /// </summary>
    public static List<string> GetDescriptionList(this Enum enumValue)
  {
        string result = enumValue.GetDescription();
        return result.Split(',').ToList();
    }
}
```

**Giải thích:**

**GetDescription():**
- Dùng Reflection để get Description attribute
- Fallback to enum name nếu không có Description
- Regex patterns để format enum name thành readable string

**GetDescriptionList():**
- Split Description by comma
- Example: `".jpg,.png,.jpeg"` → `[".jpg", ".png", ".jpeg"]`
- Dùng để validate file extension

**Usage example:**
```csharp
FileType.Image.GetDescriptionList(); 
// Returns: [".jpg", ".png", ".jpeg"]

var extension = ".png";
var allowedExtensions = FileType.Image.GetDescriptionList();
bool isValid = allowedExtensions.Contains(extension.ToLower());
```

**⚠️ Performance:**
- Reflection có cost → cache result nếu gọi nhiều lần
- Trong ECO, chỉ gọi khi validate upload → acceptable

---

### Bước 4.2: RegexExtensions (ReplaceWhitespace)

**Làm gì:** Extension method để replace whitespace trong filename.

**Tại sao:** 
- Filename không nên có spaces (URL encoding issues)
- Replace spaces with hyphens: `"my file.jpg"` → `"my-file.jpg"`

**File:** `src/Infrastructure/Infrastructure/Common/Extensions/RegexExtensions.cs`

```csharp
using System.Text.RegularExpressions;

namespace ECO.WebApi.Infrastructure.Common.Extensions;

/// <summary>
/// Extension methods cho string (Regex operations)
/// </summary>
public static class RegexExtensions
{
    /// <summary>
    /// Regex pattern để match whitespace characters
    /// </summary>
    private static readonly Regex Whitespace = new(@"\s+");

    /// <summary>
    /// Replace tất cả whitespace với replacement string
    /// </summary>
    /// <param name="input">Input string</param>
    /// <param name="replacement">Replacement string (e.g., "-", "_")</param>
    /// <returns>String với whitespace replaced</returns>
    public static string ReplaceWhitespace(this string input, string replacement)
    {
        return Whitespace.Replace(input, replacement);
    }
}
```

**Giải thích:**

**Regex pattern `\s+`:**
- Match bất kỳ whitespace character (space, tab, newline...)
- `+` quantifier: match 1 hoặc nhiều whitespace liên tiếp

**Static Regex instance:**
- Compiled once, reused nhiều lần
- Better performance than `new Regex()` mỗi lần

**Usage example:**
```csharp
string filename = "my file name.jpg";
string sanitized = filename.ReplaceWhitespace("-");
// Result: "my-file-name.jpg"
```

**⚠️ Lưu ý:**
- Chỉ replace whitespace, không remove special characters
- Combine with `RemoveSpecialCharacters()` để sanitize hoàn toàn

---

## 5. Infrastructure Layer - LocalFileStorageService

### Bước 5.1: LocalFileStorageService Implementation

**Làm gì:** Implement IFileStorageService với local disk storage.

**Tại sao:** 
- Simple, no external dependencies
- Perfect cho development và small applications
- Easy to understand và debug

**File:** `src/Infrastructure/Infrastructure/FileStorage/LocalFileStorageService.cs`

```csharp
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ECO.WebApi.Application.Common.FileStorage;
using ECO.WebApi.Domain.Common;
using ECO.WebApi.Infrastructure.Common.Extensions;

namespace ECO.WebApi.Infrastructure.FileStorage;

/// <summary>
/// Local file storage service (disk-based)
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    /// <summary>
    /// Upload file lên local disk
    /// </summary>
    public async Task<string> UploadAsync<T>(
        FileUploadRequest? request, 
        FileType supportedFileType, 
        CancellationToken cancellationToken = default)
        where T : class
    {
        // Null check
        if (request == null || request.Data == null)
        {
      return string.Empty;
        }

      // Validate extension
     if (request.Extension is null || 
       !supportedFileType.GetDescriptionList().Contains(request.Extension.ToLower()))
        {
        throw new InvalidOperationException("File Format Not Supported.");
        }

        // Validate name
        if (request.Name is null)
        {
    throw new InvalidOperationException("Name is required.");
        }

        // Extract base64 data từ data URL
        // Format: "data:image/png;base64,iVBORw0KG..."
  string base64Data = Regex.Match(
       request.Data, 
       "data:image/(?<type>.+?),(?<data>.+)")
    .Groups["data"].Value;

   // Convert base64 string to MemoryStream
        var streamData = new MemoryStream(Convert.FromBase64String(base64Data));

        if (streamData.Length > 0)
        {
       // Get folder name từ entity type
  // Example: ApplicationUser → "ApplicationUser" folder
    string folder = typeof(T).Name;

  // macOS path fix (use forward slash)
       if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
                folder = folder.Replace(@"\", "/");
  }

     // Build folder path based on file type
         // Image: Files/Images/{EntityName}
 // Others: Files/Others/{EntityName}
       string folderName = supportedFileType switch
            {
              FileType.Image => Path.Combine("Files", "Images", folder),
                _ => Path.Combine("Files", "Others", folder),
            };

      // Full path: {CurrentDirectory}/Files/Images/ApplicationUser
       string pathToSave = Path.Combine(Directory.GetCurrentDirectory(), folderName);

    // Create directory nếu chưa tồn tại
            Directory.CreateDirectory(pathToSave);

     // Sanitize filename
            string fileName = request.Name.Trim('"'); // Remove quotes
            fileName = RemoveSpecialCharacters(fileName); // Remove special chars
            fileName = fileName.ReplaceWhitespace("-"); // Replace spaces with hyphen
          fileName += request.Extension.Trim(); // Add extension

     // Full file path
      string fullPath = Path.Combine(pathToSave, fileName);

     // Database path (relative)
 string dbPath = Path.Combine(folderName, fileName);

       // Handle duplicate filenames (add -1, -2, -3...)
      if (File.Exists(dbPath))
    {
      dbPath = NextAvailableFilename(dbPath);
     fullPath = NextAvailableFilename(fullPath);
        }

            // Save file to disk
     using var stream = new FileStream(fullPath, FileMode.Create);
            await streamData.CopyToAsync(stream, cancellationToken);

        // Return relative path với forward slashes (URL format)
        return dbPath.Replace("\\", "/");
        }
        else
     {
         return string.Empty;
        }
    }

    /// <summary>
    /// Remove special characters từ filename (chỉ giữ alphanumeric, underscore, dot)
    /// </summary>
    public static string RemoveSpecialCharacters(string str)
    {
  return Regex.Replace(
          str, 
       "[^a-zA-Z0-9_.]+", 
  string.Empty, 
      RegexOptions.Compiled);
}

    /// <summary>
    /// Remove file từ disk
    /// </summary>
    public void Remove(string? path)
    {
if (File.Exists(path))
        {
            File.Delete(path);
   }
    }

    // ===== Helper Methods: Duplicate Filename Handling =====

    private const string NumberPattern = "-{0}";

    /// <summary>
    /// Get next available filename nếu file đã tồn tại
    /// Example: file.jpg → file-1.jpg → file-2.jpg
    /// </summary>
    private static string NextAvailableFilename(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        if (Path.HasExtension(path))
    {
        // Insert "-{number}" before extension
      // "file.jpg" → "file-{number}.jpg"
     return GetNextFilename(
                path.Insert(
    path.LastIndexOf(Path.GetExtension(path), StringComparison.Ordinal), 
        NumberPattern));
        }

  // No extension: append "-{number}"
        return GetNextFilename(path + NumberPattern);
    }

    /// <summary>
    /// Binary search để tìm next available number
    /// Efficient cho nhiều duplicate files
    /// </summary>
    private static string GetNextFilename(string pattern)
    {
 // Try với số 1 trước
        string tmp = string.Format(pattern, 1);
        if (!File.Exists(tmp))
        {
            return tmp;
        }

  // Binary search để tìm gap
   int min = 1, max = 2;

        // Tìm upper bound (double until not exists)
 while (File.Exists(string.Format(pattern, max)))
        {
            min = max;
max *= 2;
        }

        // Binary search trong range [min, max]
    while (max != min + 1)
   {
  int pivot = (max + min) / 2;
    if (File.Exists(string.Format(pattern, pivot)))
       {
min = pivot;
            }
          else
            {
       max = pivot;
            }
 }

        return string.Format(pattern, max);
    }
}
```

**Giải thích:**

**UploadAsync Flow:**
1. **Validate:** Extension, Name
2. **Extract base64:** Regex match data URL pattern
3. **Organize folder:** `Files/{FileType}/{EntityName}/`
4. **Sanitize filename:** Remove special chars, replace spaces
5. **Handle duplicates:** Add suffix (-1, -2...) nếu file exists
6. **Save to disk:** FileStream write
7. **Return path:** Relative URL path

**Folder Structure:**
```
Files/
├── Images/
│   ├── ApplicationUser/
│   │   ├── avatar.jpg
│   │ └── avatar-1.jpg
│   ├── Product/
│   │   ├── product-image.png
│ │   └── product-image-1.png
│   └── Category/
│       └── category-icon.jpg
└── Others/
    └── Document/
        └── file.pdf
```

**Filename Sanitization:**
- Input: `"My File (2023).jpg"`
- After RemoveSpecialCharacters: `"MyFile2023.jpg"`
- After ReplaceWhitespace: `"MyFile2023.jpg"` (no spaces to replace)
- Final: `"MyFile2023.jpg"`

**Duplicate Handling (Binary Search):**
- **Why binary search:** Efficient khi có nhiều duplicates (1000+ files)
- **Time complexity:** O(log n) thay vì O(n)
- **Example:** `file.jpg` → `file-1.jpg` → `file-2.jpg` → ...

**macOS Path Fix:**
- macOS dùng forward slash `/` instead of backslash `\`
- `RuntimeInformation.IsOSPlatform()` check OS type

**⚠️ Security Considerations:**
- Validate file extension (whitelist only)
- Remove special characters (prevent path traversal)
- No directory traversal (`.`, `..`)
- Size limit check (TODO: add in future)

---

## 6. Infrastructure Layer - Static File Middleware

### Bước 6.1: Startup Configuration

**Làm gì:** Configure static file middleware để serve uploaded files.

**Tại sao:** 
- Serve files qua HTTP (không cần controller)
- Better performance (IIS/Kestrel optimization)
- Standard ASP.NET Core pattern

**File:** `src/Infrastructure/Infrastructure/FileStorage/Startup.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace ECO.WebApi.Infrastructure.FileStorage;

/// <summary>
/// File storage startup configuration
/// </summary>
internal static class Startup
{
    /// <summary>
    /// Configure static file middleware
    /// </summary>
    internal static IApplicationBuilder UseFileStorage(this IApplicationBuilder app)
    {
 // Files directory path
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Files");

        // Create directory nếu chưa tồn tại
     if (!Directory.Exists(filePath))
     {
            Directory.CreateDirectory(filePath);
        }

        // Configure static file middleware
        app.UseStaticFiles(new StaticFileOptions()
        {
   FileProvider = new PhysicalFileProvider(filePath),
RequestPath = new PathString("/Files")
        });

        return app;
    }
}
```

**Giải thích:**

**PhysicalFileProvider:**
- Map physical folder `{CurrentDirectory}/Files` to URL `/Files`
- Serve files directly from disk

**RequestPath `/Files`:**
- URL pattern: `https://localhost:7001/Files/{path}`
- Example: `/Files/Images/ApplicationUser/avatar.jpg`

**Auto-create directory:**
- Ensure `Files/` folder exists khi startup
- Prevent FileNotFoundException

**⚠️ Lưu ý:**
- Static files served WITHOUT going through MVC pipeline (faster)
- No authorization check (files are public)
- For private files → Use controller with authorization

---

### Bước 6.2: Register trong Infrastructure Startup

**Làm gì:** Call `UseFileStorage()` trong main Infrastructure startup.

**Tại sao:** Modular startup pattern - clean separation.

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

Đảm bảo có dòng này trong `AddInfrastructure()`:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    // ... existing services ...

    // File Storage Service (auto-registered via ITransientService)
    // LocalFileStorageService will be auto-discovered

    // ... existing services ...

    return services;
}
```

**⚠️ Lưu ý:**
- `LocalFileStorageService` implement `IFileStorageService : ITransientService`
- Auto-register qua Service Registration pattern (BUILD_08)
- Không cần manual register

---

### Bước 6.3: Configure trong Program.cs

**Làm gì:** Add `UseFileStorage()` middleware vào pipeline.

**Tại sao:** Enable static file serving.

**File:** `src/Host/Host/Program.cs`

Thêm middleware sau `app.UseStaticFiles()`:

```csharp
// Static Files
app.UseStaticFiles();

// File Storage (uploaded files)
app.UseFileStorage();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();
```

**⚠️ Order quan trọng:**
- Phải đặt TRƯỚC `UseAuthentication()` (static files không cần auth)
- Sau `UseStaticFiles()` (wwwroot files first)

---

## 7. Usage Examples

### Bước 7.1: Upload User Avatar

**Scenario:** User upload avatar trong profile update.

**Request DTO:**
```csharp
namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Update user avatar request
/// </summary>
public class UpdateUserAvatarRequest : IRequest<string>
{
    public string UserId { get; set; } = default!;
    public FileUploadRequest Avatar { get; set; } = default!;
}
```

**Handler:**
```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.FileStorage;
using ECO.WebApi.Domain.Common;
using ECO.WebApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Handler update user avatar
/// </summary>
public class UpdateUserAvatarHandler : IRequestHandler<UpdateUserAvatarRequest, string>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorage;
    private readonly string _baseUrl;

    public UpdateUserAvatarHandler(
        UserManager<ApplicationUser> userManager,
     IFileStorageService fileStorage,
        IConfiguration configuration)
    {
        _userManager = userManager;
      _fileStorage = fileStorage;
        _baseUrl = configuration["AppUrl"] ?? "https://localhost:7001";
    }

    public async Task<string> Handle(
 UpdateUserAvatarRequest request, 
    CancellationToken cancellationToken)
    {
        // Get user
    var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException("User not found");

     // Remove old avatar nếu có
        if (!string.IsNullOrEmpty(user.ImageUrl))
        {
            _fileStorage.Remove(user.ImageUrl);
        }

        // Upload new avatar
        // File sẽ lưu tại: Files/Images/ApplicationUser/{filename}
        var filePath = await _fileStorage.UploadAsync<ApplicationUser>(
   request.Avatar,
  FileType.Image,
      cancellationToken);

   // Update user
    user.ImageUrl = filePath;
        await _userManager.UpdateAsync(user);

        // Return full URL
        return $"{_baseUrl}/{filePath}";
    }
}
```

**Controller:**
```csharp
using ECO.WebApi.Application.Identity.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers.Identity;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
  private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Update user avatar
    /// </summary>
    [HttpPut("{userId}/avatar")]
    public async Task<ActionResult<string>> UpdateAvatar(
        string userId,
        [FromBody] FileUploadRequest avatar)
    {
        var request = new UpdateUserAvatarRequest
   {
     UserId = userId,
  Avatar = avatar
        };

        var avatarUrl = await _mediator.Send(request);
        return Ok(new { avatarUrl });
    }
}
```

**API Call:**
```bash
curl -X PUT https://localhost:7001/api/users/550e8400-e29b-41d4-a716-446655440000/avatar \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "name": "avatar",
  "extension": ".jpg",
  "data": "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAYABgAAD..."
  }'
```

**Response:**
```json
{
  "avatarUrl": "https://localhost:7001/Files/Images/ApplicationUser/avatar.jpg"
}
```

**Giải thích:**
- Upload file → Get relative path: `Files/Images/ApplicationUser/avatar.jpg`
- Save path to database (user.ImageUrl)
- Return full URL to client
- Client display image: `<img src="{avatarUrl}" />`

---

### Bước 7.2: Upload Product Image

**Scenario:** Admin upload product image.

**Request DTO:**
```csharp
namespace ECO.WebApi.Application.Catalog.Products;

public class CreateProductRequest : IRequest<Guid>
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public FileUploadRequest? Image { get; set; }
}
```

**Handler:**
```csharp
using ECO.WebApi.Application.Common.FileStorage;
using ECO.WebApi.Domain.Catalog;
using ECO.WebApi.Domain.Common;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

public class CreateProductHandler : IRequestHandler<CreateProductRequest, Guid>
{
    private readonly IRepository<Product> _repository;
    private readonly IFileStorageService _fileStorage;

    public CreateProductHandler(
        IRepository<Product> repository,
      IFileStorageService fileStorage)
    {
 _repository = repository;
     _fileStorage = fileStorage;
    }

    public async Task<Guid> Handle(
   CreateProductRequest request, 
  CancellationToken cancellationToken)
    {
        // Upload product image nếu có
        string? imagePath = null;
        if (request.Image is not null)
        {
     // File sẽ lưu tại: Files/Images/Product/{filename}
            imagePath = await _fileStorage.UploadAsync<Product>(
          request.Image,
              FileType.Image,
         cancellationToken);
    }

        // Create product
var product = Product.Create(
 request.Name,
            request.Description,
         request.Price,
    imagePath);

    // Save to database
        await _repository.AddAsync(product, cancellationToken);

        return product.Id;
    }
}
```

**Controller:**
```csharp
[HttpPost]
[MustHavePermission("Products.Create")]
public async Task<ActionResult<Guid>> CreateProduct([FromBody] CreateProductRequest request)
{
    var productId = await _mediator.Send(request);
    return CreatedAtAction(nameof(GetProduct), new { id = productId }, productId);
}
```

**API Call:**
```bash
curl -X POST https://localhost:7001/api/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "name": "iPhone 15 Pro",
    "description": "Latest iPhone model",
    "price": 999.99,
    "image": {
      "name": "iphone-15-pro",
      "extension": ".png",
      "data": "data:image/png;base64,iVBORw0KG..."
    }
  }'
```

**Response:**
```json
{
  "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

### Bước 7.3: Delete Product với Image Cleanup

**Scenario:** Delete product và cleanup image file.

**Handler:**
```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.FileStorage;
using ECO.WebApi.Domain.Catalog;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

public class DeleteProductRequest : IRequest<string>
{
    public Guid ProductId { get; set; }
}

public class DeleteProductHandler : IRequestHandler<DeleteProductRequest, string>
{
    private readonly IRepository<Product> _repository;
    private readonly IFileStorageService _fileStorage;

    public DeleteProductHandler(
        IRepository<Product> repository,
  IFileStorageService fileStorage)
    {
        _repository = repository;
        _fileStorage = fileStorage;
    }

    public async Task<string> Handle(
        DeleteProductRequest request, 
        CancellationToken cancellationToken)
    {
        // Get product
    var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found");

        // Delete image file nếu có
 if (!string.IsNullOrEmpty(product.ImageUrl))
  {
     _fileStorage.Remove(product.ImageUrl);
        }

        // Delete product
   await _repository.DeleteAsync(product, cancellationToken);

        return "Product deleted successfully";
    }
}
```

**Controller:**
```csharp
[HttpDelete("{id}")]
[MustHavePermission("Products.Delete")]
public async Task<ActionResult<string>> DeleteProduct(Guid id)
{
    var result = await _mediator.Send(new DeleteProductRequest { ProductId = id });
    return Ok(result);
}
```

**⚠️ Important:**
- LUÔN xóa file trước khi delete entity
- Nếu xóa entity trước → orphan files
- Consider background job để cleanup orphan files định kỳ

---

### Bước 7.4: Get File URL Helper

**Scenario:** Helper method để generate full URL từ relative path.

**Extension Method:**
```csharp
namespace ECO.WebApi.Application.Common.Extensions;

public static class FilePathExtensions
{
    /// <summary>
    /// Convert relative file path to full URL
    /// </summary>
    public static string? ToFullUrl(this string? relativePath, string baseUrl)
    {
        if (string.IsNullOrEmpty(relativePath))
          return null;

        // Ensure forward slashes
        relativePath = relativePath.Replace("\\", "/");

        // Build full URL
        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
 }
}
```

**Usage trong DTOs:**
```csharp
namespace ECO.WebApi.Application.Catalog.Products;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    
    // Relative path (from database)
    public string? ImagePath { get; set; }
    
    // Full URL (computed)
    public string? ImageUrl => ImagePath?.ToFullUrl("https://localhost:7001");
}
```

**Mapping:**
```csharp
var product = await _repository.GetByIdAsync(id);
var dto = product.Adapt<ProductDto>();

// dto.ImagePath = "Files/Images/Product/iphone-15-pro.png"
// dto.ImageUrl = "https://localhost:7001/Files/Images/Product/iphone-15-pro.png"
```

---

### Bước 7.5: Frontend Integration (React Example)

**Upload Component:**
```typescript
// FileUpload.tsx
import React, { useState } from 'react';

interface FileUploadProps {
  onUpload: (file: FileUploadRequest) => void;
}

interface FileUploadRequest {
  name: string;
  extension: string;
  data: string;
}

export const FileUpload: React.FC<FileUploadProps> = ({ onUpload }) => {
  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Convert file to base64
    const reader = new FileReader();
    reader.onloadend = () => {
      const base64String = reader.result as string;
      
    // Extract filename and extension
      const fileName = file.name.substring(0, file.name.lastIndexOf('.'));
      const extension = file.name.substring(file.name.lastIndexOf('.'));

      // Create upload request
      const uploadRequest: FileUploadRequest = {
        name: fileName,
        extension: extension,
        data: base64String
   };

      onUpload(uploadRequest);
    };

    reader.readAsDataURL(file);
  };

  return (
    <div>
  <input 
        type="file" 
        accept=".jpg,.jpeg,.png"
        onChange={handleFileChange} 
      />
    </div>
  );
};
```

**API Call:**
```typescript
// productService.ts
export const createProduct = async (data: CreateProductRequest) => {
  const response = await fetch('https://localhost:7001/api/products', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify(data)
  });

  return response.json();
};
```

---

## 8. Best Practices & Guidelines

### 8.1: File Size Limits

**Problem:** No size limit → disk full attacks.

**Solution:** Add size validation trong validator.

```csharp
public class FileUploadRequestValidator : AbstractValidator<FileUploadRequest>
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public FileUploadRequestValidator()
    {
        // ... existing rules ...

        RuleFor(p => p.Data)
  .Must(BeValidSize)
 .WithMessage($"File size must not exceed {MaxFileSizeBytes / 1024 / 1024}MB");
    }

    private bool BeValidSize(string base64Data)
    {
      if (string.IsNullOrEmpty(base64Data))
     return false;

        // Extract base64 string (remove data URL prefix)
    var match = Regex.Match(base64Data, "data:image/(?<type>.+?),(?<data>.+)");
     var base64 = match.Groups["data"].Value;

        // Calculate size
        var bytes = Convert.FromBase64String(base64);
        return bytes.Length <= MaxFileSizeBytes;
    }
}
```

---

### 8.2: File Type Validation (Content-Type)

**Problem:** User upload .exe renamed to .jpg.

**Solution:** Validate file content, not just extension.

```csharp
public static class FileValidator
{
    private static readonly Dictionary<string, byte[]> FileSignatures = new()
    {
        { ".jpg", new byte[] { 0xFF, 0xD8, 0xFF } }, // JPEG
        { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } }, // PNG
        { ".gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } }, // GIF
    };

    public static bool IsValidImage(string extension, byte[] fileData)
    {
if (!FileSignatures.TryGetValue(extension.ToLower(), out var signature))
         return false;

    // Check file header (magic bytes)
 return fileData.Take(signature.Length).SequenceEqual(signature);
    }
}
```

**Usage trong service:**
```csharp
// After convert base64 to bytes
var bytes = Convert.FromBase64String(base64Data);

if (!FileValidator.IsValidImage(request.Extension, bytes))
{
    throw new InvalidOperationException("Invalid image file");
}
```

---

### 8.3: Thumbnail Generation

**Problem:** Large images slow down page load.

**Solution:** Generate thumbnails on upload.

```csharp
// Add package: SixLabors.ImageSharp
// <PackageReference Include="SixLabors.ImageSharp" Version="3.1.0" />

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

public async Task<(string originalPath, string thumbnailPath)> UploadWithThumbnailAsync<T>(
  FileUploadRequest request,
    FileType supportedFileType,
    CancellationToken cancellationToken = default)
    where T : class
{
    // Upload original
    var originalPath = await UploadAsync<T>(request, supportedFileType, cancellationToken);

    // Generate thumbnail
    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), originalPath);
    using var image = await Image.LoadAsync(fullPath, cancellationToken);

    // Resize to 200x200
    image.Mutate(x => x.Resize(new ResizeOptions
    {
  Size = new Size(200, 200),
        Mode = ResizeMode.Max
    }));

    // Save thumbnail
    var thumbnailPath = originalPath.Replace(
   Path.GetFileName(originalPath),
 $"thumb_{Path.GetFileName(originalPath)}");

    var thumbnailFullPath = Path.Combine(Directory.GetCurrentDirectory(), thumbnailPath);
    await image.SaveAsync(thumbnailFullPath, cancellationToken);

    return (originalPath, thumbnailPath);
}
```

---

### 8.4: CDN Integration

**Problem:** Static files serve từ API server → slow, resource-intensive.

**Solution:** Upload files to CDN (Azure Blob, AWS S3, Cloudflare R2).

**Abstract interface:**
```csharp
public interface IFileStorageService
{
    Task<string> UploadAsync<T>(...);
    void Remove(string? path);
    
    // New: Get public URL
  string GetPublicUrl(string path);
}
```

**LocalFileStorageService:**
```csharp
public string GetPublicUrl(string path)
{
    return $"{_baseUrl}/{path}";
}
```

**AzureBlobStorageService:**
```csharp
public string GetPublicUrl(string path)
{
    // Azure Blob Storage URL
    return $"https://{_accountName}.blob.core.windows.net/{_containerName}/{path}";
}
```

---

### 8.5: Cleanup Orphan Files

**Problem:** Files remain khi delete entity fails or app crash.

**Solution:** Background job to cleanup orphan files.

```csharp
using Hangfire;

public class FileCleanupJob
{
    private readonly ApplicationDbContext _db;

    [AutomaticRetry(Attempts = 3)]
    public async Task CleanupOrphanFiles()
 {
        // Get all file paths từ database
        var userAvatars = await _db.Users
      .Where(u => !string.IsNullOrEmpty(u.ImageUrl))
     .Select(u => u.ImageUrl)
            .ToListAsync();

        var productImages = await _db.Products
            .Where(p => !string.IsNullOrEmpty(p.ImageUrl))
            .Select(p => p.ImageUrl)
            .ToListAsync();

   var allDbFiles = userAvatars.Concat(productImages).ToHashSet();

        // Scan Files directory
  var filesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Files");
        var allDiskFiles = Directory.GetFiles(filesDirectory, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Directory.GetCurrentDirectory(), f))
          .ToList();

        // Find orphan files (on disk but not in DB)
        var orphanFiles = allDiskFiles.Except(allDbFiles).ToList();

        // Delete orphan files
        foreach (var orphan in orphanFiles)
        {
       File.Delete(orphan);
        }
    }

    public static void Schedule()
    {
        // Run every day at 2 AM
 RecurringJob.AddOrUpdate<FileCleanupJob>(
            "cleanup-orphan-files",
            job => job.CleanupOrphanFiles(),
            "0 2 * * *"); // Cron: 2:00 AM daily
    }
}
```

---

### 8.6: Folder Structure Best Practices

**Recommended structure:**
```
Files/
├── Images/
│   ├── ApplicationUser/
│   │   ├── {userId}-avatar.jpg
│   │   └── {userId}-avatar-thumb.jpg
│   ├── Product/
│   │   ├── {productId}-main.png
│   │   ├── {productId}-main-thumb.png
│   │   ├── {productId}-gallery-1.png
│   │   └── {productId}-gallery-2.png
│   └── Category/
│       └── {categoryId}-icon.png
├── Documents/
│   ├── Invoice/
│   │   └── invoice-{invoiceId}.pdf
│   └── Report/
│       └── report-{date}.pdf
└── Temp/
    └── {sessionId}-upload.tmp
```

**Benefits:**
- Easy to find files by entity
- Easy to cleanup (delete folder when delete entity)
- Separate temp files from permanent files

---

## 9. Testing

### 9.1: Unit Test - RemoveSpecialCharacters

```csharp
using ECO.WebApi.Infrastructure.FileStorage;
using Xunit;

namespace ECO.WebApi.Infrastructure.Tests.FileStorage;

public class LocalFileStorageServiceTests
{
    [Theory]
    [InlineData("my-file", "my-file")]
    [InlineData("my file", "myfile")]
    [InlineData("my@file#123", "myfile123")]
    [InlineData("こんにちは", "")]
    [InlineData("file_name.jpg", "file_name.jpg")]
    public void RemoveSpecialCharacters_ShouldWork(string input, string expected)
{
      // Act
        var result = LocalFileStorageService.RemoveSpecialCharacters(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
```

---

### 9.2: Integration Test - Upload File

```csharp
using ECO.WebApi.Application.Common.FileStorage;
using ECO.WebApi.Domain.Common;
using ECO.WebApi.Domain.Identity;
using ECO.WebApi.Infrastructure.FileStorage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECO.WebApi.Infrastructure.Tests.FileStorage;

public class FileUploadIntegrationTests : IDisposable
{
private readonly LocalFileStorageService _service;
private readonly string _testFilesPath;

    public FileUploadIntegrationTests()
    {
        var mockLogger = new Mock<ILogger<LocalFileStorageService>>();
 _service = new LocalFileStorageService();

        _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "Files");
    }

    [Fact]
    public async Task UploadAsync_ShouldCreateFile()
    {
        // Arrange
        var request = new FileUploadRequest
      {
            Name = "test-avatar",
         Extension = ".jpg",
   Data = "data:image/jpeg;base64,/9j/4AAQSkZJRg..." // Valid base64
        };

      // Act
      var result = await _service.UploadAsync<ApplicationUser>(
            request,
    FileType.Image,
      CancellationToken.None);

        // Assert
     Assert.NotEmpty(result);
  Assert.Contains("Files/Images/ApplicationUser", result);

   // Verify file exists
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), result);
        Assert.True(File.Exists(fullPath));
    }

    [Fact]
    public async Task UploadAsync_DuplicateFile_ShouldAddSuffix()
    {
        // Arrange
        var request = new FileUploadRequest
      {
            Name = "duplicate",
            Extension = ".jpg",
            Data = "data:image/jpeg;base64,/9j/4AAQSkZJRg..."
        };

        // Act - Upload same file twice
     var result1 = await _service.UploadAsync<ApplicationUser>(request, FileType.Image);
      var result2 = await _service.UploadAsync<ApplicationUser>(request, FileType.Image);

        // Assert
        Assert.NotEqual(result1, result2);
     Assert.Contains("-1", result2); // Should have suffix
    }

    public void Dispose()
    {
        // Cleanup test files
  if (Directory.Exists(_testFilesPath))
     {
            Directory.Delete(_testFilesPath, true);
   }
    }
}
```

---

## 10. Troubleshooting

### Problem 1: File Not Found (404)

**Error:** `GET /Files/Images/User/avatar.jpg` returns 404.

**Solutions:**
```bash
# 1. Check file exists
ls Files/Images/User/avatar.jpg

# 2. Check path format (forward slashes)
# Wrong: Files\Images\User\avatar.jpg
# Correct: Files/Images/User/avatar.jpg

# 3. Check static file middleware order in Program.cs
# Should be: UseStaticFiles() -> UseFileStorage() -> UseAuthentication()

# 4. Check file permissions (Linux/macOS)
chmod 644 Files/Images/User/avatar.jpg
```

---

### Problem 2: Base64 Decode Error

**Error:** `FormatException: The input is not a valid Base-64 string`

**Solutions:**
```csharp
// Check data format
var dataUrl = "data:image/jpeg;base64,/9j/4AAQ...";

// Extract base64 part
var match = Regex.Match(dataUrl, "data:image/(?<type>.+?),(?<data>.+)");
var base64 = match.Groups["data"].Value;

// Validate base64
try
{
var bytes = Convert.FromBase64String(base64);
}
catch (FormatException ex)
{
    throw new InvalidOperationException("Invalid base64 data", ex);
}
```

---

### Problem 3: Permission Denied

**Error:** `UnauthorizedAccessException: Access to the path is denied`

**Solutions:**
```bash
# Windows: Grant permissions to IIS AppPool user
icacls "C:\path\to\Files" /grant "IIS AppPool\DefaultAppPool:(OI)(CI)F" /T

# Linux/macOS: Grant permissions to www-data user
chown -R www-data:www-data /var/www/app/Files
chmod -R 755 /var/www/app/Files
```

---

### Problem 4: Large File Upload Fails

**Error:** Request body too large.

**Solutions:**
```csharp
// Program.cs - Increase request size limit
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
});

builder.WebHost.ConfigureKestrel(options =>
{
  options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});
```

---

### Problem 5: Special Characters in Filename

**Error:** File saved với tên invalid.

**Solutions:**
```csharp
// Use RemoveSpecialCharacters
var sanitized = LocalFileStorageService.RemoveSpecialCharacters(fileName);

// Replace whitespace
sanitized = sanitized.ReplaceWhitespace("-");

// Result: "my file (2023).jpg" → "myfile2023.jpg"
```

---

## 11. Summary

### ✅ Đã hoàn thành trong bước này:

**Domain Layer:**
- ✅ `FileType` enum với extensions whitelist

**Application Layer:**
- ✅ `IFileStorageService` interface
- ✅ `FileUploadRequest` DTO với FluentValidation

**Infrastructure Layer:**
- ✅ `LocalFileStorageService` implementation
- ✅ `EnumExtensions` (GetDescriptionList)
- ✅ `RegexExtensions` (ReplaceWhitespace)
- ✅ Static file middleware configuration

**Features:**
- ✅ Upload files với base64 format
- ✅ Auto-organize files by entity type
- ✅ Filename sanitization (remove special chars, replace spaces)
- ✅ Duplicate filename handling (auto-suffix)
- ✅ File validation (extension whitelist)
- ✅ File deletion
- ✅ Static file serving

---

### 📊 File Storage Architecture:

```
Application Layer (Abstraction)
    │
    ├── IFileStorageService (interface)
    │   ├── UploadAsync<T>(request, fileType)
    │   └── Remove(path)
    │
    └── FileUploadRequest (DTO)
      ├── Name
        ├── Extension
  └── Data (base64)

Infrastructure Layer (Implementation)
  │
    ├── LocalFileStorageService
    │   ├── Upload to disk
    │   ├── Organize by entity type
    │   ├── Sanitize filename
    │   ├── Handle duplicates
    │   └── Remove files
    │
    └── Static File Middleware
        ├── Serve files from /Files
        └── No authentication required

File Organization
  │
    Files/
    ├── Images/
    │   ├── ApplicationUser/
    │   ├── Product/
    │   └── Category/
    └── Others/
        └── Document/
```

---

### 📌 Key Concepts:

**File Upload Flow:**
1. Client sends base64 data
2. Validate extension whitelist
3. Convert base64 to bytes
4. Organize folder by entity type
5. Sanitize filename
6. Handle duplicates (add suffix)
7. Save to disk
8. Return relative path

**Folder Organization:**
- Generic `<T>` parameter determines folder
- `Files/{FileType}/{EntityName}/`
- Example: `Files/Images/ApplicationUser/avatar.jpg`

**Filename Sanitization:**
- Remove special characters: `[^a-zA-Z0-9_.]+`
- Replace whitespace with hyphen: `-`
- Result: `"My File (2023).jpg"` → `"MyFile2023.jpg"`

**Duplicate Handling:**
- Binary search algorithm (O(log n))
- Add suffix: `file.jpg` → `file-1.jpg` → `file-2.jpg`

**Static File Serving:**
- PhysicalFileProvider maps `/Files` → `{CurrentDirectory}/Files`
- No MVC pipeline overhead
- Fast serving

---

### 📁 File Structure:

```
src/Core/Domain/Common/
└── FileType.cs

src/Core/Application/Common/FileStorage/
├── IFileStorageService.cs
└── FileUploadRequest.cs

src/Infrastructure/Infrastructure/Common/Extensions/
├── EnumExtensions.cs
└── RegexExtensions.cs

src/Infrastructure/Infrastructure/FileStorage/
├── LocalFileStorageService.cs
└── Startup.cs

Files/ (runtime)
├── Images/
│   ├── ApplicationUser/
│   ├── Product/
│   └── Category/
└── Others/
    └── Document/
```

---

## 12. Next Steps

**Tiếp theo:** [BUILD_21 - Email Service](BUILD_21_Email_Service.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo `IMailService` interface
2. ✅ Implement SMTP mail service (MailKit)
3. ✅ Tạo `IEmailTemplateService` interface
4. ✅ Implement email template service (Razor templates)
5. ✅ Email templates (Welcome, Password Reset, Notifications)
6. ✅ SMTP configuration và settings

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
