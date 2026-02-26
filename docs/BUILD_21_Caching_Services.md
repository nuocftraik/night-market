# Caching Services - Local và Distributed Cache

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Phase 4 (Authentication & Authorization) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Caching Services - hệ thống caching linh hoạt hỗ trợ cả Local Cache (IMemoryCache) và Distributed Cache (Redis/SQL Server).

---

## 1. Overview

**Làm gì:** Xây dựng hệ thống caching với 2 implementation:
- **Local Cache:** In-memory cache (IMemoryCache) - nhanh, chỉ trong process
- **Distributed Cache:** Redis hoặc SQL Server - chia sẻ giữa nhiều instances

**Tại sao cần:**
- **Performance:** Giảm database queries, tăng tốc response time
- **Scalability:** Distributed cache cho môi trường multi-instance (load balancing)
- **Flexibility:** Switch giữa Local/Distributed cache qua configuration
- **Cost-effective:** Local cache miễn phí, Distributed cache cho production

**Trong bước này chúng ta sẽ:**
- ✅ Tạo `ICacheService` interface (Application layer)
- ✅ Implement `LocalCacheService` (IMemoryCache)
- ✅ Implement `DistributedCacheService` (Redis/SQL Server)
- ✅ Tạo `CacheSettings` configuration
- ✅ Setup modular startup với cache registration
- ✅ Tạo JSON configuration file
- ✅ Usage examples với real-world scenarios

**Real-world example:**
```csharp
// Controller - Cache user permissions (expensive query)
public class UsersController : ControllerBase
{
    private readonly ICacheService _cache;
    private readonly IUserService _userService;
    
    public async Task<ActionResult<List<string>>> GetPermissions(string userId)
    {
 string cacheKey = $"user-permissions:{userId}";
   
        // Try get from cache first
        var permissions = await _cache.GetAsync<List<string>>(cacheKey);
        if (permissions is not null)
    return Ok(permissions); // Cache hit
        
      // Cache miss - query from database
        permissions = await _userService.GetPermissionsAsync(userId);
     
        // Store in cache for 30 minutes
        await _cache.SetAsync(cacheKey, permissions, TimeSpan.FromMinutes(30));
      
  return Ok(permissions);
    }
}
```

---

## 2. Add Required Packages

### Bước 2.1: Distributed Cache Packages

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Distributed Cache - Redis -->
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
</ItemGroup>
```

**Giải thích packages:**
- `Microsoft.Extensions.Caching.StackExchangeRedis`: Redis implementation cho IDistributedCache (production-ready, high performance)

**⚠️ Lưu ý:**
- IMemoryCache và IDistributedCache đã có sẵn trong ASP.NET Core
- Redis package chỉ cần khi sử dụng Redis làm distributed cache
- Có thể dùng SQL Server cache thay Redis (dùng package `Microsoft.Extensions.Caching.SqlServer`)

---

## 3. Application Layer - Cache Interface

### Bước 3.1: ICacheService Interface

**Làm gì:** Tạo abstraction cho caching service với tất cả operations cần thiết.

**Tại sao:** Abstraction giúp switch giữa Local/Distributed cache mà không thay đổi code.

**File:** `src/Core/Application/Common/Caching/ICacheService.cs`

```csharp
namespace ECO.WebApi.Application.Common.Caching;

/// <summary>
/// Interface for caching service
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Lấy giá trị từ cache theo key. Nếu không tìm thấy, trả về null.
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// Lấy giá trị từ cache theo key (phiên bản async). Nếu không tìm thấy, trả về null.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken token = default);

    /// <summary>
    /// Làm mới mục cache, tức là gia hạn thời gian hết hạn của mục đó.
    /// </summary>
    void Refresh(string key);

/// <summary>
    /// Làm mới mục cache (phiên bản async).
  /// </summary>
    Task RefreshAsync(string key, CancellationToken token = default);

    /// <summary>
    /// Xóa mục cache theo key.
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Xóa mục cache theo key (phiên bản async).
    /// </summary>
    Task RemoveAsync(string key, CancellationToken token = default);

    /// <summary>
    /// Lưu trữ giá trị vào cache với một thời gian hết hạn tùy chọn.
    /// </summary>
    void Set<T>(string key, T value, TimeSpan? slidingExpiration = null);

    /// <summary>
    /// Lưu trữ giá trị vào cache (phiên bản async) với một thời gian hết hạn tùy chọn.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default);
}
```

**Giải thích:**
- **Generic `<T>`:** Support mọi kiểu dữ liệu (string, int, objects, lists...)
- **Nullable `T?`:** Get methods trả về null nếu không tìm thấy
- **Sync + Async:** Cả 2 versions (Local cache sync, Distributed cache async)
- **Refresh():** Gia hạn thời gian hết hạn mà không thay đổi value
- **slidingExpiration:** Thời gian hết hạn tự động gia hạn khi truy cập

**⚠️ Sliding Expiration vs Absolute Expiration:**
- **Sliding:** Thời gian reset mỗi khi truy cập (ví dụ: 10 phút kể từ lần truy cập cuối)
- **Absolute:** Thời gian cố định (ví dụ: hết hạn lúc 12:00 bất kể có truy cập hay không)
- ECO.WebApi dùng **Sliding Expiration** để cache "hot data" lâu hơn

---

## 4. Infrastructure Layer - Local Cache Implementation

### Bước 4.1: LocalCacheService

**Làm gì:** Implement ICacheService bằng IMemoryCache (in-process cache).

**Tại sao:** 
- Nhanh nhất (không có network overhead)
- Miễn phí (không cần Redis/SQL Server)
- Phù hợp cho single-instance applications hoặc development

**File:** `src/Infrastructure/Infrastructure/Caching/LocalCacheService.cs`

```csharp
using ECO.WebApi.Application.Common.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Infrastructure.Caching;

/// <summary>
/// Local cache service using IMemoryCache (in-process cache)
/// </summary>
public class LocalCacheService : ICacheService
{
    private readonly ILogger<LocalCacheService> _logger;
    private readonly IMemoryCache _cache;

    public LocalCacheService(ILogger<LocalCacheService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Lấy giá trị từ local cache
    /// </summary>
    public T? Get<T>(string key) =>
    _cache.Get<T>(key);

    /// <summary>
    /// Lấy giá trị từ local cache (async wrapper)
    /// </summary>
    public Task<T?> GetAsync<T>(string key, CancellationToken token = default) =>
        Task.FromResult(Get<T>(key)); // IMemoryCache là đồng bộ, chỉ cần wrap trong Task

    /// <summary>
    /// Làm mới mục cache, gia hạn thời gian hết hạn cho mục đó
    /// </summary>
    public void Refresh(string key) =>
        _cache.TryGetValue(key, out _); // Kiểm tra sự tồn tại của key, nếu có thì gia hạn thời gian hết hạn

    /// <summary>
    /// Làm mới mục cache (phiên bản async)
 /// </summary>
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Refresh(key); // Gọi lại phương thức Refresh đồng bộ
        return Task.CompletedTask; // Trả về Task hoàn thành
    }

    /// <summary>
    /// Xóa mục cache theo key
    /// </summary>
    public void Remove(string key) =>
        _cache.Remove(key);

