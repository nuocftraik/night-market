# AWS S3 Storage Implementation - Alternative Provider

> 📚 [Quay lại BUILD_24 Main](BUILD_24_Blob_Storage.md)  
> 📋 **Prerequisites:** BUILD_24 (Azure Blob Storage) đã đọc - hiểu IBlobStorageService interface

Tài liệu này hướng dẫn implement **AWS S3** storage provider - alternative cho Azure Blob Storage.

**⚠️ Lưu ý:** File này là **optional sub-document** của BUILD_24. Main document focus vào Azure Blob Storage. Chỉ đọc file này nếu bạn cần AWS S3 support.

---

## 1. Overview

**Làm gì:** Implement `IBlobStorageService` interface với AWS S3 SDK.

**Tại sao cần AWS S3:**
- **Multi-cloud strategy:** Không lock-in vào Azure
- **Cost optimization:** AWS S3 có pricing tốt hơn cho một số scenarios
- **Regional availability:** AWS có data centers ở nhiều regions hơn
- **Existing infrastructure:** Team đã dùng AWS cho services khác

**Trong document này:**
- ✅ Add AWS S3 packages
- ✅ Implement AwsS3StorageService
- ✅ Configure AWS credentials
- ✅ Update Startup registration
- ✅ Key differences vs Azure Blob Storage

**⚠️ Prerequisites:**
- Đã đọc BUILD_24 main document (hiểu `IBlobStorageService` interface)
- Đã có `BlobStorageSettings`, `UploadBlobRequest`, `BlobModel` (từ BUILD_24)

---

## 2. Add AWS S3 Packages

### Bước 2.1: Add AWS SDK Packages

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Azure Blob Storage SDK (from BUILD_24 main) -->
    <PackageReference Include="Azure.Storage.Blobs" Version="12.21.2" />
    
    <!-- AWS S3 SDK -->
  <PackageReference Include="AWSSDK.S3" Version="3.7.402.13" />
    <PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.7.301" />
</ItemGroup>
```

**Giải thích packages:**
- `AWSSDK.S3`: AWS SDK cho S3 operations (upload, download, delete)
- `AWSSDK.Extensions.NETCore.Setup`: Integration helpers cho .NET Core DI

**⚠️ Lưu ý:**
- AWSSDK.S3 version 3.7.x có breaking changes từ 3.3.x
- `IAmazonS3` interface là main entry point

---

## 3. AWS S3 Implementation

### Bước 3.1: Implement AwsS3StorageService

**File:** `src/Infrastructure/Infrastructure/BlobStorage/AwsS3StorageService.cs`

```csharp
using Amazon.S3;
using Amazon.S3.Model;
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
/// AWS S3 Storage implementation
/// </summary>
public class AwsS3StorageService : IBlobStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly BlobStorageSettings _settings;
    private readonly ILogger<AwsS3StorageService> _logger;
    private readonly string _bucketName;

    public AwsS3StorageService(
        IAmazonS3 s3Client,
        IOptions<BlobStorageSettings> settings,
        ILogger<AwsS3StorageService> logger)
    {
     _s3Client = s3Client;
        _settings = settings.Value;
        _logger = logger;

        if (_settings.Aws == null || string.IsNullOrEmpty(_settings.Aws.BucketName))
        {
   throw new InvalidOperationException("AWS S3 settings are not configured.");
        }

    _bucketName = _settings.Aws.BucketName;
    }

    public async Task<string> UploadAsync(UploadBlobRequest request, CancellationToken cancellationToken = default)
    {
   try
  {
            // S3 key format: container/blobname
            var key = $"{request.ContainerName}/{request.BlobName}";

     // Check if object exists
    if (!request.Overwrite)
            {
          try
     {
        await _s3Client.GetObjectMetadataAsync(_bucketName, key, cancellationToken);
         throw new ConflictException($"Object '{key}' already exists in bucket '{_bucketName}'.");
      }
       catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
    // Object doesn't exist - OK to upload
      }
       }

            // Upload request
            var putRequest = new PutObjectRequest
  {
     BucketName = _bucketName,
       Key = key,
        InputStream = request.Data,
        ContentType = request.ContentType,
           AutoCloseStream = false
        };

  // Add metadata
       if (request.Metadata != null)
 {
   foreach (var kvp in request.Metadata)
                {
        putRequest.Metadata.Add(kvp.Key, kvp.Value);
                }
    }

            // Upload
         var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            _logger.LogInformation(
                "Uploaded object '{Key}' to bucket '{BucketName}' (ETag: {ETag})",
                key,
    _bucketName,
   response.ETag);

            // Return S3 URL
            return $"https://{_bucketName}.s3.{_settings.Aws.Region}.amazonaws.com/{key}";
        }
