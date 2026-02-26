# Blob Storage Integration - Azure Blob Storage

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 23 (Email Service) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Blob Storage Service với Azure Blob Storage cho việc lưu trữ files lớn (images, videos, documents).

---

## 1. Overview

**Làm gì:** Xây dựng abstraction cho Blob Storage với implementation cho Azure Blob Storage.

**Tại sao cần:**
- **Scalability:** Lưu trữ files lớn trên cloud storage thay vì local disk
- **Reliability:** Azure có high availability và disaster recovery
- **CDN Integration:** Dễ dàng tích hợp CDN để serve files nhanh hơn
- **Cost Effective:** Pay-per-use, không cần maintain servers
- **Flexibility:** Abstraction cho phép switch providers trong tương lai nếu cần

**Trong bước này chúng ta sẽ:**
- ✅ Tạo IBlobStorageService interface
- ✅ Implement AzureBlobStorageService
- ✅ Tạo BlobStorageSettings configuration
- ✅ Setup container management
- ✅ Implement upload/download/delete operations
- ✅ Tạo BlobStorageController cho testing

**Real-world example:**
```csharp
// Upload product image to blob storage
var request = new UploadBlobRequest
{
    ContainerName = "products",
    BlobName = $"product-{productId}.jpg",
    ContentType = "image/jpeg",
    Data = imageStream
};

var url = await _blobStorage.UploadAsync(request);
// url: https://mystore.blob.core.windows.net/products/product-123.jpg

// Save URL to database
product.ImageUrl = url;
await _repository.UpdateAsync(product);
```

---

## 2. Add Required Packages

### Bước 2.1: Add Azure Blob Storage Package

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Azure Blob Storage SDK -->
    <PackageReference Include="Azure.Storage.Blobs" Version="12.21.2" />
</ItemGroup>
```

**Giải thích packages:**
- `Azure.Storage.Blobs`: Official Azure Blob Storage client library

**⚠️ Lưu ý:**
- Version 12.x của Azure.Storage.Blobs là latest stable
- SDK sử dụng modern Azure identity và supports async/await

---

## 3. Application Layer - Interfaces và DTOs

### Bước 3.1: Tạo IBlobStorageService Interface

**Làm gì:** Định nghĩa contract cho blob storage operations.

**Tại sao:** Abstraction để decouple business logic khỏi Azure-specific implementations.

**File:** `src/Core/Application/Common/BlobStorage/IBlobStorageService.cs`

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ECO.WebApi.Application.Common.BlobStorage;

/// <summary>
/// Service for blob storage operations
/// </summary>
public interface IBlobStorageService : ITransientService
{
    /// <summary>
    /// Upload blob to storage
    /// </summary>
    Task<string> UploadAsync(UploadBlobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download blob from storage
    /// </summary>
    Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete blob from storage
    /// </summary>
    Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if blob exists
    /// </summary>
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get blob URL (public or SAS token)
    /// </summary>
    Task<string> GetBlobUrlAsync(string containerName, string blobName, int expiryMinutes = 60, CancellationToken cancellationToken = default);

    /// <summary>
    /// List blobs in container
    /// </summary>
    Task<List<BlobModel>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create container if not exists
    /// </summary>
    Task CreateContainerAsync(string containerName, bool isPublic = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete container
    /// </summary>
    Task DeleteContainerAsync(string containerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all containers
    /// </summary>
  Task<List<string>> ListContainersAsync(CancellationToken cancellationToken = default);
}
```

**Giải thích:**
- **UploadAsync:** Upload file và return public URL hoặc blob identifier
- **DownloadAsync:** Download blob as Stream (memory efficient)
- **GetBlobUrlAsync:** Generate SAS token URL cho private blobs
- **ListBlobsAsync:** Query blobs với prefix filter (e.g., "products/")
- **CreateContainerAsync:** Auto-create container nếu chưa tồn tại

**Tại sao return Stream thay vì byte[]:**
- Memory efficient cho large files
- Có thể stream trực tiếp to response
- Không load toàn bộ file vào memory

---

### Bước 3.2: Tạo DTOs

**File:** `src/Core/Application/Common/BlobStorage/UploadBlobRequest.cs`

```csharp
using System.IO;

namespace ECO.WebApi.Application.Common.BlobStorage;

/// <summary>
/// Request to upload blob to storage
/// </summary>
public class UploadBlobRequest
{
    /// <summary>
    /// Container name
  /// </summary>
    public string ContainerName { get; set; } = default!;

    /// <summary>
    /// Blob name (file name with path)
  /// Example: "products/product-123.jpg" or "avatars/user-456.png"
    /// </summary>
    public string BlobName { get; set; } = default!;

    /// <summary>
    /// Content type (MIME type)
    /// Example: "image/jpeg", "application/pdf"
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// File data stream
    /// </summary>
    public Stream Data { get; set; } = default!;

    /// <summary>
    /// Overwrite if blob exists
    /// </summary>
    public bool Overwrite { get; set; } = true;

    /// <summary>
    /// Optional metadata (key-value pairs)
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
```

**File:** `src/Core/Application/Common/BlobStorage/BlobModel.cs`

```csharp
using System;

namespace ECO.WebApi.Application.Common.BlobStorage;

/// <summary>
/// Blob information model
/// </summary>
public class BlobModel
{
    /// <summary>
    /// Blob name (file name with path)
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Container name
    /// </summary>
    public string ContainerName { get; set; } = default!;

    /// <summary>
    /// Blob size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Content type (MIME type)
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// Public URL (if container is public)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// ETag for concurrency control
 /// </summary>
    public string? ETag { get; set; }

    /// <summary>
 /// Metadata (key-value pairs)
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
```