    /// <summary>
    /// Xóa mục cache (phiên bản async)
    /// </summary>
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
   Remove(key); // Gọi phương thức Remove đồng bộ
        return Task.CompletedTask; // Trả về Task hoàn thành
    }

    /// <summary>
    /// Lưu trữ giá trị vào cache với thời gian hết hạn tùy chọn
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null)
    {
     // Nếu không có giá trị thời gian hết hạn nào được chỉ định, mặc định là 10 phút
  slidingExpiration ??= TimeSpan.FromMinutes(10);

   // Đặt giá trị vào cache và cấu hình thời gian hết hạn (sliding expiration)
     _cache.Set(key, value, new MemoryCacheEntryOptions { SlidingExpiration = slidingExpiration });

   // Ghi log thông tin về việc thêm dữ liệu vào cache
        _logger.LogDebug($"Added to Cache : {key}");
    }

    /// <summary>
    /// Lưu trữ giá trị vào cache (phiên bản async)
    /// </summary>
    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken token = default)
  {
        Set(key, value, slidingExpiration); // Gọi lại phương thức Set đồng bộ
      return Task.CompletedTask; // Trả về Task hoàn thành
    }
}
```

**Giải thích:**
- **IMemoryCache:** Built-in ASP.NET Core cache, lưu trong RAM của process
- **Synchronous operations:** IMemoryCache không có async API, chỉ wrap trong Task
- **Default expiration:** 10 phút nếu không chỉ định
- **Refresh logic:** `TryGetValue()` tự động gia hạn sliding expiration
- **No error handling:** Local cache không fail (không có network issues)

**Tại sao dùng Task.FromResult():**
- Interface yêu cầu async methods
- IMemoryCache chỉ có sync API
- `Task.FromResult()` wrap kết quả đồng bộ trong Task đã complete
- Không tốn performance (không tạo thread mới)

**Lợi ích:**
- ✅ Cực kỳ nhanh (memory access)
- ✅ Không cần cài đặt gì thêm
- ✅ Hoàn hảo cho development
- ✅ Không có network latency

**⚠️ Hạn chế:**
- Không share giữa multiple instances (mỗi instance có cache riêng)
- Mất dữ liệu khi restart application
- RAM giới hạn (không thể cache quá nhiều)

---

## 5. Infrastructure Layer - Distributed Cache Implementation

### Bước 5.1: DistributedCacheService

**Làm gì:** Implement ICacheService bằng IDistributedCache (Redis/SQL Server).

**Tại sao:**
- Share cache giữa nhiều instances (load balancing)
- Persistent data (không mất khi restart)
- Scale horizontally (thêm RAM cho Redis cluster)

**File:** `src/Infrastructure/Infrastructure/Caching/DistributedCacheService.cs`

```csharp
using System.Text;
using ECO.WebApi.Application.Common.Caching;
using ECO.WebApi.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Infrastructure.Caching;

/// <summary>
/// Distributed cache service using IDistributedCache (Redis/SQL Server)
/// </summary>
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly ISerializerService _serializer;

    public DistributedCacheService(
        IDistributedCache cache, 
  ISerializerService serializer, 
  ILogger<DistributedCacheService> logger) =>
    (_cache, _serializer, _logger) = (cache, serializer, logger);

    // IDistributedCache chỉ lưu trữ dữ liệu dưới dạng mảng byte (byte[]),
    // vì vậy cần phải serialize (chuyển đối tượng thành chuỗi hoặc mảng byte để lưu)
    // và deserialize (chuyển từ mảng byte trở lại đối tượng).

    // Do Distributed Cache có thể nằm trên một máy chủ hoặc hệ thống khác (ví dụ Redis, SQL Server),
    // các thao tác với cache có thể gặp lỗi khi có vấn đề về kết nối mạng.
    // Vì vậy, cần xử lý lỗi bằng các khối try-catch để đảm bảo rằng ứng dụng không bị crash nếu có sự cố.

    /// <summary>
    /// Lấy raw byte array từ distributed cache (internal method)
 /// </summary>
 private byte[]? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

 try
 {
      return _cache.Get(key);
 }
        catch
        {
            return null; // Swallow exception - cache miss nếu có lỗi
        }
    }

    /// <summary>
    /// Lấy giá trị từ distributed cache và deserialize
    /// </summary>
    public T? Get<T>(string key) =>
        Get(key) is { } data
  ? Deserialize<T>(data)
       : default;

    /// <summary>
    /// Lấy giá trị từ distributed cache (async) và deserialize
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken token = default) =>
     await GetAsync(key, token) is { } data
   ? Deserialize<T>(data)
            : default;

    /// <summary>
    /// Lấy raw byte array từ distributed cache (async - internal method)
 /// </summary>
    private async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
     try
        {
    return await _cache.GetAsync(key, token);
        }
        catch
     {
          return null; // Swallow exception
        }
    }

    /// <summary>
    /// Làm mới thời gian hết hạn của mục cache
    /// </summary>
    public void Refresh(string key)
    {
try
        {
            _cache.Refresh(key);
        }
        catch
        {
     // Swallow exception
        }
    }

    /// <summary>
    /// Làm mới thời gian hết hạn của mục cache (async)
    /// </summary>
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
      try
 {
 await _cache.RefreshAsync(key, token);
      _logger.LogDebug(string.Format("Cache Refreshed : {0}", key));
     }
        catch
        {
      // Swallow exception
    }
    }

    /// <summary>
    /// Xóa mục cache
    /// </summary>
    public void Remove(string key)
    {
    try
        {
            _cache.Remove(key);
        }
        catch
    {
            // Swallow exception
        }
    }

    /// <summary>
    /// Xóa mục cache (async)
  /// </summary>
  public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        try
 {
            await _cache.RemoveAsync(key, token);
    }
        catch
        {
      // Swallow exception
     }
    }

 /// <summary>
    /// Lưu giá trị vào distributed cache
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null) =>
        Set(key, Serialize(value), slidingExpiration);

    /// <summary>
    /// Lưu raw byte array vào distributed cache (internal method)
    /// </summary>
    private void Set(string key, byte[] value, TimeSpan? slidingExpiration = null)
    {
        try
        {
  _cache.Set(key, value, GetOptions(slidingExpiration));
        _logger.LogDebug($"Added to Cache : {key}");
        }
        catch
   {
          // Swallow exception
        }
    }

    /// <summary>
    /// Lưu giá trị vào distributed cache (async)
  /// </summary>
    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default) =>
        SetAsync(key, Serialize(value), slidingExpiration, cancellationToken);

    /// <summary>
    /// Lưu raw byte array vào distributed cache (async - internal method)
    /// </summary>
    private async Task SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null, CancellationToken token = default)
    {
        try
      {
  await _cache.SetAsync(key, value, GetOptions(slidingExpiration), token);
        _logger.LogDebug($"Added to Cache : {key}");
        }
        catch
        {
            // Swallow exception
  }
    }

    /// <summary>
    /// Serialize object thành byte array
    /// </summary>
    private byte[] Serialize<T>(T item) =>
     Encoding.Default.GetBytes(_serializer.Serialize(item));

/// <summary>
  /// Deserialize byte array thành object
    /// </summary>