catch (AmazonS3Exception ex)
        {
      _logger.LogError(ex, "Failed to upload object '{Key}' to bucket '{BucketName}'", 
        $"{request.ContainerName}/{request.BlobName}", _bucketName);
 throw new InternalServerException($"Failed to upload to S3: {ex.Message}", ex);
        }
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
    var key = $"{containerName}/{blobName}";

      var request = new GetObjectRequest
        {
     BucketName = _bucketName,
      Key = key
 };

       var response = await _s3Client.GetObjectAsync(request, cancellationToken);

          // Copy to memory stream
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
      memoryStream.Position = 0;

    _logger.LogInformation(
  "Downloaded object '{Key}' from bucket '{BucketName}' ({Size} bytes)",
      key,
      _bucketName,
    memoryStream.Length);

       return memoryStream;
    }
   catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException($"Object '{containerName}/{blobName}' not found in bucket '{_bucketName}'.");
        }
catch (AmazonS3Exception ex)
    {
        _logger.LogError(ex, "Failed to download object '{Key}' from bucket '{BucketName}'", 
    $"{containerName}/{blobName}", _bucketName);
  throw new InternalServerException($"Failed to download from S3: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{containerName}/{blobName}";

          var request = new DeleteObjectRequest
        {
          BucketName = _bucketName,
                Key = key
    };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);

      _logger.LogInformation(
      "Deleted object '{Key}' from bucket '{BucketName}'",
      key,
  _bucketName);
        }
        catch (AmazonS3Exception ex)
        {
   _logger.LogError(ex, "Failed to delete object '{Key}' from bucket '{BucketName}'", 
     $"{containerName}/{blobName}", _bucketName);
       throw new InternalServerException($"Failed to delete from S3: {ex.Message}", ex);
        }
    }