**Giải thích:**
- **UploadBlobRequest:** Contains all data needed to upload a blob
- **BlobModel:** Represents blob metadata (không chứa binary data)
- **Metadata:** Custom key-value pairs (e.g., "UploadedBy", "ProductId")

---

## 4. Infrastructure Layer - Azure Blob Storage Implementation

### Bước 4.1: Tạo BlobStorageSettings

**File:** `src/Infrastructure/Infrastructure/BlobStorage/BlobStorageSettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.BlobStorage;

/// <summary>
/// Configuration for Azure Blob Storage
/// </summary>
public class BlobStorageSettings
{
    /// <summary>
 /// Azure Blob Storage connection string
    /// </summary>
    public string ConnectionString { get; set; } = default!;

    /// <summary>
    /// Default container name
    /// </summary>
    public string DefaultContainer { get; set; } = "default";

    /// <summary>
    /// Enable public access by default
    /// </summary>
    public bool DefaultPublicAccess { get; set; } = false;
}
```

**Giải thích:**
- **ConnectionString:** Connection string from Azure Portal
- **DefaultContainer:** Fallback container nếu không specify
- **DefaultPublicAccess:** Containers mới tạo có public access hay không

---

### Bước 4.2: Implement AzureBlobStorageService

**File:** `src/Infrastructure/Infrastructure/BlobStorage/AzureBlobStorageService.cs`