private T Deserialize<T>(byte[] cachedData) =>
        _serializer.Deserialize<T>(Encoding.Default.GetString(cachedData));

    /// <summary>
    /// Tạo cache options với sliding expiration
    /// </summary>
    private static DistributedCacheEntryOptions GetOptions(TimeSpan? slidingExpiration)
    {
    var options = new DistributedCacheEntryOptions();
        if (slidingExpiration.HasValue)
        {
     options.SetSlidingExpiration(slidingExpiration.Value);
 }
   else
  {
         // TODO: add to appsettings?
       options.SetSlidingExpiration(TimeSpan.FromMinutes(10)); // Default expiration time of 10 minutes.
     }

      return options;
    }
}
```

**Giải thích:**

**Serialization (Tại sao cần):**
- IDistributedCache chỉ làm việc với `byte[]`
- Objects phải serialize thành JSON string → bytes
- Redis không biết C# objects, chỉ biết bytes
- Flow: `Object → JSON string → bytes → Redis → bytes → JSON string → Object`

**Error Handling (Tại sao swallow exceptions):**
- Network có thể fail (Redis down, timeout, network issue)
- Cache là **không bắt buộc** - app vẫn chạy được nếu cache fail
- Swallow exceptions → cache miss → query từ database
- Không crash app vì cache issues

**Try-Catch trong mọi method:**
- Get/GetAsync: Return null nếu fail (cache miss)
- Set/SetAsync: Không lưu cache nếu fail (không sao)
- Remove/RemoveAsync: Không xóa nếu fail (không sao)
- Refresh/RefreshAsync: Không refresh nếu fail (không sao)

**Tại sao dùng ISerializerService:**
- JSON serialization (NewtonSoft.Json hoặc System.Text.Json)
- Centralized serialization logic
- Có thể config serialization settings (camelCase, ignore null...)
- Reusable trong toàn application

**Lợi ích:**
- ✅ Share cache giữa nhiều instances (perfect cho load balancing)
- ✅ Persistent (không mất khi restart)
- ✅ Scale horizontally (add more Redis nodes)
- ✅ Built-in Redis features (eviction policies, TTL, pub/sub...)

**⚠️ Lưu ý:**
- Slower than local cache (network latency)
- Cần cài đặt Redis hoặc SQL Server
- Serialization overhead (CPU cost)
- **LUÔN swallow exceptions** để tránh crash app

---

## 6. Configuration

### Bước 6.1: CacheSettings Configuration Class

**Làm gì:** Tạo configuration class để bind với JSON settings.

**Tại sao:** Type-safe configuration, validation, IntelliSense support.

**File:** `src/Infrastructure/Infrastructure/Caching/CacheSettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Caching;

/// <summary>
/// Configuration settings for caching service
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// Có sử dụng distributed cache hay không (true = Redis/SQL, false = Local)
    /// </summary>
  public bool UseDistributedCache { get; set; }

    /// <summary>
  /// Ưu tiên Redis hơn SQL Server distributed cache
    /// </summary>
    public bool PreferRedis { get; set; }

  /// <summary>
    /// Redis connection string (format: "localhost:6379")
    /// </summary>
    public string? RedisURL { get; set; }
}
```

**Giải thích:**
- **UseDistributedCache:** Switch giữa Local/Distributed cache
- **PreferRedis:** Nếu `false` sẽ dùng `DistributedMemoryCache` (local fallback cho distributed interface)
- **RedisURL:** Connection string cho Redis (ví dụ: `localhost:6379` hoặc `redis.example.com:6379,password=secret`)

**⚠️ Lưu ý:**
- `PreferRedis = false` + `UseDistributedCache = true` → Dùng DistributedMemoryCache (NOT persistent, nhưng có distributed interface)
- `PreferRedis = true` + `UseDistributedCache = true` → Dùng Redis (persistent, shared)
- `UseDistributedCache = false` → Dùng LocalCacheService (ignore PreferRedis)

---

### Bước 6.2: JSON Configuration File

**Làm gì:** Tạo configuration file cho caching settings.

**Tại sao:** External configuration, dễ dàng thay đổi mà không rebuild code.

**File:** `src/Host/Host/Configurations/cache.json`

```json
{
  "CacheSettings": {
"UseDistributedCache": false,
    "PreferRedis": true,
    "RedisURL": "localhost:6379"
}
}
```

**Giải thích:**
- **UseDistributedCache: false** → Development dùng Local Cache (không cần Redis)
- **PreferRedis: true** → Production sẽ dùng Redis khi enable distributed cache
- **RedisURL: "localhost:6379"** → Default Redis connection string

**⚠️ Cấu hình theo môi trường:**

**Development (cache.json):**
```json
{
  "CacheSettings": {
  "UseDistributedCache": false
  }
}
```

**Production (cache.Production.json):**
```json
{
  "CacheSettings": {
  "UseDistributedCache": true,
    "PreferRedis": true,
    "RedisURL": "production-redis.example.com:6379,password=your-secure-password"
  }
}
```

**Staging (cache.Staging.json):**
```json
{
  "CacheSettings": {
    "UseDistributedCache": true,
    "PreferRedis": true,
    "RedisURL": "staging-redis.example.com:6379,password=staging-password"
  }
}
```

---

## 7. Service Registration

### Bước 7.1: Caching Startup Module

**Làm gì:** Register caching services vào DI container với modular startup pattern.

**Tại sao:** Clean separation, conditional registration dựa trên configuration.

**File:** `src/Infrastructure/Infrastructure/Caching/Startup.cs`

```csharp
using ECO.WebApi.Application.Common.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Caching;

/// <summary>
/// Caching services startup module
/// </summary>
internal static class Startup
{
    /// <summary>
    /// Register caching services vào DI container
    /// </summary>
    internal static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration config)
    {
    // Bind CacheSettings từ appsettings.json
        var settings = config.GetSection(nameof(CacheSettings)).Get<CacheSettings>();
        if (settings == null)
          return services; // Không có config -> skip

      if (settings.UseDistributedCache)
        {
    // Distributed Cache Mode
        if (settings.PreferRedis)
         {
    // Setup Redis distributed cache
     services.AddStackExchangeRedisCache(options =>
      {
       options.Configuration = settings.RedisURL;
    options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions()
       {
      AbortOnConnectFail = true, // Fail fast nếu Redis không connect được
         EndPoints = { settings.RedisURL! }
  };
       });
            }
            else
     {
     // Fallback: DistributedMemoryCache (distributed interface nhưng local storage)
      services.AddDistributedMemoryCache();
         }

            // Register DistributedCacheService
  services.AddTransient<ICacheService, DistributedCacheService>();
   }
        else
        {
  // Local Cache Mode
         // Register LocalCacheService
            services.AddTransient<ICacheService, LocalCacheService>();
        }

        // Register IMemoryCache (always needed for LocalCacheService)
        services.AddMemoryCache();

        return services;
    }
}
```

**Giải thích:**

**Configuration binding:**
- `config.GetSection(nameof(CacheSettings))` → Bind `CacheSettings` section từ JSON
- Null check → Skip registration nếu không có config

**Conditional registration:**
```
IF UseDistributedCache = true
    IF PreferRedis = true
  → Register Redis + DistributedCacheService
    ELSE
        → Register DistributedMemoryCache + DistributedCacheService
ELSE
    → Register LocalCacheService