public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
   try
        {
   var key = $"{containerName}/{blobName}";
   await _s3Client.GetObjectMetadataAsync(_bucketName, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return false;
        }
  catch (AmazonS3Exception ex)
 {
   _logger.LogError(ex, "Failed to check existence of object '{Key}' in bucket '{BucketName}'", 
     $"{containerName}/{blobName}", _bucketName);
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
            var key = $"{containerName}/{blobName}";

     // Check if object exists
  if (!await ExistsAsync(containerName, blobName, cancellationToken))
       {
            throw new NotFoundException($"Object '{key}' not found in bucket '{_bucketName}'.");
    }

            // Generate pre-signed URL
    var request = new GetPreSignedUrlRequest
        {
 BucketName = _bucketName,
        Key = key,
           Verb = HttpVerb.GET,
         Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
       };

  var url = _s3Client.GetPreSignedURL(request);

      return url;
  }
        catch (AmazonS3Exception ex)
  {
            _logger.LogError(ex, "Failed to get URL for object '{Key}' in bucket '{BucketName}'", 
    $"{containerName}/{blobName}", _bucketName);
     throw new InternalServerException($"Failed to get S3 URL: {ex.Message}", ex);
        }
    }

    public async Task<List<BlobModel>> ListBlobsAsync(
  string containerName, 
        string? prefix = null, 
        CancellationToken cancellationToken = default)
    {
      try
   {
            var listRequest = new ListObjectsV2Request
            {
  BucketName = _bucketName,
  Prefix = string.IsNullOrEmpty(prefix) 
          ? containerName 
       : $"{containerName}/{prefix}"
 };

            var blobs = new List<BlobModel>();

            ListObjectsV2Response response;
     do
        {
    response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

       foreach (var obj in response.S3Objects)
   {
       // Remove container prefix from key
    var blobName = obj.Key.StartsWith($"{containerName}/") 
             ? obj.Key.Substring(containerName.Length + 1) 
               : obj.Key;

              blobs.Add(new BlobModel
     {
      Name = blobName,
          ContainerName = containerName,
       Size = obj.Size,
         ContentType = "application/octet-stream", // S3 doesn't return content type in list
      LastModified = obj.LastModified,
     Url = $"https://{_bucketName}.s3.{_settings.Aws.Region}.amazonaws.com/{obj.Key}",
             ETag = obj.ETag
         });
         }

                listRequest.ContinuationToken = response.NextContinuationToken;
        }
            while (response.IsTruncated);

          _logger.LogInformation(
                "Listed {Count} objects in bucket '{BucketName}' (prefix: '{Prefix}')",
                blobs.Count,
     _bucketName,
             prefix ?? "(none)");

       return blobs;
        }
        catch (AmazonS3Exception ex)
      {
            _logger.LogError(ex, "Failed to list objects in bucket '{BucketName}'", _bucketName);
          throw new InternalServerException($"Failed to list S3 objects: {ex.Message}", ex);
      }
    }

    public Task CreateContainerAsync(string containerName, bool isPublic = false, CancellationToken cancellationToken = default)
  {
        // S3 uses folders (prefixes) instead of containers
        // No need to create container explicitly
  _logger.LogInformation(
   "Container '{ContainerName}' will be created automatically on first upload (S3 uses prefixes)",
    containerName);

        return Task.CompletedTask;
    }

    public Task DeleteContainerAsync(string containerName, CancellationToken cancellationToken = default)
    {
        // S3 uses folders (prefixes) - cannot delete prefix directly
     // Would need to delete all objects with that prefix
        _logger.LogWarning(
       "Container deletion not supported for S3 (delete all objects with prefix '{ContainerName}' instead)",
       containerName);

        return Task.CompletedTask;
    }

    public async Task<List<string>> ListContainersAsync(CancellationToken cancellationToken = default)
  {
        try
    {
            // List all unique prefixes (simulate containers)
          var listRequest = new ListObjectsV2Request
          {
    BucketName = _bucketName,
       Delimiter = "/"
            };

   var response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

      var containers = response.CommonPrefixes
                .Select(p => p.TrimEnd('/'))
       .ToList();

          _logger.LogInformation("Listed {Count} containers (prefixes)", containers.Count);

          return containers;
        }
     catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list containers in bucket '{BucketName}'", _bucketName);
throw new InternalServerException($"Failed to list S3 containers: {ex.Message}", ex);
        }
    }
}
```

**Giải thích AWS S3 Implementation:**

**Key Differences vs Azure:**
- **Container = S3 Prefix:** S3 không có "container" concept, dùng prefixes thay thế
- **Pre-signed URLs:** AWS equivalent của SAS tokens (method `GetPreSignedURL`)
- **Bucket-level:** All objects trong 1 bucket, containers là logical prefixes
- **ListObjectsV2:** Pagination với `ContinuationToken` (không giống Azure's `IAsyncEnumerable`)

**S3 Key Format:**
```
container/blobname

Examples:
products/product-123.jpg
avatars/user-456.png
documents/2024/invoice-001.pdf
```

**Pre-signed URL:**
```csharp
// Similar to Azure SAS token
var request = new GetPreSignedUrlRequest
{
    BucketName = "my-bucket",
    Key = "products/product-123.jpg",
    Verb = HttpVerb.GET,
    Expires = DateTime.UtcNow.AddMinutes(60)
};