```csharp
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ECO.WebApi.Application.Common.BlobStorage;
using ECO.WebApi.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECO.WebApi.Infrastructure.BlobStorage;

/// <summary>
/// Azure Blob Storage implementation
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageSettings _settings;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(
        IOptions<BlobStorageSettings> settings,
        ILogger<AzureBlobStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_settings.ConnectionString))
        {
             throw new InvalidOperationException("Azure Blob Storage connection string is not configured.");
        }

         _blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
    }

    public async Task<string> UploadAsync(UploadBlobRequest request, CancellationToken cancellationToken = default)
    {
      try
      {
        // Get container client
        var containerClient = _blobServiceClient.GetBlobContainerClient(request.ContainerName);

        // Create container if not exists
        await containerClient.CreateIfNotExistsAsync(
        publicAccessType: _settings.DefaultPublicAccess 
                 ? PublicAccessType.Blob 
                 : PublicAccessType.None,
        cancellationToken: cancellationToken);

        // Get blob client
        var blobClient = containerClient.GetBlobClient(request.BlobName);

        // Check if blob exists và overwrite setting
        if (!request.Overwrite && await blobClient.ExistsAsync(cancellationToken))
        {
            throw new ConflictException($"Blob '{request.BlobName}' already exists in container '{request.ContainerName}'.");
        }

       // Upload options
       var uploadOptions = new BlobUploadOptions
          {
            HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = request.ContentType
                }
          };

       // Add metadata if provided
      if (request.Metadata != null && request.Metadata.Any())
      {
            uploadOptions.Metadata = request.Metadata;
      }

      // Upload blob
       await blobClient.UploadAsync(
            request.Data,
            uploadOptions,
            cancellationToken);

          _logger.LogInformation("Uploaded blob '{BlobName}' to container '{ContainerName}' ({Size} bytes)",
            request.BlobName,
             request.ContainerName,
               request.Data.Length);

            // Return blob URL
            return blobClient.Uri.ToString();
        }
   catch (RequestFailedException ex)
        {
     _logger.LogError(ex, "Failed to upload blob '{BlobName}' to container '{ContainerName}'", 
request.BlobName, request.ContainerName);
  throw new InternalServerException($"Failed to upload blob: {ex.Message}", ex);
   }
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
     {
         var blobClient = _blobServiceClient
      .GetBlobContainerClient(containerName)
    .GetBlobClient(blobName);

            // Check if blob exists
    if (!await blobClient.ExistsAsync(cancellationToken))
      {
 throw new NotFoundException($"Blob '{blobName}' not found in container '{containerName}'.");
            }

            // Download to memory stream
            var memoryStream = new MemoryStream();
   await blobClient.DownloadToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0; // Reset position for reading

      _logger.LogInformation(
                "Downloaded blob '{BlobName}' from container '{ContainerName}' ({Size} bytes)",
          blobName,
     containerName,
 memoryStream.Length);

            return memoryStream;
    }
     catch (RequestFailedException ex)
        {
         _logger.LogError(ex, "Failed to download blob '{BlobName}' from container '{ContainerName}'", 
      blobName, containerName);
   throw new InternalServerException($"Failed to download blob: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
 {
      try
        {
       var blobClient = _blobServiceClient
.GetBlobContainerClient(containerName)
   .GetBlobClient(blobName);

            // Delete blob
          var deleted = await blobClient.DeleteIfExistsAsync(
  DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken);

        if (deleted.Value)
            {
    _logger.LogInformation(
         "Deleted blob '{BlobName}' from container '{ContainerName}'",
       blobName,
   containerName);
     }
  else
            {
           _logger.LogWarning(
   "Blob '{BlobName}' not found in container '{ContainerName}' (already deleted?)",
   blobName,
    containerName);
         }
   }
        catch (RequestFailedException ex)
        {
   _logger.LogError(ex, "Failed to delete blob '{BlobName}' from container '{ContainerName}'", 
       blobName, containerName);
 throw new InternalServerException $"Failed to delete blob: {ex.Message}", ex);
        }
    }

    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
      {
  var blobClient = _blobServiceClient
    .GetBlobContainerClient(containerName)
   .GetBlobClient(blobName);

 return await blobClient.ExistsAsync(cancellationToken);
        }
        catch (RequestFailedException ex)
        {
    _logger.LogError(ex, "Failed to check existence of blob '{BlobName}' in container '{ContainerName}'", 
    blobName, containerName);
 return false;
        }
    }

    public async Task<string> GetBlobUrlAsync(
        string containerName, 
    string blobName, 
        int expiryMinutes = 60, 
      CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

     // Check if blob exists
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
      throw new NotFoundException($"Blob '{blobName}' not found in container '{containerName}'.");
            }

            // Check if container is public
          var properties = await containerClient.GetPropertiesAsync(cancellationToken);
            if (properties.Value.PublicAccess != PublicAccessType.None)
            {
       // Public container - return direct URL
       return blobClient.Uri.ToString();
            }

        // Private container - generate SAS token
  if (!blobClient.CanGenerateSasUri)
     {
     throw new InvalidOperationException("Cannot generate SAS token. Check storage account configuration.");
     }

         var sasBuilder = new BlobSasBuilder
            {
     BlobContainerName = containerName,
                BlobName = blobName,
    Resource = "b", // Blob
      StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Grace period
       ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
            };

     // Grant read permission
sasBuilder.SetPermissions(BlobSasPermissions.Read);

  // Generate SAS token and URL
            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
    }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to get URL for blob '{BlobName}' in container '{ContainerName}'", 
  blobName, containerName);
            throw new InternalServerException($"Failed to get blob URL: {ex.Message}", ex);
        }
    }

    public async Task<List<BlobModel>> ListBlobsAsync(
        string containerName, 
string? prefix = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

         // Check if container exists
            if (!await containerClient.ExistsAsync(cancellationToken))
  {
    throw new NotFoundException($"Container '{containerName}' not found.");
            }

 var blobs = new List<BlobModel>();

         // List blobs with optional prefix filter
         await foreach (var blobItem in containerClient.GetBlobsAsync(
     prefix: prefix,
    cancellationToken: cancellationToken))
        {
             var blobClient = containerClient.GetBlobClient(blobItem.Name);

         blobs.Add(new BlobModel
          {
      Name = blobItem.Name,
           ContainerName = containerName,
            Size = blobItem.Properties.ContentLength ?? 0,
            ContentType = blobItem.Properties.ContentType ?? "application/octet-stream",
          LastModified = blobItem.Properties.LastModified ?? DateTimeOffset.UtcNow,
        Url = blobClient.Uri.ToString(),
        ETag = blobItem.Properties.ETag?.ToString(),
         Metadata = blobItem.Metadata
      });
       }

         _logger.LogInformation(
   "Listed {Count} blobs in container '{ContainerName}' (prefix: '{Prefix}')",
    blobs.Count,
       containerName,
  prefix ?? "(none)");

    return blobs;
  }
        catch (RequestFailedException ex)
        {
   _logger.LogError(ex, "Failed to list blobs in container '{ContainerName}'", containerName);
     throw new InternalServerException($"Failed to list blobs: {ex.Message}", ex);
        }
    }

    public async Task CreateContainerAsync(
      string containerName, 
      bool isPublic = false, 
        CancellationToken cancellationToken = default)
    {
  try
 {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

  // Create container
            await containerClient.CreateIfNotExistsAsync(
         publicAccessType: isPublic ? PublicAccessType.Blob : PublicAccessType.None,
      cancellationToken: cancellationToken);

      _logger.LogInformation(
   "Created container '{ContainerName}' (public: {IsPublic})",
          containerName,
                isPublic);
     }
        catch (RequestFailedException ex)
        {
         _logger.LogError(ex, "Failed to create container '{ContainerName}'", containerName);
            throw new InternalServerException($"Failed to create container: {ex.Message}", ex);
        }
    }

    public async Task DeleteContainerAsync(string containerName, CancellationToken cancellationToken = default)
    {
        try
     {
      var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

     // Delete container
    var deleted = await containerClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            if (deleted.Value)
            {
_logger.LogInformation("Deleted container '{ContainerName}'", containerName);
     }
            else
        {
          _logger.LogWarning("Container '{ContainerName}' not found (already deleted?)", containerName);
        }
        }
        catch (RequestFailedException ex)
        {
        _logger.LogError(ex, "Failed to delete container '{ContainerName}'", containerName);
   throw new InternalServerException($"Failed to delete container: {ex.Message}", ex);
  }
    }

 public async Task<List<string>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
  try
        {
  var containers = new List<string>();

      await foreach (var containerItem in _blobServiceClient.GetBlobContainersAsync(cancellationToken: cancellationToken))
          {
   containers.Add(containerItem.Name);
       }

            _logger.LogInformation("Listed {Count} containers", containers.Count);

            return containers;
        }
        catch (RequestFailedException ex)
 {
         _logger.LogError(ex, "Failed to list containers");
    throw new InternalServerException $"Failed to list containers: {ex.Message}", ex);
        }
    }
}
```

**Giải thích chi tiết:**

**Upload Flow:**
1. Get container client (auto-create if not exists)
2. Check overwrite setting
3. Set content type và metadata
4. Upload stream to blob
5. Return public URL

**Download Flow:**
1. Get blob client
2. Check blob exists
3. Download to MemoryStream
4. Reset stream position để caller có thể read

**SAS Token Generation:**
- Public containers: Return direct URL
- Private containers: Generate SAS token with read permission
- Grace period: -5 minutes để avoid clock skew

**Error Handling:**
- `RequestFailedException`: Azure SDK exception
- Wrap thành `InternalServerException` với message rõ ràng
- Log tất cả errors với context