```

**Redis configuration:**
- `AbortOnConnectFail = true` → Fail fast khi startup nếu Redis down (catch issues sớm)
- `EndPoints` → Redis server address

**DistributedMemoryCache (Fallback):**
- Implement IDistributedCache interface
- Nhưng lưu local (không share giữa instances)
- Useful cho testing distributed cache code mà không cần Redis

**AddMemoryCache():**
- LUÔN register IMemoryCache
- LocalCacheService cần IMemoryCache
- Không conflict với distributed cache (có thể dùng cả 2)

**⚠️ Lưu ý:**
- Service lifetime = `Transient` (new instance mỗi request)
- Không dùng `Singleton` vì cache services có logging (inject ILogger)

---

### Bước 7.2: Register trong Infrastructure Startup

**Làm gì:** Call `AddCaching()` trong main Infrastructure startup.

**Tại sao:** Modular startup pattern - clean separation of concerns.

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

Đảm bảo có dòng này trong `AddInfrastructure()` method:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    // ... existing code ...

    // Add Caching Services
    services.AddCaching(config);

    // ... existing code ...

    return services;
}
```

**⚠️ Lưu ý:**
- Order không quan trọng (caching không depend vào services khác)
- Nhưng recommend register sớm (nhiều services khác có thể dùng cache)

---

### Bước 7.3: Load cache.json trong Program.cs

**Làm gì:** Load `cache.json` configuration file vào `IConfiguration`.

**Tại sao:** Tách biệt cache config ra file riêng, dễ quản lý.

**File:** `src/Host/Host/Program.cs`

Đảm bảo có dòng này trong `builder.Configuration`:

```csharp
builder.Configuration
    .AddJsonFile("Configurations/cache.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"Configurations/cache.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
```

**Giải thích:**
- `cache.json` → Base configuration (required)
- `cache.{Environment}.json` → Override cho môi trường cụ thể (optional)
- `reloadOnChange: true` → Hot reload khi file thay đổi

**⚠️ Lưu ý:**
- Đã có sẵn pattern này trong ECO.WebApi
- Chỉ cần đảm bảo file `cache.json` tồn tại trong `Configurations/` folder

---

## 8. Usage Examples

### Bước 8.1: Basic Cache Operations

**Scenario:** Cache user profile để giảm database queries.

**Request DTO:**
```csharp
namespace ECO.WebApi.Application.Identity.Users;

public class GetUserRequest : IRequest<UserDto>
{
    public string UserId { get; set; } = default!;
}
```

**Response DTO:**
```csharp
namespace ECO.WebApi.Application.Identity.Users;

public class UserDto
{
    public string Id { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; }
}
```

**Handler với Cache-Aside Pattern:**
```csharp
using ECO.WebApi.Application.Common.Caching;
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Domain.Identity;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Handler lấy user profile với caching
/// </summary>
public class GetUserHandler : IRequestHandler<GetUserRequest, UserDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICacheService _cache;

    public GetUserHandler(UserManager<ApplicationUser> userManager, ICacheService cache)
    {
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<UserDto> Handle(GetUserRequest request, CancellationToken cancellationToken)
    {
        // Cache key pattern: "entity:id"
        string cacheKey = $"user:{request.UserId}";

        // Try get from cache first (Cache-Aside Pattern)
    var cachedUser = await _cache.GetAsync<UserDto>(cacheKey, cancellationToken);
    if (cachedUser is not null)
        {
            return cachedUser; // Cache hit - return immediately
        }

        // Cache miss - query from database
        var user = await _userManager.FindByIdAsync(request.UserId)
      ?? throw new NotFoundException("User not found");

        // Map to DTO
        var userDto = user.Adapt<UserDto>();

  // Store in cache for 30 minutes
   await _cache.SetAsync(cacheKey, userDto, TimeSpan.FromMinutes(30), cancellationToken);

        return userDto;
    }
}
```

**Controller:**
```csharp
using ECO.WebApi.Application.Identity.Users;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers.Identity;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get user by ID (cached)
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserDto>> GetUser(string userId)
    {
        var user = await _mediator.Send(new GetUserRequest { UserId = userId });
return Ok(user);
    }
}
```

**API Call:**
```bash
# First call - Cache miss (slow - query database)
curl -X GET https://localhost:7001/api/users/550e8400-e29b-41d4-a716-446655440000

# Second call - Cache hit (fast - return from cache)
curl -X GET https://localhost:7001/api/users/550e8400-e29b-41d4-a716-446655440000
```

**Response (cả 2 calls đều giống nhau):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "userName": "john.doe",
  "email": "john.doe@example.com",
  "fullName": "John Doe",
  "isActive": true
}
```

**Giải thích Cache-Aside Pattern:**
1. **Check cache first:** Try get từ cache
2. **Cache hit:** Return ngay lập tức (fast path)
3. **Cache miss:** Query database → Cache result → Return
4. **Next request:** Cache hit (benefit from cached data)

**Performance Impact:**
- **First request:** ~100ms (database query + cache write)
- **Subsequent requests:** ~5ms (cache read only)
- **Benefit:** 20x faster cho cached requests

---

### Bước 8.2: Cache Invalidation (Remove on Update)

**Scenario:** Xóa cache khi user update profile.

**Update Request:**
```csharp
namespace ECO.WebApi.Application.Identity.Users;

public class UpdateUserRequest : IRequest<string>
{
    public string UserId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
}
```

**Handler với Cache Invalidation:**
```csharp
using ECO.WebApi.Application.Common.Caching;
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Handler update user với cache invalidation
/// </summary>
public class UpdateUserHandler : IRequestHandler<UpdateUserRequest, string>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICacheService _cache;

    public UpdateUserHandler(UserManager<ApplicationUser> userManager, ICacheService cache)
    {
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<string> Handle(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        // Get user from database
  var user = await _userManager.FindByIdAsync(request.UserId)
          ?? throw new NotFoundException("User not found");

// Update user properties
user.FullName = request.FullName;
     user.Email = request.Email;

        // Save to database
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
          throw new InternalServerException("Failed to update user");

        // IMPORTANT: Invalidate cache after update
  string cacheKey = $"user:{request.UserId}";
 await _cache.RemoveAsync(cacheKey, cancellationToken);

        // Also invalidate related caches (permissions, roles...)
        await _cache.RemoveAsync($"user-permissions:{request.UserId}", cancellationToken);

        return "User updated successfully";
    }
}
```

**Controller:**
```csharp
[HttpPut("{userId}")]
public async Task<ActionResult<string>> UpdateUser(string userId, [FromBody] UpdateUserRequest request)
{
    request.UserId = userId;
    var result = await _mediator.Send(request);
    return Ok(result);
}
```

**API Call:**
```bash
curl -X PUT https://localhost:7001/api/users/550e8400-e29b-41d4-a716-446655440000 \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "John Updated",
    "email": "john.updated@example.com"
  }'