var url = _s3Client.GetPreSignedURL(request);
// url: https://my-bucket.s3.ap-southeast-1.amazonaws.com/products/product-123.jpg?X-Amz-Algorithm=...
```

---

## 4. Configuration

### Bước 4.1: Update Startup.cs - Register AWS S3

**File:** `src/Infrastructure/Infrastructure/BlobStorage/Startup.cs`

Update method `AddBlobStorage` để support AWS:

```csharp
using Amazon;
using Amazon.S3;
using ECO.WebApi.Application.Common.BlobStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ECO.WebApi.Infrastructure.BlobStorage;

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

        // Register based on provider
        switch (settings.Provider.ToLowerInvariant())
        {
            case "azure":
      RegisterAzureBlobStorage(services, settings);
           break;

       case "aws":
        RegisterAwsS3Storage(services, settings); // ✅ Added AWS support
       break;

            case "local":
           // Use local file storage (từ BUILD_22)
                throw new NotImplementedException("Local blob storage not implemented yet. Use Azure or AWS.");

    default:
         throw new InvalidOperationException($"Unknown blob storage provider: {settings.Provider}");
        }

   return services;
  }

    private static void RegisterAzureBlobStorage(IServiceCollection services, BlobStorageSettings settings)
    {
if (string.IsNullOrEmpty(settings.AzureConnectionString))
      {
    throw new InvalidOperationException("Azure Blob Storage connection string is not configured.");
        }

        services.AddTransient<IBlobStorageService, AzureBlobStorageService>();
    }

    private static void RegisterAwsS3Storage(IServiceCollection services, BlobStorageSettings settings)
    {
        if (settings.Aws == null)
  {
          throw new InvalidOperationException("AWS S3 settings are not configured.");
     }

        // Register AWS S3 client
        services.AddAWSService<IAmazonS3>();

        // Configure AWS options
        services.AddDefaultAWSOptions(
            Amazon.Extensions.NETCore.Setup.AWSOptions.LoadFromConfiguration(
     new ConfigurationBuilder()
              .AddInMemoryCollection(new[]
            {
             new System.Collections.Generic.KeyValuePair<string, string?>(
    "AWS:Region", 
      settings.Aws.Region),
    new System.Collections.Generic.KeyValuePair<string, string?>(
     "AWS:Profile", 
  "default")
     })
           .Build()));

    services.AddTransient<IBlobStorageService, AwsS3StorageService>();
    }
}
```

**Giải thích:**
- **AddAWSService<IAmazonS3>():** Register AWS S3 client từ SDK
- **AddDefaultAWSOptions():** Configure AWS region và profile
- **IAmazonS3:** Main interface cho S3 operations

---

### Bước 4.2: appsettings.json - AWS S3 Configuration

**File:** `src/Host/Host/appsettings.json`

```json
{
  "BlobStorageSettings": {
    "Provider": "AWS",
  "Aws": {
      "AccessKey": "YOUR_AWS_ACCESS_KEY",
      "SecretKey": "YOUR_AWS_SECRET_KEY",
    "Region": "ap-southeast-1",
      "BucketName": "my-app-storage"
    },
    "DefaultContainer": "uploads",
    "DefaultPublicAccess": false
  }
}
```

**⚠️ Security Note:**
- ❌ KHÔNG commit AWS credentials vào source control
- ✅ Dùng **User Secrets** cho development:
```bash
dotnet user-secrets set "BlobStorageSettings:Aws:AccessKey" "YOUR_KEY"
dotnet user-secrets set "BlobStorageSettings:Aws:SecretKey" "YOUR_SECRET"
```
- ✅ Dùng **AWS Secrets Manager** hoặc **IAM Role** cho production (no hardcoded credentials)

---

### Bước 4.3: AWS Credentials Best Practices

**Option 1: User Secrets (Development)**
```bash
# Set secrets
dotnet user-secrets init
dotnet user-secrets set "BlobStorageSettings:Aws:AccessKey" "AKIAIOSFODNN7EXAMPLE"
dotnet user-secrets set "BlobStorageSettings:Aws:SecretKey" "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
```

**Option 2: Environment Variables (Production)**
```bash
# Linux/macOS
export AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE
export AWS_SECRET_ACCESS_KEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
export AWS_REGION=ap-southeast-1