---

### Bước 4.3: Tạo Startup Configuration

**File:** `src/Infrastructure/Infrastructure/BlobStorage/Startup.cs`

```csharp
using ECO.WebApi.Application.Common.BlobStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ECO.WebApi.Infrastructure.BlobStorage;

/// <summary>
/// Blob storage dependency injection registration
/// </summary>
internal static class Startup
{
    internal static IServiceCollection AddBlobStorage(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Configure settings
        services.Configure<BlobStorageSettings>(
         config.GetSection(nameof(BlobStorageSettings)));

        var settings = config.GetSection(nameof(BlobStorageSettings)).Get<BlobStorageSettings>();

        if (settings == null)
        {
         throw new InvalidOperationException("BlobStorageSettings is not configured.");
   }

    if (string.IsNullOrEmpty(settings.ConnectionString))
        {
  throw new InvalidOperationException("Azure Blob Storage connection string is not configured.");
        }

        // Register Azure Blob Storage service
        services.AddTransient<IBlobStorageService, AzureBlobStorageService>();

        return services;
    }
}
```

**Giải thích:**
- Configure settings từ appsettings.json
- Validate connection string exists
- Register `AzureBlobStorageService` as `IBlobStorageService`

---

### Bước 4.4: Register trong Infrastructure Startup

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
// ...existing code...

using ECO.WebApi.Infrastructure.BlobStorage;

namespace ECO.WebApi.Infrastructure;

public static class Startup
{
  public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
{
        // ...existing registrations...

        return services
            .AddPersistence(config)
            .AddAuth(config)
 .AddBackgroundJobs(config)
 .AddCaching(config)
         .AddMailing(config)
            .AddBlobStorage(config) // ✅ Add Blob Storage
            .AddServices();
    }
}
```

---

## 5. Configuration

### Bước 5.1: appsettings.json - Azure Blob Storage

**File:** `src/Host/Host/appsettings.json`

```json
{
  "BlobStorageSettings": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
    "DefaultContainer": "uploads",
    "DefaultPublicAccess": false
  }
}
```

### Bước 5.2: appsettings.Development.json - Local Development

**File:** `src/Host/Host/appsettings.Development.json`

```json
{
  "BlobStorageSettings": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "DefaultContainer": "dev-uploads",
    "DefaultPublicAccess": true
  }
}
```

**⚠️ Lưu ý:** `UseDevelopmentStorage=true` dùng với **Azurite** (Azure Storage Emulator):
```bash
# Install Azurite
npm install -g azurite

# Run Azurite
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

---

## 6. Usage Examples

### Bước 6.1: Upload Product Image

**Request:**
```csharp
public class UploadProductImageRequest : IRequest<string>
{
    public Guid ProductId { get; set; }
    public IFormFile Image { get; set; } = default!;
}
```

**Handler:**
```csharp
using ECO.WebApi.Application.Common.BlobStorage;
using MediatR;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ECO.WebApi.Application.Catalog.Products;

public class UploadProductImageHandler : IRequestHandler<UploadProductImageRequest, string>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IRepository<Product> _repository;

    public UploadProductImageHandler(
        IBlobStorageService blobStorage,
        IRepository<Product> repository)
    {
        _blobStorage = blobStorage;
        _repository = repository;
    }

    public async Task<string> Handle(
        UploadProductImageRequest request, 
        CancellationToken cancellationToken)
    {
        // Validate product exists
  var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken)
  ?? throw new NotFoundException($"Product {request.ProductId} not found.");

        // Validate image file
        if (request.Image.Length == 0)
        {
        throw new ValidationException("Image file is empty.");
    }

    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(request.Image.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(extension))
        {
        throw new ValidationException($"Invalid image format. Allowed: {string.Join(", ", allowedExtensions)}");
        }

        // Generate unique blob name
   var blobName = $"{request.ProductId}/{Guid.NewGuid()}{extension}";

        // Upload to blob storage
    var uploadRequest = new UploadBlobRequest
        {
            ContainerName = "products",
 BlobName = blobName,
     ContentType = request.Image.ContentType,
        Data = request.Image.OpenReadStream(),
    Overwrite = false,
            Metadata = new Dictionary<string, string>
    {
                { "ProductId", request.ProductId.ToString() },
      { "OriginalFileName", request.Image.FileName },
        { "UploadedAt", DateTime.UtcNow.ToString("O") }
       }
     };

     var imageUrl = await _blobStorage.UploadAsync(uploadRequest, cancellationToken);

        // Update product image URL
        product.ImageUrl = imageUrl;
await _repository.UpdateAsync(product, cancellationToken);

  return imageUrl;
    }
}
```

**Controller:**
```csharp
[HttpPost("{id}/image")]
[MustHavePermission(ECOAction.Update, ECOFunction.Products)]
public async Task<ActionResult<string>> UploadImage(
    Guid id,
    [FromForm] IFormFile image)
{
    var request = new UploadProductImageRequest
    {
  ProductId = id,
        Image = image
    };

    var imageUrl = await Mediator.Send(request);

 return Ok(new { imageUrl });
}
```

**API Call:**
```bash
curl -X POST https://localhost:7001/api/products/123e4567-e89b-12d3-a456-426614174000/image \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "image=@product.jpg"
```

**Response:**
```json
{
  "imageUrl": "https://mystorageaccount.blob.core.windows.net/products/123e4567-e89b-12d3-a456-426614174000/9c8f7e6d-5b4a-3c2b-1a0e-9f8d7c6b5a4e.jpg"
}
```

---

### Bước 6.2: Download User Avatar