```

**Giải thích Cache Invalidation:**
- **Why:** Database đã thay đổi, cache cũ không còn valid
- **When:** Sau khi save database thành công
- **How:** Remove cache key → next request sẽ cache miss → query mới từ database
- **Related keys:** Xóa cả related caches (permissions, roles...) để tránh stale data

**⚠️ Cache Invalidation Patterns:**
1. **Remove on Update:** Xóa cache, next request sẽ rebuild (recommended)
2. **Update Cache:** Update cache trực tiếp (complex, error-prone)
3. **TTL Only:** Để cache tự expire (stale data trong TTL window)

---

### Bước 8.3: Complex Cache Key (User Permissions)

**Scenario:** Cache user permissions (expensive query - join nhiều bảng).

**GetPermissionsRequest:**
```csharp
namespace ECO.WebApi.Application.Identity.Users;

public class GetUserPermissionsRequest : IRequest<List<string>>
{
    public string UserId { get; set; } = default!;
}
```

**Handler với Complex Caching:**
```csharp
using ECO.WebApi.Application.Common.Caching;
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Handler lấy user permissions với caching (expensive query)
/// </summary>
public class GetUserPermissionsHandler : IRequestHandler<GetUserPermissionsRequest, List<string>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICacheService _cache;

    public GetUserPermissionsHandler(UserManager<ApplicationUser> userManager, ICacheService cache)
    {
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<List<string>> Handle(GetUserPermissionsRequest request, CancellationToken cancellationToken)
    {
  // Cache key với prefix descriptive
        string cacheKey = $"user-permissions:{request.UserId}";

        // Try cache first
        var cachedPermissions = await _cache.GetAsync<List<string>>(cacheKey, cancellationToken);
        if (cachedPermissions is not null)
    {
         return cachedPermissions;
    }

    // Cache miss - expensive query (join User -> Roles -> RoleClaims)
        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException("User not found");

 var roles = await _userManager.GetRolesAsync(user);

        // Query permissions từ RoleClaims (join nhiều bảng)
    var permissions = new List<string>();
        foreach (var roleName in roles)
        {
          // This would be a complex query in real implementation
            // Joining ApplicationRole -> ApplicationRoleClaim -> Permission
  // Simplified here for example
            var rolePermissions = await GetRolePermissionsAsync(roleName, cancellationToken);
        permissions.AddRange(rolePermissions);
        }

  // Remove duplicates
        permissions = permissions.Distinct().ToList();

        // Cache for 60 minutes (permissions don't change often)
        await _cache.SetAsync(cacheKey, permissions, TimeSpan.FromMinutes(60), cancellationToken);

   return permissions;
    }

    private async Task<List<string>> GetRolePermissionsAsync(string roleName, CancellationToken cancellationToken)
    {
        // Placeholder - actual implementation would query RoleClaims
      // Example: SELECT Permission FROM RoleClaims WHERE RoleName = @roleName
 return new List<string> { "Users.View", "Users.Create", "Products.View" };
    }
}
```

**Controller:**
```csharp
[HttpGet("{userId}/permissions")]
public async Task<ActionResult<List<string>>> GetPermissions(string userId)
{
    var permissions = await _mediator.Send(new GetUserPermissionsRequest { UserId = userId });
 return Ok(permissions);
}
```

**API Call:**
```bash
curl -X GET https://localhost:7001/api/users/550e8400-e29b-41d4-a716-446655440000/permissions
```

**Response:**
```json
[
  "Users.View",
  "Users.Create",
  "Users.Update",
  "Users.Delete",
  "Products.View",
  "Products.Create"
]
```

**Giải thích:**

**Why cache permissions:**
- Expensive query (join 3-4 tables: User → UserRoles → Roles → RoleClaims → Permissions)
- Checked frequently (mỗi API call check permissions)
- Rarely changes (permissions update không thường xuyên)

**Cache TTL considerations:**
- **60 minutes:** Balance giữa freshness và performance
- **Longer TTL:** Nếu permissions ít thay đổi
- **Shorter TTL:** Nếu cần real-time updates
- **Invalidate on change:** Remove cache khi assign/revoke roles

**Cache key pattern:**
- `user-permissions:{userId}` → Descriptive, easy to invalidate
- **NOT** `perms:{userId}` → Too short, unclear
- **NOT** `{userId}:permissions` → Inconsistent with other keys

---

### Bước 8.4: Cache with Pagination (Product List)

**Scenario:** Cache product list với pagination.

**SearchProductsRequest:**
```csharp
namespace ECO.WebApi.Application.Catalog.Products;

public class SearchProductsRequest : IRequest<PaginatedResult<ProductDto>>
{
    public string? Keyword { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
```

**Handler với Pagination Caching:**
```csharp
using ECO.WebApi.Application.Common.Caching;
using ECO.WebApi.Application.Common.Models;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Handler search products với caching per page
/// </summary>
public class SearchProductsHandler : IRequestHandler<SearchProductsRequest, PaginatedResult<ProductDto>>
{
  private readonly IProductRepository _repository;
    private readonly ICacheService _cache;

    public SearchProductsHandler(IProductRepository repository, ICacheService cache)
  {
        _repository = repository;
 _cache = cache;
    }

    public async Task<PaginatedResult<ProductDto>> Handle(SearchProductsRequest request, CancellationToken cancellationToken)
    {
        // Cache key bao gồm tất cả query parameters
        string cacheKey = $"products:search:" +
           $"keyword={request.Keyword ?? "all"}:" +
     $"page={request.PageNumber}:" +
   $"size={request.PageSize}";

 // Try cache
        var cachedResult = await _cache.GetAsync<PaginatedResult<ProductDto>>(cacheKey, cancellationToken);
     if (cachedResult is not null)
 {
       return cachedResult;
        }

        // Cache miss - query database
        var spec = new ProductsBySearchSpec(request);
        var products = await _repository.ListAsync(spec, cancellationToken);
     var count = await _repository.CountAsync(spec, cancellationToken);

      var result = new PaginatedResult<ProductDto>(products, count, request.PageNumber, request.PageSize);

        // Cache for 15 minutes (product list changes frequently)
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), cancellationToken);

     return result;
    }
}
```

**Controller:**
```csharp
[HttpPost("search")]
public async Task<ActionResult<PaginatedResult<ProductDto>>> Search([FromBody] SearchProductsRequest request)
{
    var result = await _mediator.Send(request);
    return Ok(result);
}
```

**API Call:**
```bash
curl -X POST https://localhost:7001/api/products/search \
  -H "Content-Type: application/json" \
  -d '{
    "keyword": "laptop",
    "pageNumber": 1,
    "pageSize": 10
  }'
```

**Response:**
```json
{
  "data": [
    {
      "id": "product-1",
      "name": "Dell Laptop",
 "price": 999.99
    }
  ],
  "currentPage": 1,
"totalPages": 5,
  "totalCount": 50,
  "pageSize": 10,
  "hasPrevious": false,
  "hasNext": true
}
```

**Giải thích Pagination Caching:**

**Cache key design:**
- Include ALL query parameters: `keyword`, `page`, `size`
- Each combination → different cache key
- Example:
  - `products:search:keyword=laptop:page=1:size=10` → Cache entry 1
  - `products:search:keyword=laptop:page=2:size=10` → Cache entry 2
  - `products:search:keyword=phone:page=1:size=10` → Cache entry 3

**Cache invalidation for pagination:**
```csharp
// When product created/updated/deleted → invalidate ALL product caches
public async Task InvalidateProductCaches()
{
    // Cannot remove by pattern in IDistributedCache (Redis limitation)
  // Option 1: Use Redis directly with pattern matching
    // Option 2: Track cache keys in separate set
    // Option 3: Use cache versioning (add version to key)
    
    // Simple approach: Let cache expire naturally (TTL = 15 minutes)
    // For critical updates: Use SignalR to notify clients
}
```

**⚠️ Pagination Caching Considerations:**
- **Short TTL:** Product list changes frequently (15 minutes)
- **Cache explosion:** Nhiều combinations → nhiều cache entries
- **Memory limit:** Monitor cache size, evict least used entries
- **Invalidation challenge:** Không thể xóa by pattern (need workarounds)

---

### Bước 8.5: Cache Refresh Pattern

**Scenario:** Refresh cache trước khi expire (proactive caching).

**Background Job (Hangfire):**
```csharp
using ECO.WebApi.Application.Common.Caching;
using ECO.WebApi.Application.Catalog.Products;
using Hangfire;
using MediatR;