# Windows
set AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE
set AWS_SECRET_ACCESS_KEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
set AWS_REGION=ap-southeast-1
```

**Option 3: IAM Role (Production - Recommended)**
```csharp
// No credentials needed - use IAM role attached to EC2/ECS/Lambda
services.AddAWSService<IAmazonS3>();
```

---

## 5. Key Differences: Azure vs AWS

### 5.1: Terminology Mapping

| Azure Blob Storage | AWS S3 | Notes |
|-------------------|--------|-------|
| Storage Account | Bucket | Top-level container |
| Container | Prefix/Folder | Logical grouping (S3 uses prefixes) |
| Blob | Object | Individual file |
| SAS Token | Pre-signed URL | Temporary access URL |
| Connection String | Access Key + Secret | Authentication credentials |

---

### 5.2: Container Management

**Azure:**
```csharp
// Create physical container
await containerClient.CreateIfNotExistsAsync();

// Delete container (deletes all blobs)
await containerClient.DeleteAsync();
```

**AWS S3:**
```csharp
// Containers are logical (prefixes) - no creation needed
// Upload to "products/" automatically creates prefix

// Cannot delete prefix directly - must delete all objects with prefix
var objects = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
{
    BucketName = "my-bucket",
    Prefix = "products/"
});

foreach (var obj in objects.S3Objects)
{
    await _s3Client.DeleteObjectAsync("my-bucket", obj.Key);
}
```

---

### 5.3: URL Structure

**Azure:**
```
https://{storage-account}.blob.core.windows.net/{container}/{blob}
https://mystore.blob.core.windows.net/products/product-123.jpg
```

**AWS S3:**
```
https://{bucket}.s3.{region}.amazonaws.com/{key}
https://my-bucket.s3.ap-southeast-1.amazonaws.com/products/product-123.jpg
```

---

### 5.4: Temporary Access

**Azure SAS Token:**
```csharp
var sasBuilder = new BlobSasBuilder
{
    BlobContainerName = "products",
    BlobName = "product-123.jpg",
    Resource = "b",
    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(60)
};
sasBuilder.SetPermissions(BlobSasPermissions.Read);
var sasUri = blobClient.GenerateSasUri(sasBuilder);
```

**AWS Pre-signed URL:**
```csharp
var request = new GetPreSignedUrlRequest
{
    BucketName = "my-bucket",
    Key = "products/product-123.jpg",
    Verb = HttpVerb.GET,
    Expires = DateTime.UtcNow.AddMinutes(60)
};
var url = _s3Client.GetPreSignedURL(request);
```

---

## 6. Testing AWS S3

### Bước 6.1: Test với BlobStorageController

Same controller from BUILD_24 works với AWS S3 (abstraction power!):

```bash
# Switch to AWS in appsettings.json
{
"BlobStorageSettings": {
    "Provider": "AWS",  # Changed from "Azure"
    "Aws": { ... }
  }
}

# Test upload (same API)
curl -X POST "https://localhost:7001/api/blobstorage/upload" \
  -F "containerName=test" \
  -F "file=@test-image.jpg"

# Response (S3 URL instead of Azure URL)
{
  "url": "https://my-bucket.s3.ap-southeast-1.amazonaws.com/test/abc123.jpg",
  ...
}
```

---

### Bước 6.2: Verify S3 Storage

**Option 1: AWS CLI**
```bash
# List objects in bucket
aws s3 ls s3://my-bucket/test/

# Download object
aws s3 cp s3://my-bucket/test/abc123.jpg ./downloaded.jpg
```

**Option 2: AWS Console**
```
1. Login to AWS Console
2. Navigate to S3 Service
3. Select bucket "my-bucket"
4. Browse to "test/" prefix
5. Verify uploaded file exists
```

---

## 7. Migration Guide: Azure → AWS

### Bước 7.1: Migrate Existing Blobs

**Strategy 1: Dual-write (Gradual migration)**
```csharp
// Write to both Azure and AWS during transition
public async Task<string> UploadAsync(UploadBlobRequest request, CancellationToken ct)
{
    // Upload to primary (Azure)
    var azureUrl = await _azureBlobStorage.UploadAsync(request, ct);

    // Upload to secondary (AWS) - fire-and-forget
    _ = _jobService.Enqueue<IAwsS3StorageService>(
        x => x.UploadAsync(request, default));

    return azureUrl;
}
```

**Strategy 2: Batch migration**
```csharp
public class MigrateAzureToAwsJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
{
        // List all Azure blobs
        var azureBlobs = await _azureBlobStorage.ListBlobsAsync("products");