**Request:**
```csharp
public class DownloadAvatarRequest : IRequest<(Stream Stream, string ContentType, string FileName)>
{
public Guid UserId { get; set; }
}
```

**Handler:**
```csharp
public class DownloadAvatarHandler 
    : IRequestHandler<DownloadAvatarRequest, (Stream Stream, string ContentType, string FileName)>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IRepository<ApplicationUser> _userRepository;

  public DownloadAvatarHandler(
        IBlobStorageService blobStorage,
        IRepository<ApplicationUser> userRepository)
    {
  _blobStorage = blobStorage;
        _userRepository = userRepository;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> Handle(
        DownloadAvatarRequest request,
        CancellationToken cancellationToken)
    {
        // Get user
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User {request.UserId} not found.");

        if (string.IsNullOrEmpty(user.ImageUrl))
   {
 throw new NotFoundException("User has no avatar.");
      }

        // Extract container và blob name từ URL
        // Example URL: https://mystorageaccount.blob.core.windows.net/avatars/user-123.jpg
        var uri = new Uri(user.ImageUrl);
     var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
    var containerName = segments[0]; // "avatars"
  var blobName = segments[1];      // "user-123.jpg"

        // Download from blob storage
        var stream = await _blobStorage.DownloadAsync(containerName, blobName, cancellationToken);

 // Determine content type from extension
        var extension = Path.GetExtension(blobName).ToLowerInvariant();
        var contentType = extension switch
        {
     ".jpg" or ".jpeg" => "image/jpeg",
      ".png" => "image/png",
            ".gif" => "image/gif",
  ".webp" => "image/webp",
  _ => "application/octet-stream"
        };

        return (stream, contentType, blobName);
    }
}
```

**Controller:**
```csharp
[HttpGet("{id}/avatar")]
[AllowAnonymous]
public async Task<IActionResult> DownloadAvatar(Guid id)
{
    var request = new DownloadAvatarRequest { UserId = id };
    var (stream, contentType, fileName) = await Mediator.Send(request);

    return File(stream, contentType, fileName);
}
```

---

### Bước 6.3: Generate Temporary Download Link (SAS Token)

**Request:**
```csharp
public class GetDocumentDownloadLinkRequest : IRequest<string>
{
public Guid DocumentId { get; set; }
    public int ExpiryMinutes { get; set; } = 60;
}
```

**Handler:**
```csharp
public class GetDocumentDownloadLinkHandler : IRequestHandler<GetDocumentDownloadLinkRequest, string>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IRepository<Document> _repository;
    private readonly ICurrentUser _currentUser;

    public GetDocumentDownloadLinkHandler(
        IBlobStorageService blobStorage,
      IRepository<Document> repository,
        ICurrentUser currentUser)
    {
      _blobStorage = blobStorage;
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(
        GetDocumentDownloadLinkRequest request,
        CancellationToken cancellationToken)
    {
        // Get document
   var document = await _repository.GetByIdAsync(request.DocumentId, cancellationToken)
          ?? throw new NotFoundException($"Document {request.DocumentId} not found.");

        // Check permission (example: owner only)
        if (document.OwnerId != _currentUser.GetUserId())
        {
       throw new ForbiddenException("You don't have permission to download this document.");
        }

        // Extract container và blob name từ URL
        var uri = new Uri(document.BlobUrl);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
      var containerName = segments[0];
     var blobName = segments[1];

        // Generate temporary download link (SAS token)
     var downloadUrl = await _blobStorage.GetBlobUrlAsync(
    containerName,
            blobName,
          expiryMinutes: request.ExpiryMinutes,
       cancellationToken);

    return downloadUrl;
    }
}
```

**Controller:**
```csharp
[HttpGet("documents/{id}/download-link")]
[MustHavePermission(ECOAction.View, ECOFunction.Documents)]
public async Task<ActionResult<string>> GetDownloadLink(
    Guid id,
    [FromQuery] int expiryMinutes = 60)
{
    var request = new GetDocumentDownloadLinkRequest
    {
  DocumentId = id,
        ExpiryMinutes = expiryMinutes
    };

    var downloadUrl = await Mediator.Send(request);

    return Ok(new { downloadUrl, expiresIn = $"{expiryMinutes} minutes" });
}
```

**API Call:**
```bash
curl -X GET "https://localhost:7001/api/documents/123e4567-e89b-12d3-a456-426614174000/download-link?expiryMinutes=30" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Response:**
```json
{
  "downloadUrl": "https://mystorageaccount.blob.core.windows.net/documents/doc-123.pdf?sv=2021-08-06&se=2024-01-30T10%3A30%3A00Z&sr=b&sp=r&sig=SIGNATURE",
  "expiresIn": "30 minutes"
}
```

---

### Bước 6.4: List Blobs với Prefix Filter

**Request:**
```csharp
public class ListProductImagesRequest : IRequest<List<BlobModel>>
{
    public Guid ProductId { get; set; }
}
```

**Handler:**
```csharp
public class ListProductImagesHandler : IRequestHandler<ListProductImagesRequest, List<BlobModel>>
{
    private readonly IBlobStorageService _blobStorage;