namespace ECO.WebApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job để refresh product cache định kỳ
/// </summary>
public class ProductCacheRefreshJob
{
    private readonly IMediator _mediator;
    private readonly ICacheService _cache;

    public ProductCacheRefreshJob(IMediator mediator, ICacheService cache)
    {
      _mediator = mediator;
     _cache = cache;
    }

    /// <summary>
    /// Refresh cache cho top products (chạy mỗi 10 phút)
  /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task RefreshTopProductsCache()
  {
        // Query top 20 products
        var request = new SearchProductsRequest
        {
  PageNumber = 1,
          PageSize = 20
};

        var result = await _mediator.Send(request);

        // Force refresh cache (update even if exists)
        string cacheKey = "products:top20";
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));

        // Refresh cache expiration (keep hot data in cache)
    await _cache.RefreshAsync(cacheKey);
    }

    /// <summary>
    /// Schedule job khi application startup
    /// </summary>
    public static void Schedule()
    {
        // Run every 10 minutes
        RecurringJob.AddOrUpdate<ProductCacheRefreshJob>(
            "refresh-top-products-cache",
         job => job.RefreshTopProductsCache(),
          "*/10 * * * *"); // Cron expression: every 10 minutes
    }
}
```

**Register trong Program.cs:**
```csharp
// After app.UseHangfireDashboard()
ProductCacheRefreshJob.Schedule();
```

**Giải thích Cache Refresh:**

**Why proactive refresh:**
- Avoid "cold cache" scenario (first user pays latency cost)
- Always have fresh data ready
- Smooth performance (no sudden slow requests)

**When to use:**
- High-traffic endpoints (homepage, top products, popular categories)
- Expensive queries (complex joins, aggregations)
- Predictable access patterns

**Refresh vs Set:**
- `Refresh()`: Chỉ gia hạn TTL (data không đổi)
- `Set()`: Update cả data và TTL
- Combine: `Set()` new data + `Refresh()` để extend TTL

**⚠️ Lưu ý:**
- Không abuse (quá nhiều refresh jobs → waste resources)
- Monitor cache hit rate (nếu hit rate thấp → không cần refresh)
- Balance: Refresh frequency vs Query cost

---

## 9. Advanced Patterns

### 9.1: Cache Versioning (Handle Schema Changes)

**Problem:** Cached data có old schema, code expect new schema → deserialization error.

**Solution:** Version trong cache key.

```csharp
public class UserService
{
private const int CACHE_VERSION = 2; // Increment khi schema changes
    private readonly ICacheService _cache;

    public async Task<UserDto> GetUserAsync(string userId)
    {
        // Cache key bao gồm version
        string cacheKey = $"user:v{CACHE_VERSION}:{userId}";

        var cachedUser = await _cache.GetAsync<UserDto>(cacheKey);
     if (cachedUser is not null)
        return cachedUser;

        // ... query database ...

        await _cache.SetAsync(cacheKey, userDto, TimeSpan.FromMinutes(30));
   return userDto;
    }
}
```

**Benefits:**
- ✅ Old cache tự động invalid (different key)
- ✅ No deserialization errors
- ✅ Gradual migration (old cache expire tự nhiên)

---

### 9.2: Cache Warming (Startup)

**Problem:** Slow first requests (cache miss).

**Solution:** Pre-populate cache khi application startup.

```csharp
public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly ICacheService _cache;
    private readonly IProductRepository _productRepository;

    public async Task InitializeApplicationDbForTenantAsync()
    {
     // ... existing initialization ...

        // Warm cache with top products
        await WarmProductCacheAsync();
    }

    private async Task WarmProductCacheAsync()
    {
        var topProducts = await _productRepository.GetTopProductsAsync(50);
      await _cache.SetAsync("products:top50", topProducts, TimeSpan.FromHours(1));

        // Warm categories cache
    var categories = await _productRepository.GetAllCategoriesAsync();
        await _cache.SetAsync("categories:all", categories, TimeSpan.FromHours(1));
    }
}
```

**⚠️ Lưu ý:**
- Chỉ warm critical data (homepage, navigation...)
- Không warm quá nhiều (slow startup)
- Use background job nếu data lớn

---

### 9.3: Conditional Caching (Cache only if...)

**Scenario:** Chỉ cache nếu query success và có data.

```csharp
public async Task<List<ProductDto>> SearchProductsAsync(SearchRequest request)
{
    string cacheKey = $"products:search:{request.Keyword}";

    var cached = await _cache.GetAsync<List<ProductDto>>(cacheKey);
    if (cached is not null)
        return cached;

    var products = await _repository.SearchAsync(request);

    // Chỉ cache nếu có kết quả
    if (products.Any())
    {
        await _cache.SetAsync(cacheKey, products, TimeSpan.FromMinutes(15));
    }

    return products;
}
```

**Why:**
- Không cache empty results (waste memory)
- Không cache errors (sẽ fix sau, không muốn cache error)

---

### 9.4: Multi-Level Caching (L1 + L2)

**Scenario:** Local cache (L1) + Distributed cache (L2) cho optimal performance.

```csharp
public class MultiLevelCacheService : ICacheService
{
    private readonly IMemoryCache _l1Cache; // Fast, local
    private readonly IDistributedCache _l2Cache; // Shared, distributed
    private readonly ISerializerService _serializer;

    public async Task<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        // Check L1 cache first (fastest)
        if (_l1Cache.TryGetValue(key, out T? cachedValue))
 return cachedValue;

        // Check L2 cache (distributed)
        var l2Data = await _l2Cache.GetAsync(key, token);
        if (l2Data is not null)
 {
        var value = _serializer.Deserialize<T>(Encoding.UTF8.GetString(l2Data));

            // Store in L1 cache for faster access next time
         _l1Cache.Set(key, value, TimeSpan.FromMinutes(5)); // Shorter TTL for L1

    return value;
        }