        foreach (var blob in azureBlobs)
     {
            // Download from Azure
            var stream = await _azureBlobStorage.DownloadAsync("products", blob.Name);

            // Upload to AWS
            await _awsS3Storage.UploadAsync(new UploadBlobRequest
  {
      ContainerName = "products",
           BlobName = blob.Name,
     Data = stream,
      ContentType = blob.ContentType
         });

            _logger.LogInformation("Migrated {BlobName} from Azure to AWS", blob.Name);
        }
    }
}
```

---

## 8. Cost Comparison

### 8.1: Storage Costs (Example)

**Azure Blob Storage (Hot tier):**
- First 50 TB: $0.0184/GB/month
- Operations: $0.004 per 10,000 writes

**AWS S3 (Standard):**
- First 50 TB: $0.023/GB/month
- Operations: $0.005 per 1,000 PUT requests

**Winner:** Azure slightly cheaper for storage, AWS cheaper for writes.

**⚠️ Note:** Prices vary by region và change frequently. Check current pricing.

---

### 8.2: Transfer Costs

**Azure:**
- Egress: $0.087/GB (first 10 TB)
- Ingress: Free

**AWS:**
- Egress: $0.09/GB (first 10 TB)
- Ingress: Free

**Winner:** Similar pricing.

**Optimization:** Use CloudFront (AWS) or Azure CDN to reduce egress costs.

---

## 9. Summary

### ✅ Đã hoàn thành trong document này:

**Infrastructure Layer - AWS S3:**
- ✅ AwsS3StorageService implementation
- ✅ S3 bucket operations với prefix-based containers
- ✅ Pre-signed URL generation
- ✅ Pagination với `ListObjectsV2`

**Configuration:**
- ✅ AWS credentials configuration
- ✅ Startup registration với dynamic provider
- ✅ Security best practices (IAM roles, secrets)

**Key Learnings:**
- ✅ Container = Prefix concept trong S3
- ✅ Pre-signed URL vs SAS token
- ✅ AWS SDK integration với .NET Core
- ✅ Migration strategies Azure → AWS

---

### 📊 Architecture Comparison:

```
Azure Blob Storage         AWS S3
================         ================
Storage Account    →      Bucket
├─ Container 1    ├─ prefix-1/
│  ├─ blob-a.jpg            │  ├─ blob-a.jpg
│  └─ blob-b.jpg    │  └─ blob-b.jpg
└─ Container 2          └─ prefix-2/
   └─ blob-c.pdf             └─ blob-c.pdf

SAS Token            →   Pre-signed URL
BlobServiceClient    →  IAmazonS3
```

---

### 📁 File Structure (AWS-specific):

```
src/Infrastructure/Infrastructure/BlobStorage/
├── AzureBlobStorageService.cs (from BUILD_24)
├── AwsS3StorageService.cs (this document)
├── BlobStorageSettings.cs (supports both)
└── Startup.cs (updated with AWS registration)
```

---

## 10. Next Steps

**Quay lại:** [BUILD_24 - Blob Storage Main](BUILD_24_Blob_Storage.md)

**Tiếp theo:** [BUILD_25 - Background Jobs](BUILD_25_Background_Jobs.md)

**Advanced Topics:**
- Multi-region S3 replication
- S3 lifecycle policies (auto-delete old files)
- S3 Event Notifications (trigger Lambda on upload)
- S3 Transfer Acceleration (faster uploads)

---

**Maintained By:** ECO.WebApi Development Team  
**Last Updated:** 2026-01-30