    public ListProductImagesHandler(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public async Task<List<BlobModel>> Handle(
 ListProductImagesRequest request,
        CancellationToken cancellationToken)
    {
        // List all blobs with prefix "productId/"
        var blobs = await _blobStorage.ListBlobsAsync(
containerName: "products",
   prefix: request.ProductId.ToString(),
    cancellationToken);

 return blobs;
    }
}
```

**Controller:**
```csharp
[HttpGet("{id}/images")]
[MustHavePermission(ECOAction.View, ECOFunction.Products)]
public async Task<ActionResult<List<BlobModel>>> ListImages(Guid id)
{
    var request = new ListProductImagesRequest { ProductId = id };
    var images = await Mediator.Send(request);

    return Ok(images);
}
```

---

### Bước 6.5: Delete Old Avatar When Uploading New One

**Handler with cleanup:**
```csharp
public class UpdateUserAvatarHandler : IRequestHandler<UpdateUserAvatarRequest, string>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IRepository<ApplicationUser> _userRepository;
  private readonly ICurrentUser _currentUser;
    private readonly ILogger<UpdateUserAvatarHandler> _logger;

    public UpdateUserAvatarHandler(
        IBlobStorageService blobStorage,
  IRepository<ApplicationUser> userRepository,
   ICurrentUser currentUser,
        ILogger<UpdateUserAvatarHandler> logger)
    {
        _blobStorage = blobStorage;
        _userRepository = userRepository;
      _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<string> Handle(
        UpdateUserAvatarRequest request,
  CancellationToken cancellationToken)
 {
   var userId = _currentUser.GetUserId();

      var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
    ?? throw new NotFoundException($"User {userId} not found.");

        // Delete old avatar if exists
    if (!string.IsNullOrEmpty(user.ImageUrl))
        {
     try
      {
                var oldUri = new Uri(user.ImageUrl);
    var segments = oldUri.AbsolutePath.TrimStart('/').Split('/', 2);
     var oldContainer = segments[0];
        var oldBlobName = segments[1];

   await _blobStorage.DeleteAsync(oldContainer, oldBlobName, cancellationToken);

   _logger.LogInformation(
"Deleted old avatar for user {UserId}: {BlobName}",
       userId,
           oldBlobName);
            }
            catch (Exception ex)
     {
   _logger.LogWarning(ex, "Failed to delete old avatar for user {UserId}", userId);
      // Continue - không fail request nếu delete failed
     }
        }

  // Upload new avatar
  var blobName = $"user-{userId}{Path.GetExtension(request.Avatar.FileName)}";

    var uploadRequest = new UploadBlobRequest
        {
   ContainerName = "avatars",
         BlobName = blobName,
 ContentType = request.Avatar.ContentType,
            Data = request.Avatar.OpenReadStream(),
   Overwrite = true
        };

        var newAvatarUrl = await _blobStorage.UploadAsync(uploadRequest, cancellationToken);

    // Update user
        user.ImageUrl = newAvatarUrl;
        await _userRepository.UpdateAsync(user, cancellationToken);

     return newAvatarUrl;
    }
}
```

---

## 7. Testing

### Bước 7.1: Tạo BlobStorageController (Development Only)

**File:** `src/Host/Host/Controllers/BlobStorageController.cs`

```csharp
using ECO.WebApi.Application.Common.BlobStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ECO.WebApi.Host.Controllers;

/// <summary>
/// Blob storage testing endpoints (Development only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // For testing only - remove in production
public class BlobStorageController : ControllerBase
{
    private readonly IBlobStorageService _blobStorage;

    public BlobStorageController(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    /// <summary>
    /// Upload file to blob storage
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
      [FromForm] string containerName,
     [FromForm] IFormFile file,
    CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
 return BadRequest("No file uploaded.");
  }

        var blobName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        var request = new UploadBlobRequest
      {
       ContainerName = containerName,
  BlobName = blobName,
 ContentType = file.ContentType,
       Data = file.OpenReadStream(),
            Metadata = new Dictionary<string, string>
     {
           { "OriginalFileName", file.FileName },
       { "UploadedAt", DateTime.UtcNow.ToString("O") }
     }
        };

        var url = await _blobStorage.UploadAsync(request, cancellationToken);

        return Ok(new
        {
url,
 containerName,
            blobName,
            size = file.Length,
          contentType = file.ContentType
   });
    }