      return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken token = default)
    {
        // Set L1 cache (fast access)
        _l1Cache.Set(key, value, TimeSpan.FromMinutes(5));

  // Set L2 cache (shared across instances)
  var bytes = Encoding.UTF8.GetBytes(_serializer.Serialize(value));
 await _l2Cache.SetAsync(key, bytes, GetOptions(slidingExpiration), token);
    }
}
```

**Benefits:**
- ✅ L1 cache: Ultra fast (memory), short TTL
- ✅ L2 cache: Shared, longer TTL
- ✅ Best of both worlds

**⚠️ Complexity:**
- Harder to invalidate (2 caches)
- Memory usage (duplicate data)
- Useful cho high-traffic scenarios only

---

## 10. Best Practices & Guidelines

### 10.1: Cache Key Naming Conventions

**✅ Good:**
```csharp
"user:{userId}"         // Entity:ID
"user-permissions:{userId}"        // Entity-Related:ID
"products:search:keyword={keyword}:page={page}" // Query with params
"categories:all"        // Collection
"config:app-settings"         // Configuration
```

**❌ Bad:**
```csharp
"{userId}"     // No context
"u:{userId}"         // Too short
"GetUserPermissions_{userId}"  // Method name in key
"user-{userId}-permissions"  // Inconsistent separator
```

**Rules:**
- Use `:` to separate entity and ID
- Use `-` for multi-word entities
- Lowercase everything
- Include version if schema changes
- Include all query params for searches

---

### 10.2: Cache TTL Guidelines

**Entity-based caching:**
- **User profile:** 30 minutes (moderate changes)
- **User permissions:** 60 minutes (rare changes)
- **Product details:** 15 minutes (frequent updates)
- **Categories:** 1 hour (very rare changes)
- **Configuration:** 24 hours (almost never changes)

**Query-based caching:**
- **Product search:** 10-15 minutes (frequent inventory changes)
- **Hot products:** 5 minutes (real-time feel)
- **Reports/Analytics:** 1 hour (acceptable staleness)

**Rule of thumb:**
- Frequently changing data: 5-15 minutes
- Occasionally changing: 30-60 minutes
- Rarely changing: 1-24 hours
- Static/Config: Days or manual invalidation

---

### 10.3: When NOT to Cache

**❌ Không cache:**
- **Sensitive data:** Passwords, tokens, PII (personal identifiable information)
- **One-time data:** OTP codes, password reset tokens
- **Real-time data:** Stock prices, live scores (nếu cần real-time)
- **Large objects:** Files, images (dùng blob storage)
- **Audit data:** Audit trails, logs (phải chính xác 100%)

**❌ Không cache nếu:**
- Query đã đủ nhanh (<50ms)
- Data thay đổi liên tục (vô nghĩa)
- Cache hit rate thấp (<30%)
- Memory/Redis cost > query cost

---

### 10.4: Cache Invalidation Strategies

**1. Time-based (TTL):**
```csharp
await _cache.SetAsync(key, value, TimeSpan.FromMinutes(30));
```
- ✅ Simple, automatic
- ❌ Stale data trong TTL window

**2. Event-based (Manual invalidation):**
```csharp
// On update
await _cache.RemoveAsync($"user:{userId}");
```
- ✅ Always fresh data
- ❌ Complex, error-prone (forget to invalidate)

**3. Hybrid (TTL + Event-based):**
```csharp
// TTL as backup, manual invalidation for immediate updates
await _cache.SetAsync(key, value, TimeSpan.FromHours(1));
await _cache.RemoveAsync(key); // On explicit update
```
- ✅ Best of both worlds
- ✅ Recommended approach

**4. Cache Versioning:**
```csharp
string key = $"user:v{VERSION}:{userId}";
```
- ✅ Schema changes safe
- ❌ Memory waste (old versions linger until TTL)

---

### 10.5: Monitoring & Metrics

**Key metrics to track:**
- **Cache hit rate:** `Hits / (Hits + Misses) * 100%`
  - Target: >70% hit rate
  - <50% hit rate → Investigate (wrong TTL? wrong data?)

- **Cache latency:**
  - Local cache: <1ms
  - Redis: 1-5ms
  - >10ms → Network issue or Redis overload

- **Cache size:** Monitor memory usage
  - Local cache: Don't exceed 20% of app memory
  - Redis: Monitor eviction rate

- **Cache errors:** Network failures, serialization errors
  - Should be rare (<0.1%)
  - Spike → Investigate Redis connection

**Logging examples:**
```csharp
_logger.LogInformation("Cache hit: {CacheKey}", cacheKey);
_logger.LogWarning("Cache miss: {CacheKey}", cacheKey);
_logger.LogError("Cache error: {CacheKey}, {Error}", cacheKey, ex.Message);
```

---

### 10.6: Security Considerations

**❌ Không cache sensitive data:**
```csharp
// BAD - Don't do this
await _cache.SetAsync($"user-password:{userId}", hashedPassword);
await _cache.SetAsync($"jwt-token:{userId}", token);
```

**✅ Cache non-sensitive data only:**
```csharp
// GOOD
await _cache.SetAsync($"user-profile:{userId}", userDto); // Public profile only
await _cache.SetAsync($"user-permissions:{userId}", permissions); // Not PII
```

**Cache key security:**
- Don't include sensitive data in keys (logged, visible in Redis)
- Use hashed IDs if needed: `$"user:{userId.GetHashCode()}"` (giảm readability nhưng tăng security)

**Redis security:**
- Use password authentication in production
- Enable TLS/SSL for Redis connection
- Restrict Redis port (không expose ra internet)

---

## 11. Testing Caching

### 11.1: Unit Test với Mock Cache

```csharp
using ECO.WebApi.Application.Common.Caching;
using Moq;
using Xunit;

namespace ECO.WebApi.Application.Tests.Identity.Users;

public class GetUserHandlerTests
{
    private readonly Mock<ICacheService> _mockCache;
 private readonly Mock<IUserRepository> _mockRepository;
    private readonly GetUserHandler _handler;

    public GetUserHandlerTests()
    {
        _mockCache = new Mock<ICacheService>();
        _mockRepository = new Mock<IUserRepository>();
  _handler = new GetUserHandler(_mockRepository.Object, _mockCache.Object);
    }

    [Fact]
 public async Task Handle_CacheHit_ShouldReturnCachedUser()
    {
        // Arrange
var userId = "user-123";
        var cachedUser = new UserDto { Id = userId, UserName = "john.doe" };

      _mockCache
 .Setup(x => x.GetAsync<UserDto>($"user:{userId}", It.IsAny<CancellationToken>()))
        .ReturnsAsync(cachedUser);

      var request = new GetUserRequest { UserId = userId };

    // Act
    var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
      Assert.Equal(cachedUser, result);
        _mockRepository.Verify(x => x.GetByIdAsync(userId), Times.Never); // Repository not called
    }