    /// <summary>
    /// Download file from blob storage
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download(
        [FromQuery] string containerName,
        [FromQuery] string blobName,
        CancellationToken cancellationToken)
    {
        var stream = await _blobStorage.DownloadAsync(containerName, blobName, cancellationToken);

        var contentType = Path.GetExtension(blobName).ToLowerInvariant() switch
        {
".jpg" or ".jpeg" => "image/jpeg",
       ".png" => "image/png",
     ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        return File(stream, contentType, blobName);
    }

    /// <summary>
    /// Delete blob from storage
    /// </summary>
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete(
     [FromQuery] string containerName,
        [FromQuery] string blobName,
   CancellationToken cancellationToken)
    {
     await _blobStorage.DeleteAsync(containerName, blobName, cancellationToken);

        return Ok(new { message = $"Blob '{blobName}' deleted successfully." });
    }

    /// <summary>
    /// Get temporary download link (SAS token)
    /// </summary>
    [HttpGet("download-link")]
    public async Task<IActionResult> GetDownloadLink(
[FromQuery] string containerName,
        [FromQuery] string blobName,
        [FromQuery] int expiryMinutes = 60,
        CancellationToken cancellationToken)
    {
        var url = await _blobStorage.GetBlobUrlAsync(
     containerName,
            blobName,
      expiryMinutes,
   cancellationToken);

        return Ok(new
   {
      url,
        expiresIn = $"{expiryMinutes} minutes"
        });
    }

    /// <summary>
    /// List blobs in container
    /// </summary>
    [HttpGet("list")]
 public async Task<IActionResult> ListBlobs(
        [FromQuery] string containerName,
        [FromQuery] string? prefix,
   CancellationToken cancellationToken)
    {
        var blobs = await _blobStorage.ListBlobsAsync(containerName, prefix, cancellationToken);

        return Ok(new
        {
         containerName,
      prefix = prefix ?? "(none)",
  count = blobs.Count,
     blobs
        });
    }

    /// <summary>
    /// Create container
    /// </summary>
[HttpPost("containers")]
    public async Task<IActionResult> CreateContainer(
        [FromQuery] string containerName,
        [FromQuery] bool isPublic = false,
     CancellationToken cancellationToken)
    {
        await _blobStorage.CreateContainerAsync(containerName, isPublic, cancellationToken);

  return Ok(new
        {
            message = $"Container '{containerName}' created successfully.",
          isPublic
        });
    }

    /// <summary>
    /// List all containers
    /// </summary>
    [HttpGet("containers")]
    public async Task<IActionResult> ListContainers(CancellationToken cancellationToken)
    {
        var containers = await _blobStorage.ListContainersAsync(cancellationToken);

        return Ok(new
      {
     count = containers.Count,
            containers
        });
    }
}
```

---

### Bước 7.2: Test với Postman/curl

**Test 1: Upload file**
```bash
curl -X POST "https://localhost:7001/api/blobstorage/upload" \
-F "containerName=test" \
  -F "file=@test-image.jpg"
```

**Expected Response:**
```json
{
  "url": "https://mystorageaccount.blob.core.windows.net/test/9c8f7e6d-5b4a-3c2b-1a0e-9f8d7c6b5a4e.jpg",
  "containerName": "test",
  "blobName": "9c8f7e6d-5b4a-3c2b-1a0e-9f8d7c6b5a4e.jpg",
  "size": 245678,
"contentType": "image/jpeg"
}
```

**Test 2: List blobs**
```bash
curl -X GET "https://localhost:7001/api/blobstorage/list?containerName=test"
```

**Test 3: Get download link**
```bash
curl -X GET "https://localhost:7001/api/blobstorage/download-link?containerName=test&blobName=9c8f7e6d-5b4a-3c2b-1a0e-9f8d7c6b5a4e.jpg&expiryMinutes=30"
```

**Test 4: Download file**
```bash
curl -X GET "https://localhost:7001/api/blobstorage/download?containerName=test&blobName=9c8f7e6d-5b4a-3c2b-1a0e-9f8d7c6b5a4e.jpg" \
  --output downloaded-image.jpg
```

**Test 5: Delete blob**
```bash
curl -X DELETE "https://localhost:7001/api/blobstorage/delete?containerName=test&blobName=9c8f7e6d-5b4a-3c2b-1a0e-9f8d7c6b5a4e.jpg"
```

---

## 8. Best Practices

### 8.1: Container Naming Strategy

**Recommended structure:**
```
products/         - Product images
avatars/  - User avatars
documents/        - User documents
attachments/      - Email attachments
exports/       - Export files (Excel, PDF)
temp/      - Temporary files (cleanup after 24h)
```

**File naming convention:**
```
{entity}-{id}/{guid}.{extension}
product-123e4567-e89b/9c8f7e6d-5b4a.jpg
user-456d7c8e-9f0a/avatar.png
```

---

### 8.2: Security Best Practices

**1. Validation:**
```csharp
// Validate file size
const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
if (file.Length > MaxFileSize)
{
    throw new ValidationException($"File size exceeds limit ({MaxFileSize / 1024 / 1024} MB).");
}

// Validate file extension
var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!allowedExtensions.Contains(extension))
{
    throw new ValidationException($"Invalid file type. Allowed: {string.Join(", ", allowedExtensions)}");
}

// Validate content type
var allowedContentTypes = new[] { "image/jpeg", "image/png", "application/pdf" };
if (!allowedContentTypes.Contains(file.ContentType))
{
    throw new ValidationException("Invalid content type.");
}
```

**2. Access Control:**
```csharp
// Check permission before download
var document = await _repository.GetByIdAsync(documentId);
if (document.OwnerId != _currentUser.GetUserId() && 
    !await _currentUser.HasPermissionAsync(ECOAction.View, ECOFunction.AllDocuments))
{
    throw new ForbiddenException("Access denied.");
}
```

**3. SAS Token Expiry:**
```csharp
// Short expiry cho sensitive documents
var expiryMinutes = document.IsConfidential ? 15 : 60;
var url = await _blobStorage.GetBlobUrlAsync(container, blobName, expiryMinutes);
```

---

### 8.3: Performance Optimization

**1. Stream directly to response:**
```csharp
// ❌ Bad - loads entire file to memory
var bytes = await _blobStorage.DownloadAsBytesAsync(container, blobName);
return File(bytes, contentType);

// ✅ Good - streams directly
var stream = await _blobStorage.DownloadAsync(container, blobName);
return File(stream, contentType);
```

**2. Use Azure CDN:**
```json
{
  "BlobStorageSettings": {
  "CdnEndpoint": "https://cdn.myapp.com",
    "UseCdn": true
  }
}
```

**3. Implement caching:**
```csharp
var cacheKey = $"blob-metadata:{containerName}:{blobName}";
var metadata = await _cache.GetOrSetAsync(
    cacheKey,
    async () => await _blobStorage.GetMetadataAsync(containerName, blobName),
    TimeSpan.FromMinutes(15));
```

---

### 8.4: Error Handling

**Retry policy:**
```csharp
using Polly;

var retryPolicy = Policy
    .Handle<RequestFailedException>(ex => 
        ex.Status == 500 || 
   ex.Status == 503 || 
        ex.Status == 429) // Too Many Requests
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
   _logger.LogWarning(
      "Retry {RetryCount} after {Delay}s due to {Exception}",
     retryCount,
  timeSpan.TotalSeconds,
   exception.GetType().Name);
        });

await retryPolicy.ExecuteAsync(async () =>
{
    await _blobStorage.UploadAsync(request, cancellationToken);
});
```

---

### 8.5: Cleanup Strategy

**Background job to delete old temp files:**
```csharp
public class CleanupTempBlobsJob : IJob
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<CleanupTempBlobsJob> _logger;

    public CleanupTempBlobsJob(
        IBlobStorageService blobStorage,
        ILogger<CleanupTempBlobsJob> logger)
    {
   _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
   var blobs = await _blobStorage.ListBlobsAsync("temp");

   var cutoffDate = DateTimeOffset.UtcNow.AddDays(-1);
        var deletedCount = 0;

        foreach (var blob in blobs.Where(b => b.LastModified < cutoffDate))
        {
     await _blobStorage.DeleteAsync("temp", blob.Name);
      deletedCount++;
  }

      _logger.LogInformation(
    "Cleaned up {DeletedCount} temp blobs older than {CutoffDate}",
    deletedCount,
     cutoffDate);
    }
}
```

---

## 9. Troubleshooting

### 9.1: Common Issues

**Issue 1: "Connection string is invalid"**
```
Solution:
1. Verify connection string format
2. Check storage account name và key
3. Test với Azure Storage Explorer
```

**Issue 2: "Container not found"**
```csharp
// Ensure container is created before upload
await _blobStorage.CreateContainerAsync("my-container");
```

**Issue 3: "SAS token access denied"**
```
Solution:
1. Check token expiry time
2. Verify permissions (Read/Write)
3. Check clock skew (StartsOn should be few minutes before current time)
```

**Issue 4: "Upload too slow"**
```
Solution:
1. Check network bandwidth
2. Use Azure CDN
3. Enable compression for large files
4. Consider chunked uploads for very large files (>100MB)
```

---

### 9.2: Debugging Tips

**Enable detailed logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Azure.Storage.Blobs": "Debug",
      "ECO.WebApi.Infrastructure.BlobStorage": "Debug"
    }
  }
}
```

**Test with Azurite (Local Emulator):**
```bash
# Install
npm install -g azurite

# Run
azurite --silent --location c:\azurite

# Connection string
"UseDevelopmentStorage=true"
```

---

## 10. Summary

### ✅ Đã hoàn thành trong bước này:

**Application Layer:**
- ✅ IBlobStorageService interface với 9 methods
- ✅ UploadBlobRequest, BlobModel DTOs
- ✅ Abstraction cho storage operations

**Infrastructure Layer:**
- ✅ AzureBlobStorageService implementation
- ✅ Container management (create, delete, list)
- ✅ Blob operations (upload, download, delete, exists)
- ✅ SAS token generation
- ✅ Metadata support

**Configuration:**
- ✅ BlobStorageSettings
- ✅ Azure connection string configuration
- ✅ Startup registration

**Testing:**
- ✅ BlobStorageController với 6 endpoints
- ✅ Upload/Download/Delete testing
- ✅ SAS token testing

---

### 📊 Architecture Diagram:

```
┌─────────────────────────────────────────────────┐
│        Application Layer     │
│  ┌──────────────────────────────────────────┐  │
│  │    IBlobStorageService (Interface)       │  │
│  │    - UploadAsync()      │  │
│  │    - DownloadAsync()          │  │
│  │    - DeleteAsync()  │  │
│  │    - GetBlobUrlAsync() (SAS token)       │  │
│  │    - ListBlobsAsync()    │  │
│  │    - CreateContainerAsync() │  │
│  └──────────────────────────────────────────┘  │
└────────────────┬────────────────────────────────┘
       │ implements
   ↓
┌─────────────────────────────────────────────────┐
│    Azure Blob Implementation      │
├─────────────────────────────────────────────────┤
│  BlobServiceClient     │
│    ├─ Upload         │
│    ├─ Download   │
│    ├─ Delete     │
│    └─ Generate SAS      │
└────────────────┬────────────────────────────────┘
     │
       ↓
┌─────────────────────────────────────────────────┐
│    Azure Blob Storage   │
│ Containers:        │
│    - products          │
│    - avatars    │
│    - documents             │
└─────────────────────────────────────────────────┘
```

---

### 📌 Key Concepts:

**Abstraction Pattern:**
- Single interface (`IBlobStorageService`)
- Azure implementation (this document)
- Provider-agnostic business logic

**SAS Token:**
- Temporary access to private blobs
- Configurable expiry time
- Read/Write permissions

**Streaming:**
- Use `Stream` for large files (memory efficient)
- Use `byte[]` only for small files (<1MB)

---

### 📁 File Structure:

```
src/Core/Application/Common/BlobStorage/
├── IBlobStorageService.cs
├── UploadBlobRequest.cs
└── BlobModel.cs

src/Infrastructure/Infrastructure/BlobStorage/
├── AzureBlobStorageService.cs
├── BlobStorageSettings.cs
└── Startup.cs

src/Host/Host/
├── Controllers/
│   └── BlobStorageController.cs (testing only)
└── appsettings.json
```

---

## 11. Next Steps

**Tiếp theo:** [BUILD_25 - Background Jobs với Hangfire](BUILD_25_Background_Jobs.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Setup Hangfire background jobs
2. ✅ Fire-and-forget jobs
3. ✅ Delayed jobs
4. ✅ Recurring jobs (scheduled tasks)
5. ✅ Job dashboard và monitoring
6. ✅ Use case: Email sending, report generation, cleanup tasks

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