    [Fact]
    public async Task Handle_CacheMiss_ShouldQueryDatabaseAndCache()
  {
        // Arrange
   var userId = "user-123";
 var user = new ApplicationUser { Id = userId, UserName = "john.doe" };

      _mockCache
    .Setup(x => x.GetAsync<UserDto>($"user:{userId}", It.IsAny<CancellationToken>()))
      .ReturnsAsync((UserDto?)null); // Cache miss

    _mockRepository
         .Setup(x => x.GetByIdAsync(userId))
      .ReturnsAsync(user);

   var request = new GetUserRequest { UserId = userId };

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
   Assert.NotNull(result);
        _mockCache.Verify(x => x.SetAsync(
        $"user:{userId}",
            It.IsAny<UserDto>(),
      It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

### 11.2: Integration Test với Real Cache

```csharp
using ECO.WebApi.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECO.WebApi.Infrastructure.Tests.Caching;

public class LocalCacheServiceTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly LocalCacheService _cacheService;

    public LocalCacheServiceTests()
    {
      _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockLogger = new Mock<ILogger<LocalCacheService>>();
   _cacheService = new LocalCacheService(mockLogger.Object, _memoryCache);
    }

    [Fact]
    public async Task SetAndGet_ShouldWork()
    {
      // Arrange
        var key = "test-key";
        var value = new TestObject { Name = "Test", Value = 123 };

        // Act
        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(10));
        var result = await _cacheService.GetAsync<TestObject>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
 Assert.Equal(value.Value, result.Value);
    }

    [Fact]
    public async Task Remove_ShouldDeleteCacheEntry()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        await _cacheService.SetAsync(key, value);

      // Act
        await _cacheService.RemoveAsync(key);
        var result = await _cacheService.GetAsync<string>(key);

   // Assert
     Assert.Null(result);
    }

    [Fact]
    public async Task Expiration_ShouldWork()
    {
      // Arrange
  var key = "test-key";
  var value = "test-value";
        await _cacheService.SetAsync(key, value, TimeSpan.FromMilliseconds(100));

// Act
        await Task.Delay(200); // Wait for expiration
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    private class TestObject
    {
      public string Name { get; set; } = default!;
        public int Value { get; set; }
    }
}
```

---

## 12. Troubleshooting

### Problem 1: Redis Connection Failed

**Error:**
```
StackExchange.Redis.RedisConnectionException: It was not possible to connect to the redis server(s).
```

**Solution:**
```bash
# Check Redis is running
redis-cli ping
# Expected: PONG

# Check connection string in cache.json
{
  "CacheSettings": {
    "RedisURL": "localhost:6379" // Correct format
  }
}

# Test Redis connection
redis-cli -h localhost -p 6379
> ping
PONG
```

---

### Problem 2: Serialization Error

**Error:**
```
Newtonsoft.Json.JsonSerializationException: Self referencing loop detected
```

**Solution:**
```csharp
// Fix circular reference in entity
public class Product
{
    public Category Category { get; set; } // Circular reference

    // Solution: Use DTO instead of entity
}

// Or configure JsonSerializerSettings to ignore loops
services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    });
```

---

### Problem 3: Cache Not Invalidating

**Symptom:** Stale data vẫn return sau khi update.

**Solution:**
```csharp
// Ensure RemoveAsync được gọi AFTER database update success
public async Task<string> UpdateUserAsync(UpdateUserRequest request)
{
    // Update database
    await _repository.UpdateAsync(user);
    await _unitOfWork.SaveChangesAsync(); // IMPORTANT: Save first

    // Then invalidate cache
    await _cache.RemoveAsync($"user:{user.Id}");

    return "Success";
}
```

---

### Problem 4: High Memory Usage (Local Cache)

**Symptom:** Application memory tăng cao.

**Solution:**
```csharp
// Configure IMemoryCache size limit
services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // Limit 1024 entries
});

// Set size cho mỗi cache entry
_cache.Set(key, value, new MemoryCacheEntryOptions
{
    Size = 1, // Entry size
    SlidingExpiration = TimeSpan.FromMinutes(10)
});
```

---

### Problem 5: Cache Hit Rate Too Low

**Symptom:** Cache hit rate <50%.

**Investigation:**
```csharp
// Add logging để track hits/misses
public async Task<T?> GetAsync<T>(string key)
{
    var value = await _cache.GetAsync<T>(key);
    if (value is not null)
    {
        _logger.LogInformation("Cache HIT: {Key}", key);
    }
    else
    {
        _logger.LogWarning("Cache MISS: {Key}", key);
 }
    return value;
}
```

**Solutions:**
- Increase TTL (data expire quá nhanh)
- Fix cache key generation (inconsistent keys)
- Check if data được query nhiều lần (not cacheable)

---

## 13. Summary

### ✅ Đã hoàn thành trong bước này:

**Application Layer:**
- ✅ `ICacheService` interface với tất cả operations (Get, Set, Remove, Refresh)

**Infrastructure Layer:**
- ✅ `LocalCacheService` implementation (IMemoryCache)
- ✅ `DistributedCacheService` implementation (Redis/SQL Server)
- ✅ `CacheSettings` configuration class
- ✅ Modular startup với conditional registration

**Configuration:**
- ✅ `cache.json` configuration file
- ✅ Environment-specific overrides support

**Patterns & Best Practices:**
- ✅ Cache-Aside Pattern
- ✅ Cache invalidation strategies
- ✅ Pagination caching
- ✅ Multi-level caching
- ✅ Cache versioning
- ✅ Proactive cache refresh

---

### 📊 Caching Architecture:

```
Application Layer (Abstraction)
    │
    ├── ICacheService (interface)
    │   ├── Get<T>(key)
    │   ├── Set<T>(key, value, ttl)
    │   ├── Remove(key)
    │   └── Refresh(key)
    │
Infrastructure Layer (Implementation)
    │
    ├── LocalCacheService (IMemoryCache)
    │   ├── Fast (memory access)
    │   ├── Single instance only
 │   └── No persistence
    │
    └── DistributedCacheService (Redis/SQL)
        ├── Shared across instances
        ├── Persistent
   ├── Serialization required
 └── Network latency

Configuration
    │
 └── cache.json
        ├── UseDistributedCache (true/false)
        ├── PreferRedis (true/false)
        └── RedisURL (connection string)
```

---

### 📌 Key Concepts:

**Cache-Aside Pattern (Recommended):**
1. Check cache first
2. If hit → return cached data
3. If miss → query database → cache result → return
4. Next request → cache hit

**Cache Invalidation:**
- **Remove on Update:** Xóa cache khi update entity (recommended)
- **Time-based (TTL):** Để cache tự expire
- **Event-based:** Xóa cache khi có events (domain events)
- **Hybrid:** TTL + Manual remove (best practice)

**Local vs Distributed:**
- **Local Cache:** Fast, single-instance, development
- **Distributed Cache:** Shared, scalable, production
- **Switch via configuration:** No code changes needed

**Cache Key Naming:**
- Pattern: `entity:id` hoặc `entity-related:id`
- Include all query params cho searches
- Version nếu schema changes
- Lowercase, consistent separators

**TTL Guidelines:**
- Frequently changing: 5-15 minutes
- Occasionally changing: 30-60 minutes
- Rarely changing: 1-24 hours
- Static: Days or manual invalidation

---

### 📁 File Structure:

```
src/Core/Application/Common/Caching/
└── ICacheService.cs

src/Infrastructure/Infrastructure/Caching/
├── CacheSettings.cs
├── LocalCacheService.cs
├── DistributedCacheService.cs
└── Startup.cs

src/Host/Host/Configurations/
├── cache.json
├── cache.Development.json
└── cache.Production.json
```

---

## 14. Next Steps

**Tiếp theo:** [BUILD_20 - File Storage](BUILD_20_File_Storage.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo `IFileStorageService` interface
2. ✅ Implement `LocalFileStorageService` (disk storage)
3. ✅ File upload/download/delete operations
4. ✅ File validation (size, extension, content-type)
5. ✅ Integration với Controllers (multipart/form-data)

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
