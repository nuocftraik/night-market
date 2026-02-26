# Background Jobs với Hangfire - Fire-and-Forget, Delayed & Recurring Jobs

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)
> 📋 **Prerequisites:** Bước 24 (Blob Storage) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Background Jobs Service với Hangfire - để xử lý các tác vụ nền như gửi email, generate reports, cleanup tasks.

---

## 1. Overview

**Làm gì:** Xây dựng background job processing system với Hangfire để xử lý async tasks không block main request.

**Tại sao cần:**
- **Performance:** Không block HTTP requests với long-running tasks
- **Reliability:** Jobs retry tự động khi failed, persistent storage
- **Scalability:** Distributed processing với multiple workers
- **Monitoring:** Dashboard để track job status, failures, retries
- **Scheduling:** Cron-based recurring jobs (daily backups, cleanup tasks)

**Trong bước này chúng ta sẽ:**
- ✅ Setup Hangfire với SQL Server storage
- ✅ Tạo IJobService interface
- ✅ Implement HangfireService
- ✅ Fire-and-forget jobs (gửi email ngay lập tức)
- ✅ Delayed jobs (reminder sau 1 giờ)
- ✅ Recurring jobs (daily cleanup at 2 AM)
- ✅ Setup Hangfire Dashboard
- ✅ Job filtering và error handling

**Real-world example:**
```csharp
// Fire-and-forget: Gửi welcome email (không block registration request)
var jobId = await _jobService.EnqueueAsync<IMailService>(
    x => x.SendAsync(welcomeEmailRequest, default));

// Delayed: Gửi reminder sau 24 giờ
await _jobService.ScheduleAsync<INotificationService>(
    x => x.SendReminderAsync(userId, default),
    TimeSpan.FromHours(24));

// Recurring: Cleanup temp files mỗi ngày lúc 2 AM
_jobService.AddOrUpdateRecurringJob<ICleanupService>(
    "cleanup-temp-files",
    x => x.CleanupTempFilesAsync(default),
    Cron.Daily(2));
```

---

## 2. Add Required Packages

### Bước 2.1: Add Hangfire Packages

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
  <!-- Hangfire Core -->
    <PackageReference Include="Hangfire.Core" Version="1.8.9" />
    <PackageReference Include="Hangfire.SqlServer" Version="1.8.9" />
    <PackageReference Include="Hangfire.AspNetCore" Version="1.8.9" />
    
    <!-- Hangfire Extensions -->
    <PackageReference Include="Hangfire.Console" Version="1.4.2" />
    <PackageReference Include="Hangfire.Console.Extensions" Version="1.0.5" />
</ItemGroup>
```

**Giải thích packages:**
- `Hangfire.Core`: Core library cho job processing
- `Hangfire.SqlServer`: SQL Server storage provider (persistent jobs)
- `Hangfire.AspNetCore`: ASP.NET Core integration và Dashboard
- `Hangfire.Console`: Console logging trong dashboard
- `Hangfire.Console.Extensions`: Enhanced console với progress bars

**⚠️ Lưu ý:**
- Version 1.8.x là latest stable (compatible với .NET 8)
- SQL Server storage recommended cho production (Redis cũng OK)
- Dashboard require authentication trong production

---

## 3. Application Layer - Interfaces

### Bước 3.1: Tạo IJobService Interface

**Làm gì:** Định nghĩa contract cho job scheduling operations.

**Tại sao:** Abstraction để không phụ thuộc trực tiếp vào Hangfire (có thể switch sang Quartz.NET nếu cần).

**File:** `src/Core/Application/Common/BackgroundJobs/IJobService.cs`

```csharp
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ECO.WebApi.Application.Common.BackgroundJobs;

/// <summary>
/// Service for background job scheduling and management
/// </summary>
public interface IJobService : ITransientService
{
    /// <summary>
    /// Enqueue job to run immediately in background (Fire-and-Forget)
    /// </summary>
    /// <typeparam name="T">Service type to invoke</typeparam>
    /// <param name="methodCall">Method to call on service</param>
    /// <returns>Job ID</returns>
    string Enqueue<T>(Expression<Action<T>> methodCall);

    /// <summary>
    /// Enqueue async job to run immediately in background (Fire-and-Forget)
    /// </summary>
    string Enqueue<T>(Expression<Func<T, Task>> methodCall);

    /// <summary>
    /// Schedule job to run after delay (Delayed Job)
 /// </summary>
    string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay);

    /// <summary>
    /// Schedule async job to run after delay (Delayed Job)
    /// </summary>
    string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay);

    /// <summary>
    /// Schedule job to run at specific time
    /// </summary>
    string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt);

    /// <summary>
    /// Schedule async job to run at specific time
    /// </summary>
    string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt);

    /// <summary>
    /// Add or update recurring job (Cron-based scheduling)
    /// </summary>
    void AddOrUpdateRecurringJob<T>(
        string jobId,
   Expression<Action<T>> methodCall,
  string cronExpression,
        TimeZoneInfo? timeZone = null);

    /// <summary>
    /// Add or update async recurring job
    /// </summary>
    void AddOrUpdateRecurringJob<T>(
  string jobId,
        Expression<Func<T, Task>> methodCall,
    string cronExpression,
        TimeZoneInfo? timeZone = null);

    /// <summary>
    /// Remove recurring job
    /// </summary>
    void RemoveRecurringJob(string jobId);

/// <summary>
    /// Trigger recurring job to run immediately
    /// </summary>
    void TriggerRecurringJob(string jobId);

    /// <summary>
    /// Delete job from queue
    /// </summary>
    bool Delete(string jobId);

    /// <summary>
    /// Requeue failed job
    /// </summary>
    bool Requeue(string jobId);

    /// <summary>
    /// Create continuation job (run after parent job succeeds)
    /// </summary>
    string ContinueJobWith<T>(
     string parentJobId,
        Expression<Action<T>> methodCall);

    /// <summary>
    /// Create async continuation job
    /// </summary>
    string ContinueJobWith<T>(
        string parentJobId,
        Expression<Func<T, Task>> methodCall);
}
```

**Giải thích:**
- **Enqueue:** Fire-and-forget jobs (chạy ngay lập tức trong background)
- **Schedule:** Delayed jobs (chạy sau 1 khoảng thời gian hoặc tại thời điểm cụ thể)
- **AddOrUpdateRecurringJob:** Recurring jobs (chạy theo lịch Cron: daily, hourly, weekly...)
- **ContinueJobWith:** Continuation jobs (job B chạy sau khi job A thành công)

**Tại sao có cả sync và async variants:**
- Hangfire support cả `Action<T>` và `Func<T, Task>`
- Async methods recommended cho I/O operations (DB, HTTP, file...)

---

## 4. Infrastructure Layer - Hangfire Implementation

### Bước 4.1: Tạo HangfireStorageSettings

**File:** `src/Infrastructure/Infrastructure/BackgroundJobs/HangfireStorageSettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire storage configuration
/// </summary>
public class HangfireStorageSettings
{
    /// <summary>
    /// Storage type: SqlServer, Memory, Redis
    /// </summary>
    public string StorageProvider { get; set; } = "SqlServer";

    /// <summary>
    /// Connection string for SQL Server storage
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Enable Hangfire Dashboard
    /// </summary>
    public bool EnableDashboard { get; set; } = true;

    /// <summary>
    /// Dashboard path (default: /hangfire)
    /// </summary>
    public string DashboardPath { get; set; } = "/hangfire";

    /// <summary>
    /// Dashboard title
    /// </summary>
    public string DashboardTitle { get; set; } = "ECO.WebApi Jobs";

    /// <summary>
 /// Worker count (default: 20)
    /// </summary>
    public int WorkerCount { get; set; } = 20;

    /// <summary>
    /// Job retention days (default: 7 days)
    /// </summary>
    public int JobRetentionDays { get; set; } = 7;

    /// <summary>
    /// Enable automatic retry for failed jobs
    /// </summary>
    public bool EnableAutomaticRetry { get; set; } = true;

    /// <summary>
  /// Maximum retry attempts (default: 3)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
```

**Giải thích:**
- **StorageProvider:** SQL Server recommended cho production (persistent)
- **WorkerCount:** Number of concurrent background threads
- **JobRetentionDays:** Succeeded/deleted jobs cleanup after X days
- **EnableAutomaticRetry:** Auto retry failed jobs with exponential backoff

---

### Bước 4.2: Implement HangfireService

**File:** `src/Infrastructure/Infrastructure/BackgroundJobs/HangfireService.cs`

```csharp
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ECO.WebApi.Application.Common.BackgroundJobs;

namespace ECO.WebApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire implementation of job service
/// </summary>
public class HangfireService : IJobService
{
    private readonly ILogger<HangfireService> _logger;

    public HangfireService(ILogger<HangfireService> logger)
    {
        _logger = logger;
    }

    public string Enqueue<T>(Expression<Action<T>> methodCall)
    {
        var jobId = BackgroundJob.Enqueue(methodCall);

        _logger.LogInformation(
            "Enqueued job {JobId} for {Service}.{Method}",
  jobId,
            typeof(T).Name,
   GetMethodName(methodCall));

        return jobId;
    }

    public string Enqueue<T>(Expression<Func<T, Task>> methodCall)
    {
        var jobId = BackgroundJob.Enqueue(methodCall);

     _logger.LogInformation(
          "Enqueued async job {JobId} for {Service}.{Method}",
            jobId,
      typeof(T).Name,
      GetMethodName(methodCall));

        return jobId;
    }

    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay)
    {
        var jobId = BackgroundJob.Schedule(methodCall, delay);

        _logger.LogInformation(
            "Scheduled job {JobId} for {Service}.{Method} to run in {Delay}",
        jobId,
 typeof(T).Name,
 GetMethodName(methodCall),
            delay);

        return jobId;
    }

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay)
    {
        var jobId = BackgroundJob.Schedule(methodCall, delay);

        _logger.LogInformation(
            "Scheduled async job {JobId} for {Service}.{Method} to run in {Delay}",
       jobId,
        typeof(T).Name,
      GetMethodName(methodCall),
delay);

        return jobId;
    }

    public string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt)
    {
var jobId = BackgroundJob.Schedule(methodCall, enqueueAt);

    _logger.LogInformation(
         "Scheduled job {JobId} for {Service}.{Method} to run at {EnqueueAt}",
  jobId,
    typeof(T).Name,
    GetMethodName(methodCall),
     enqueueAt);

        return jobId;
    }

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt)
    {
   var jobId = BackgroundJob.Schedule(methodCall, enqueueAt);

    _logger.LogInformation(
 "Scheduled async job {JobId} for {Service}.{Method} to run at {EnqueueAt}",
      jobId,
            typeof(T).Name,
  GetMethodName(methodCall),
            enqueueAt);

   return jobId;
 }

    public void AddOrUpdateRecurringJob<T>(
      string jobId,
     Expression<Action<T>> methodCall,
        string cronExpression,
        TimeZoneInfo? timeZone = null)
    {
        RecurringJob.AddOrUpdate(
            jobId,
            methodCall,
 cronExpression,
            timeZone ?? TimeZoneInfo.Utc);

        _logger.LogInformation(
      "Added/Updated recurring job {JobId} for {Service}.{Method} with cron '{CronExpression}'",
    jobId,
        typeof(T).Name,
   GetMethodName(methodCall),
            cronExpression);
    }

    public void AddOrUpdateRecurringJob<T>(
      string jobId,
        Expression<Func<T, Task>> methodCall,
        string cronExpression,
      TimeZoneInfo? timeZone = null)
    {
        RecurringJob.AddOrUpdate(
            jobId,
    methodCall,
            cronExpression,
            timeZone ?? TimeZoneInfo.Utc);

        _logger.LogInformation(
          "Added/Updated async recurring job {JobId} for {Service}.{Method} with cron '{CronExpression}'",
      jobId,
typeof(T).Name,
      GetMethodName(methodCall),
      cronExpression);
    }

public void RemoveRecurringJob(string jobId)
    {
        RecurringJob.RemoveIfExists(jobId);

        _logger.LogInformation("Removed recurring job {JobId}", jobId);
    }

    public void TriggerRecurringJob(string jobId)
    {
  RecurringJob.Trigger(jobId);

   _logger.LogInformation("Triggered recurring job {JobId}", jobId);
    }

    public bool Delete(string jobId)
    {
 var deleted = BackgroundJob.Delete(jobId);

        if (deleted)
        {
     _logger.LogInformation("Deleted job {JobId}", jobId);
    }
    else
      {
       _logger.LogWarning("Failed to delete job {JobId} (job not found or already processed)", jobId);
        }

        return deleted;
    }

    public bool Requeue(string jobId)
    {
        var requeued = BackgroundJob.Requeue(jobId);

        if (requeued)
{
_logger.LogInformation("Requeued failed job {JobId}", jobId);
        }
        else
        {
      _logger.LogWarning("Failed to requeue job {JobId}", jobId);
      }

     return requeued;
    }

    public string ContinueJobWith<T>(
      string parentJobId,
        Expression<Action<T>> methodCall)
    {
    var jobId = BackgroundJob.ContinueJobWith(parentJobId, methodCall);

 _logger.LogInformation(
            "Created continuation job {JobId} after parent {ParentJobId} for {Service}.{Method}",
     jobId,
    parentJobId,
   typeof(T).Name,
            GetMethodName(methodCall));

        return jobId;
    }

    public string ContinueJobWith<T>(
      string parentJobId,
      Expression<Func<T, Task>> methodCall)
    {
     var jobId = BackgroundJob.ContinueJobWith(parentJobId, methodCall);

        _logger.LogInformation(
        "Created async continuation job {JobId} after parent {ParentJobId} for {Service}.{Method}",
     jobId,
            parentJobId,
       typeof(T).Name,
     GetMethodName(methodCall));

        return jobId;
    }

    private static string GetMethodName<T>(Expression<Action<T>> methodCall)
    {
        if (methodCall.Body is MethodCallExpression methodCallExpression)
    {
            return methodCallExpression.Method.Name;
        }

        return "Unknown";
    }

    private static string GetMethodName<T>(Expression<Func<T, Task>> methodCall)
    {
        if (methodCall.Body is MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name;
        }

        return "Unknown";
    }
}
```

**Giải thích:**
- **Enqueue:** Wrap `BackgroundJob.Enqueue()` với logging
- **Schedule:** Support cả `TimeSpan` delay và `DateTimeOffset` absolute time
- **AddOrUpdateRecurringJob:** Idempotent - update existing recurring job nếu jobId trùng
- **GetMethodName:** Extract method name từ expression cho logging

**Why logging:**
- Track job creation với jobId
- Debug scheduling issues
- Audit trail cho background operations

---

### Bước 4.3: Tạo Hangfire Dashboard Authorization

**File:** `src/Infrastructure/Infrastructure/BackgroundJobs/HangfireDashboardAuthorizationFilter.cs`

```csharp
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace ECO.WebApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Authorization filter for Hangfire Dashboard
/// Requires authentication to access dashboard
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
    var httpContext = context.GetHttpContext();

 // Allow access in development environment
   if (httpContext.Request.Host.Host.Contains("localhost"))
  {
            return true;
     }

        // In production: require authentication
        return httpContext.User.Identity?.IsAuthenticated ?? false;
    }
}
```

**Giải thích:**
- **Development:** Allow localhost access (no auth)
- **Production:** Require authenticated user
- **Advanced:** Check specific roles/permissions nếu cần (e.g., Admin only)

**⚠️ Security Note:**
```csharp
// Production-ready authorization
public bool Authorize(DashboardContext context)
{
    var httpContext = context.GetHttpContext();
  
    // Require Admin role
    return httpContext.User.IsInRole("Admin");
    
  // OR check specific permission
    // return httpContext.User.HasClaim("Permission", "Jobs.View");
}
```

---

### Bước 4.4: Tạo Hangfire Job Filters

**File:** `src/Infrastructure/Infrastructure/BackgroundJobs/HangfireJobFilter.cs`

```csharp
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using System;

namespace ECO.WebApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Global filter for Hangfire jobs (logging, error handling)
/// </summary>
public class HangfireJobFilter : IElectStateFilter
{
    private readonly ILogger<HangfireJobFilter> _logger;

    public HangfireJobFilter(ILogger<HangfireJobFilter> logger)
 {
        _logger = logger;
    }

    public void OnStateElection(ElectStateContext context)
    {
        var failedState = context.CandidateState as FailedState;
  if (failedState != null)
        {
        // Log job failure
            _logger.LogError(
  failedState.Exception,
    "Job {JobId} ({JobType}.{JobMethod}) failed: {ErrorMessage}",
       context.BackgroundJob.Id,
                context.BackgroundJob.Job?.Type?.Name ?? "Unknown",
        context.BackgroundJob.Job?.Method?.Name ?? "Unknown",
      failedState.Exception?.Message);
        }

        var succeededState = context.CandidateState as SucceededState;
     if (succeededState != null)
   {
     // Log job success
     _logger.LogInformation(
    "Job {JobId} ({JobType}.{JobMethod}) succeeded in {Duration}",
           context.BackgroundJob.Id,
         context.BackgroundJob.Job?.Type?.Name ?? "Unknown",
              context.BackgroundJob.Job?.Method?.Name ?? "Unknown",
        succeededState.PerformanceDuration);
        }

   var processingState = context.CandidateState as ProcessingState;
  if (processingState != null)
     {
            // Log job start
          _logger.LogInformation(
          "Job {JobId} ({JobType}.{JobMethod}) started processing on worker {WorkerName}",
     context.BackgroundJob.Id,
       context.BackgroundJob.Job?.Type?.Name ?? "Unknown",
                context.BackgroundJob.Job?.Method?.Name ?? "Unknown",
         processingState.WorkerName);
        }
}
}
```

**Giải thích:**
- **IElectStateFilter:** Hangfire filter intercept state transitions
- **FailedState:** Log exceptions với full context (job type, method, error)
- **SucceededState:** Log success với duration
- **ProcessingState:** Log when job starts (useful for tracking long-running jobs)

---

### Bước 4.5: Tạo Startup Configuration

**File:** `src/Infrastructure/Infrastructure/BackgroundJobs/Startup.cs`

```csharp
using Hangfire;
using Hangfire.Console;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using ECO.WebApi.Application.Common.BackgroundJobs;

namespace ECO.WebApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire dependency injection and configuration
/// </summary>
internal static class Startup
{
    internal static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration config)
{
        // Configure settings
     services.Configure<HangfireStorageSettings>(
    config.GetSection(nameof(HangfireStorageSettings)));

        var settings = config.GetSection(nameof(HangfireStorageSettings))
    .Get<HangfireStorageSettings>();

        if (settings == null)
        {
      throw new InvalidOperationException("HangfireStorageSettings is not configured.");
        }

        // Register IJobService
        services.AddTransient<IJobService, HangfireService>();

        // Add Hangfire services
 services.AddHangfire((serviceProvider, configuration) =>
    {
      configuration
         .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
      .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
     .UseConsole();

      // Configure storage
  switch (settings.StorageProvider.ToLowerInvariant())
      {
      case "sqlserver":
                if (string.IsNullOrEmpty(settings.ConnectionString))
           {
        throw new InvalidOperationException(
  "SQL Server connection string is required for Hangfire storage.");
           }

      configuration.UseSqlServerStorage(
        settings.ConnectionString,
         new SqlServerStorageOptions
  {
           CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
  QueuePollInterval = TimeSpan.Zero,
           UseRecommendedIsolationLevel = true,
         DisableGlobalLocks = true,
     SchemaName = "hangfire"
     });
       break;

 case "memory":
          configuration.UseInMemoryStorage();
        break;

default:
     throw new InvalidOperationException(
        $"Unknown Hangfire storage provider: {settings.StorageProvider}");
            }

            // Add global filters
            configuration.UseFilter(new HangfireJobFilter(
      serviceProvider.GetRequiredService<ILogger<HangfireJobFilter>>()));

            // Configure automatic retry
            if (settings.EnableAutomaticRetry)
        {
                configuration.UseFilter(new AutomaticRetryAttribute
                {
  Attempts = settings.MaxRetryAttempts,
          OnAttemptsExceeded = AttemptsExceededAction.Delete
              });
       }
        });

     // Add Hangfire server
        services.AddHangfireServer(options =>
 {
            options.WorkerCount = settings.WorkerCount;
            options.ServerName = $"{Environment.MachineName}:{Guid.NewGuid()}";
        });

  return services;
    }

    internal static IApplicationBuilder UseHangfireDashboard(
        this IApplicationBuilder app,
        IConfiguration config)
    {
        var settings = config.GetSection(nameof(HangfireStorageSettings))
 .Get<HangfireStorageSettings>();

    if (settings?.EnableDashboard == true)
        {
 app.UseHangfireDashboard(settings.DashboardPath, new DashboardOptions
    {
      Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
       DashboardTitle = settings.DashboardTitle,
  StatsPollingInterval = 5000, // 5 seconds
     DisplayStorageConnectionString = false
            });
        }

        return app;
    }
}
```

**Giải thích:**

**SQL Server Storage Options:**
- **CommandBatchMaxTimeout:** Max time to wait for batch commands
- **QueuePollInterval:** Zero = instant polling (better latency)
- **DisableGlobalLocks:** Better performance for multiple servers
- **SchemaName:** Separate schema cho Hangfire tables

**Server Options:**
- **WorkerCount:** Concurrent background threads (default: 20)
- **ServerName:** Unique name cho distributed setup (machine + guid)

**Dashboard Options:**
- **Authorization:** Custom filter (require auth in production)
- **StatsPollingInterval:** Dashboard refresh interval
- **DisplayStorageConnectionString:** Hide sensitive connection string

---

### Bước 4.6: Register trong Infrastructure Startup

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
// ...existing code...

using ECO.WebApi.Infrastructure.BackgroundJobs;

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
      .AddBackgroundJobs(config) // ✅ Add Background Jobs
            .AddCaching(config)
            .AddMailing(config)
   .AddBlobStorage(config)
  .AddServices();
    }
}
```

---

### Bước 4.7: Register Dashboard trong Program.cs

**File:** `src/Host/Host/Program.cs`

```csharp
// ...existing code...

using ECO.WebApi.Infrastructure.BackgroundJobs;

var builder = WebApplication.CreateBuilder(args);

// ...existing services...

var app = builder.Build();

// ...existing middleware...

// Add Hangfire Dashboard (after UseRouting, before UseEndpoints)
app.UseHangfireDashboard(builder.Configuration);

// ...existing middleware...

app.Run();
```

**⚠️ Middleware Order:**
```
UseRouting()
UseAuthentication()  // ✅ Before Dashboard (nếu require auth)
UseAuthorization()
UseHangfireDashboard()  // ✅ After Auth
UseEndpoints()
```

---

## 5. Configuration

### Bước 5.1: appsettings.json - SQL Server Storage

**File:** `src/Host/Host/appsettings.json`

```json
{
  "HangfireStorageSettings": {
    "StorageProvider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=ECO_Jobs;User Id=sa;Password=YourPassword;TrustServerCertificate=True;",
    "EnableDashboard": true,
    "DashboardPath": "/jobs",
    "DashboardTitle": "ECO.WebApi Background Jobs",
    "WorkerCount": 20,
    "JobRetentionDays": 7,
    "EnableAutomaticRetry": true,
    "MaxRetryAttempts": 3
  }
}
```

**⚠️ Lưu ý:**
- Connection string có thể dùng chung với main database hoặc separate database
- Separate database recommended cho production (isolation)
- Hangfire tự tạo schema `hangfire` với 13 tables

---

### Bước 5.2: appsettings.Development.json - In-Memory Storage

**File:** `src/Host/Host/appsettings.Development.json`

```json
{
  "HangfireStorageSettings": {
    "StorageProvider": "Memory",
    "EnableDashboard": true,
    "DashboardPath": "/jobs",
    "DashboardTitle": "ECO.WebApi Jobs (Development)",
    "WorkerCount": 5,
    "EnableAutomaticRetry": true,
    "MaxRetryAttempts": 1
  }
}
```

**Giải thích:**
- **Memory storage:** Jobs mất khi restart app (OK cho development)
- **WorkerCount = 5:** Ít workers hơn cho dev machine
- **MaxRetryAttempts = 1:** Fail faster trong development

---

## 6. Usage Examples

### Bước 6.1: Fire-and-Forget - Send Welcome Email

**Use Case:** User đăng ký → gửi welcome email ngay lập tức (không block registration response)

**Request:**
```csharp
public class RegisterUserRequest : IRequest<Guid>
{
    public string Email { get; set; } = default!;
  public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
}
```

**Handler:**
```csharp
using ECO.WebApi.Application.Common.BackgroundJobs;
using ECO.WebApi.Application.Common.Mailing;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECO.WebApi.Application.Identity.Users;

public class RegisterUserHandler : IRequestHandler<RegisterUserRequest, Guid>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJobService _jobService;
    private readonly ILogger<RegisterUserHandler> _logger;

    public RegisterUserHandler(
        UserManager<ApplicationUser> userManager,
        IJobService jobService,
  ILogger<RegisterUserHandler> logger)
    {
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
    }

    public async Task<Guid> Handle(
   RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
// Create user
        var user = new ApplicationUser
  {
 Email = request.Email,
     UserName = request.Email,
   FullName = request.FullName,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);

      if (!result.Succeeded)
        {
    throw new ValidationException("Failed to create user.");
        }

        // ✅ Enqueue welcome email (Fire-and-Forget)
        var jobId = _jobService.Enqueue<IMailService>(
      x => x.SendAsync(
                new MailRequest
  {
        To = new List<string> { user.Email! },
        Subject = "Welcome to ECO.WebApi",
          Body = $"<h1>Welcome {user.FullName}!</h1><p>Thank you for registering.</p>",
   IsHtml = true
     },
    default));

        _logger.LogInformation(
            "Enqueued welcome email job {JobId} for user {UserId}",
    jobId,
            user.Id);

        // ✅ Return immediately (email sent in background)
      return user.Id;
}
}
```

**Giải thích:**
- **Fire-and-Forget:** Job chạy ngay lập tức trong background thread
- **Non-blocking:** Registration response trả về ngay, không chờ email
- **Reliable:** Nếu SMTP server down, job sẽ retry automatically

---

### Bước 6.2: Delayed Job - Send Password Reset Reminder

**Use Case:** User request reset password → gửi reminder sau 1 giờ nếu chưa reset

**Request:**
```csharp
public class ForgotPasswordRequest : IRequest<string>
{
    public string Email { get; set; } = default!;
}
```

**Handler:**
```csharp
public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordRequest, string>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJobService _jobService;
    private readonly IMailService _mailService;

public ForgotPasswordHandler(
        UserManager<ApplicationUser> userManager,
        IJobService jobService,
   IMailService mailService)
    {
        _userManager = userManager;
        _jobService = jobService;
        _mailService = mailService;
    }

    public async Task<string> Handle(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
 {
            // Don't reveal user existence
         return "If your email exists, you will receive a reset link.";
        }

        // Generate reset token
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = $"https://myapp.com/reset-password?token={resetToken}&email={user.Email}";

  // ✅ Send immediate email
        var immediateJobId = _jobService.Enqueue<IMailService>(
     x => x.SendAsync(
        new MailRequest
          {
           To = new List<string> { user.Email! },
      Subject = "Password Reset Request",
        Body = $"Click here to reset: <a href='{resetUrl}'>Reset Password</a>",
          IsHtml = true
   },
                default));

        // ✅ Schedule reminder after 1 hour
        var reminderJobId = _jobService.Schedule<IMailService>(
   x => x.SendAsync(
                new MailRequest
           {
   To = new List<string> { user.Email! },
            Subject = "Password Reset Reminder",
           Body = "<p>You requested a password reset 1 hour ago. Link expires in 23 hours.</p>",
   IsHtml = true
        },
    default),
            TimeSpan.FromHours(1));

        return "If your email exists, you will receive a reset link.";
    }
}
```

**Giải thích:**
- **Immediate Job:** Gửi reset email ngay lập tức
- **Delayed Job:** Gửi reminder sau 1 giờ
- **Use Case:** Improve UX, remind user nếu họ quên check email

---

### Bước 6.3: Recurring Job - Daily Cleanup Temp Files

**Use Case:** Cleanup temp files, expired cache, old logs mỗi ngày lúc 2 AM

**Service:**
```csharp
using ECO.WebApi.Application.Common.BlobStorage;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECO.WebApi.Infrastructure.BackgroundJobs.Jobs;

/// <summary>
/// Background job to cleanup temp files and expired data
/// </summary>
public interface ICleanupService : ITransientService
{
    Task CleanupTempFilesAsync(CancellationToken cancellationToken);
    Task CleanupExpiredCacheAsync(CancellationToken cancellationToken);
}

public class CleanupService : ICleanupService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ICacheService _cache;
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(
        IBlobStorageService blobStorage,
ICacheService cache,
        ILogger<CleanupService> logger)
    {
     _blobStorage = blobStorage;
   _cache = cache;
        _logger = logger;
    }

    public async Task CleanupTempFilesAsync(CancellationToken cancellationToken)
    {
  _logger.LogInformation("Starting temp files cleanup...");

        try
{
          // Get all temp files
     var tempBlobs = await _blobStorage.ListBlobsAsync("temp", cancellationToken: cancellationToken);

          // Delete files older than 24 hours
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-1);
            var deletedCount = 0;

            foreach (var blob in tempBlobs.Where(b => b.LastModified < cutoffDate))
       {
           await _blobStorage.DeleteAsync("temp", blob.Name, cancellationToken);
    deletedCount++;
            }

            _logger.LogInformation(
        "Cleanup completed: Deleted {DeletedCount} temp files older than {CutoffDate}",
     deletedCount,
       cutoffDate);
    }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Temp files cleanup failed");
     throw; // Hangfire will retry
        }
    }

    public async Task CleanupExpiredCacheAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting expired cache cleanup...");

        try
   {
          // Cache cleanup logic
            // (Most cache providers auto-cleanup expired entries)

_logger.LogInformation("Cache cleanup completed");
         await Task.CompletedTask;
        }
        catch (Exception ex)
      {
      _logger.LogError(ex, "Cache cleanup failed");
      throw;
        }
    }
}
```

**Register Recurring Jobs:**
```csharp
using ECO.WebApi.Application.Common.BackgroundJobs;
using ECO.WebApi.Infrastructure.BackgroundJobs.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.BackgroundJobs;

public static class RecurringJobsSetup
{
    public static IApplicationBuilder UseRecurringJobs(this IApplicationBuilder app)
    {
      var jobService = app.ApplicationServices.GetRequiredService<IJobService>();

    // Daily cleanup at 2 AM (UTC)
      jobService.AddOrUpdateRecurringJob<ICleanupService>(
       "cleanup-temp-files",
            x => x.CleanupTempFilesAsync(default),
       Cron.Daily(2)); // 2 AM UTC

        // Hourly cache cleanup
        jobService.AddOrUpdateRecurringJob<ICleanupService>(
        "cleanup-expired-cache",
       x => x.CleanupExpiredCacheAsync(default),
   Cron.Hourly());

 return app;
    }
}
```

**Register trong Program.cs:**
```csharp
// ...existing code...

var app = builder.Build();

// ...existing middleware...

app.UseHangfireDashboard(builder.Configuration);
app.UseRecurringJobs(); // ✅ Register recurring jobs

app.Run();
```

**Giải thích:**
- **Cron.Daily(2):** Chạy mỗi ngày lúc 2 AM UTC
- **Cron.Hourly():** Chạy mỗi giờ vào phút thứ 0
- **Idempotent:** `AddOrUpdateRecurringJob` safe để call nhiều lần (update existing job)

---

### Bước 6.4: Continuation Job - Generate Report → Send Email

**Use Case:** Generate PDF report (heavy task) → sau đó gửi email với attachment

**Service:**
```csharp
public interface IReportService : ITransientService
{
    Task<string> GenerateUserReportAsync(Guid userId, CancellationToken cancellationToken);
}

public class ReportService : IReportService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IRepository<ApplicationUser> _userRepository;
  private readonly ILogger<ReportService> _logger;

    public ReportService(
IBlobStorageService blobStorage,
        IRepository<ApplicationUser> userRepository,
        ILogger<ReportService> logger)
    {
        _blobStorage = blobStorage;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<string> GenerateUserReportAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
    _logger.LogInformation("Generating report for user {UserId}", userId);

        // Simulate heavy report generation
        await Task.Delay(5000, cancellationToken); // 5 seconds

// Generate PDF (pseudo-code)
        var pdfContent = $"User Report for {userId}";
        var pdfStream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

        // Upload to blob storage
  var blobName = $"reports/user-{userId}-{DateTime.UtcNow:yyyyMMdd}.pdf";
    var uploadRequest = new UploadBlobRequest
    {
            ContainerName = "reports",
BlobName = blobName,
         ContentType = "application/pdf",
   Data = pdfStream
        };

 var reportUrl = await _blobStorage.UploadAsync(uploadRequest, cancellationToken);

        _logger.LogInformation(
    "Report generated for user {UserId}: {ReportUrl}",
        userId,
            reportUrl);

        return reportUrl;
    }
}
```

**Request:**
```csharp
public class RequestUserReportRequest : IRequest<string>
{
    public Guid UserId { get; set; }
}
```

**Handler:**
```csharp
public class RequestUserReportHandler : IRequestHandler<RequestUserReportRequest, string>
{
    private readonly IJobService _jobService;
private readonly IRepository<ApplicationUser> _userRepository;

  public RequestUserReportHandler(
        IJobService jobService,
 IRepository<ApplicationUser> userRepository)
    {
        _jobService = jobService;
        _userRepository = userRepository;
    }

    public async Task<string> Handle(
     RequestUserReportRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
        ?? throw new NotFoundException($"User {request.UserId} not found.");

        // ✅ Job 1: Generate report
   var generateJobId = _jobService.Enqueue<IReportService>(
  x => x.GenerateUserReportAsync(request.UserId, default));

        // ✅ Job 2: Send email (continuation - chạy sau khi Job 1 thành công)
        var emailJobId = _jobService.ContinueJobWith<IMailService>(
            generateJobId,
            x => x.SendAsync(
                new MailRequest
    {
      To = new List<string> { user.Email! },
     Subject = "Your Report is Ready",
       Body = "<p>Your report has been generated. Check your reports section.</p>",
            IsHtml = true
      },
  default));

        return $"Report generation started (Job: {generateJobId}, Email: {emailJobId})";
    }
}
```

**Giải thích:**
- **Job 1 (Generate):** Heavy task, có thể mất 5-10 seconds
- **Job 2 (Email):** Chỉ chạy KHI Job 1 thành công
- **Benefit:** Không waste resources gửi email nếu report generation failed

---

### Bước 6.5: Batch Job - Send Bulk Notifications

**Use Case:** Admin gửi notification đến 10,000 users → batch processing

**Service:**
```csharp
public interface INotificationService : ITransientService
{
    Task SendBulkNotificationAsync(
        string title,
        string message,
        List<Guid> userIds,
  CancellationToken cancellationToken);
    
  Task SendNotificationToUserAsync(
        Guid userId,
        string title,
  string message,
        CancellationToken cancellationToken);
}

public class NotificationService : INotificationService
{
    private readonly IJobService _jobService;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IJobService jobService,
  IRepository<ApplicationUser> userRepository,
      IMailService mailService,
        ILogger<NotificationService> logger)
    {
        _jobService = jobService;
        _userRepository = userRepository;
        _mailService = mailService;
  _logger = logger;
    }

    public async Task SendBulkNotificationAsync(
  string title,
        string message,
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
     "Starting bulk notification to {UserCount} users",
     userIds.Count);

 // ✅ Enqueue individual jobs for each user (batch processing)
        var enqueuedCount = 0;
        foreach (var userId in userIds)
        {
        _jobService.Enqueue<INotificationService>(
         x => x.SendNotificationToUserAsync(userId, title, message, default));
  
   enqueuedCount++;

         // Batch 100 jobs at a time (avoid overwhelming queue)
  if (enqueuedCount % 100 == 0)
            {
       await Task.Delay(100, cancellationToken); // Small delay
   }
      }

        _logger.LogInformation(
      "Enqueued {EnqueuedCount} notification jobs",
enqueuedCount);
    }

    public async Task SendNotificationToUserAsync(
        Guid userId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
      _logger.LogWarning("User {UserId} not found, skipping notification", userId);
   return;
 }

        // Send email notification
        await _mailService.SendAsync(
            new MailRequest
            {
      To = new List<string> { user.Email! },
                Subject = title,
     Body = message,
          IsHtml = true
     },
         cancellationToken);

     _logger.LogInformation(
  "Sent notification to user {UserId} ({Email})",
      userId,
            user.Email);
    }
}
```

**Giải thích:**
- **Batch Processing:** 10,000 users = 10,000 individual jobs
- **Rate Limiting:** Small delay mỗi 100 jobs để avoid overwhelming queue
- **Parallel Execution:** Hangfire workers process jobs concurrently (WorkerCount = 20)
- **Fault Tolerance:** Nếu 1 user email failed, 9,999 other jobs vẫn succeed

---

## 7. Testing

### Bước 7.1: Tạo JobsController (Development/Admin Only)

**File:** `src/Host/Host/Controllers/JobsController.cs`

```csharp
using ECO.WebApi.Application.Common.BackgroundJobs;
using ECO.WebApi.Shared.Authorization;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace ECO.WebApi.Host.Controllers;

/// <summary>
/// Background jobs management endpoints (Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobService;

    public JobsController(IJobService jobService)
    {
 _jobService = jobService;
  }

    /// <summary>
    /// Test fire-and-forget job
    /// </summary>
    [HttpPost("test/fire-and-forget")]
  [MustHavePermission(ECOAction.Create, ECOFunction.Jobs)]
    public IActionResult TestFireAndForget([FromBody] TestJobRequest request)
    {
        var jobId = _jobService.Enqueue<TestJobService>(
            x => x.ExecuteAsync(request.Message, default));

        return Ok(new { jobId, type = "Fire-and-Forget" });
    }

    /// <summary>
    /// Test delayed job
    /// </summary>
    [HttpPost("test/delayed")]
    [MustHavePermission(ECOAction.Create, ECOFunction.Jobs)]
 public IActionResult TestDelayed([FromBody] TestDelayedJobRequest request)
    {
        var jobId = _jobService.Schedule<TestJobService>(
        x => x.ExecuteAsync(request.Message, default),
            TimeSpan.FromSeconds(request.DelaySeconds));

        return Ok(new
        {
        jobId,
         type = "Delayed",
delaySeconds = request.DelaySeconds
        });
    }

    /// <summary>
    /// Test recurring job
    /// </summary>
    [HttpPost("test/recurring")]
    [MustHavePermission(ECOAction.Create, ECOFunction.Jobs)]
    public IActionResult TestRecurring([FromBody] TestRecurringJobRequest request)
    {
      _jobService.AddOrUpdateRecurringJob<TestJobService>(
$"test-recurring-{Guid.NewGuid()}",
   x => x.ExecuteAsync(request.Message, default),
     request.CronExpression);

      return Ok(new
        {
  type = "Recurring",
            cronExpression = request.CronExpression
        });
    }

    /// <summary>
 /// Delete job
    /// </summary>
    [HttpDelete("{jobId}")]
 [MustHavePermission(ECOAction.Delete, ECOFunction.Jobs)]
    public IActionResult DeleteJob(string jobId)
    {
  var deleted = _jobService.Delete(jobId);

        return deleted
       ? Ok(new { message = $"Job {jobId} deleted" })
     : NotFound(new { message = $"Job {jobId} not found" });
    }

    /// <summary>
    /// Requeue failed job
    /// </summary>
[HttpPost("{jobId}/requeue")]
    [MustHavePermission(ECOAction.Update, ECOFunction.Jobs)]
  public IActionResult RequeueJob(string jobId)
    {
      var requeued = _jobService.Requeue(jobId);

      return requeued
         ? Ok(new { message = $"Job {jobId} requeued" })
       : NotFound(new { message = $"Job {jobId} not found or cannot be requeued" });
    }
}

// Test DTOs
public class TestJobRequest
{
    public string Message { get; set; } = default!;
}

public class TestDelayedJobRequest
{
    public string Message { get; set; } = default!;
    public int DelaySeconds { get; set; } = 30;
}

public class TestRecurringJobRequest
{
    public string Message { get; set; } = default!;
    public string CronExpression { get; set; } = Cron.Minutely();
}
```

**Test Job Service:**
```csharp
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECO.WebApi.Infrastructure.BackgroundJobs.Jobs;

public interface ITestJobService : ITransientService
{
    Task ExecuteAsync(string message, CancellationToken cancellationToken);
}

public class TestJobService : ITestJobService
{
    private readonly ILogger<TestJobService> _logger;

    public TestJobService(ILogger<TestJobService> logger)
    {
 _logger = logger;
    }

    public async Task ExecuteAsync(string message, CancellationToken cancellationToken)
    {
      _logger.LogInformation("Test job started with message: {Message}", message);

        // Simulate work
     await Task.Delay(2000, cancellationToken);

        _logger.LogInformation("Test job completed: {Message}", message);
    }
}
```

---

### Bước 7.2: Test với Postman/curl

**Test 1: Fire-and-Forget Job**
```bash
curl -X POST "https://localhost:7001/api/jobs/test/fire-and-forget" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello from background job!"}'
```

**Response:**
```json
{
  "jobId": "12345",
  "type": "Fire-and-Forget"
}
```

**Test 2: Delayed Job (30 seconds)**
```bash
curl -X POST "https://localhost:7001/api/jobs/test/delayed" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Delayed job message",
    "delaySeconds": 30
  }'
```

**Test 3: Recurring Job (Every Minute)**
```bash
curl -X POST "https://localhost:7001/api/jobs/test/recurring" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
"message": "Recurring job",
    "cronExpression": "*/1 * * * *"
}'
```

**Test 4: Check Dashboard**
```
Navigate to: https://localhost:7001/jobs
```

Dashboard shows:
- Jobs queued/processing/succeeded/failed
- Real-time stats
- Retry attempts
- Job history

---

## 8. Best Practices

### 8.1: Cron Expressions

**Common patterns:**
```csharp
// Every minute
Cron.Minutely() // "* * * * *"

// Every hour at minute 0
Cron.Hourly() // "0 * * * *"

// Every day at 2 AM
Cron.Daily(2) // "0 2 * * *"

// Every Monday at 8 AM
Cron.Weekly(DayOfWeek.Monday, 8) // "0 8 * * 1"

// Every 1st of month at midnight
Cron.Monthly(1) // "0 0 1 * *"

// Custom: Every 15 minutes
"*/15 * * * *"

// Custom: Weekdays at 9 AM
"0 9 * * 1-5"
```

**Cron Format:**
```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday = 0)
│ │ │ │ │
* * * * *
```

---

### 8.2: Error Handling & Retry

**Automatic Retry Configuration:**
```csharp
[AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
public async Task SendEmailAsync(MailRequest request, CancellationToken cancellationToken)
{
    // Will retry 5 times with exponential backoff
    await _mailService.SendAsync(request, cancellationToken);
}
```

**Custom Retry Logic:**
```csharp
public async Task ProcessPaymentAsync(Guid orderId, CancellationToken cancellationToken)
{
    try
    {
        await _paymentService.ProcessAsync(orderId, cancellationToken);
}
    catch (PaymentGatewayException ex) when (ex.IsTransient)
    {
    // Transient error - let Hangfire retry
        throw;
    }
    catch (PaymentGatewayException ex)
    {
        // Permanent error - don't retry
        _logger.LogError(ex, "Payment failed permanently for order {OrderId}", orderId);
        
        // Save error to database
        await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.PaymentFailed);
        
    // Don't re-throw (job succeeds but payment failed)
    }
}
```

---

### 8.3: Job Idempotency

**Problem:** Job có thể execute nhiều lần (retry, duplicate enqueue)

**Solution:** Make jobs idempotent
```csharp
public async Task SendWelcomeEmailAsync(Guid userId, CancellationToken cancellationToken)
{
    // ✅ Check if already sent
    var alreadySent = await _emailLogRepository.ExistsAsync(
        x => x.UserId == userId && x.EmailType == EmailType.Welcome);

    if (alreadySent)
    {
   _logger.LogInformation("Welcome email already sent to user {UserId}, skipping", userId);
        return;
    }

    // Send email
    await _mailService.SendAsync(emailRequest, cancellationToken);

    // Log sent email
    await _emailLogRepository.AddAsync(new EmailLog
    {
        UserId = userId,
        EmailType = EmailType.Welcome,
  SentAt = DateTime.UtcNow
    }, cancellationToken);
}
```

---

### 8.4: Performance Optimization

**1. Batch heavy database operations:**
```csharp
// ❌ Bad: N queries
foreach (var userId in userIds)
{
    var user = await _userRepository.GetByIdAsync(userId);
    // Process user
}

// ✅ Good: 1 query
var users = await _userRepository.ListAsync(
    new UsersByIdsSpec(userIds));

foreach (var user in users)
{
    // Process user
}
```

**2. Use batch jobs for bulk operations:**
```csharp
// ✅ Enqueue batch of 1000 users per job
var batches = userIds.Chunk(1000);

foreach (var batch in batches)
{
    _jobService.Enqueue<INotificationService>(
        x => x.SendBulkNotificationBatchAsync(batch.ToList(), default));
}
```

**3. Set job priorities:**
```csharp
// Critical jobs (email verification)
[Queue("critical")]
public async Task SendVerificationEmailAsync(...)

// Normal jobs (welcome email)
[Queue("default")]
public async Task SendWelcomeEmailAsync(...)

// Low priority jobs (cleanup)
[Queue("low")]
public async Task CleanupTempFilesAsync(...)
```

---

### 8.5: Monitoring & Alerting

**Log job metrics:**
```csharp
public async Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
     await _orderService.ProcessAsync(orderId, cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation(
  "Order {OrderId} processed in {ElapsedMs}ms",
      orderId,
            stopwatch.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
    stopwatch.Stop();
   _logger.LogError(
          ex,
            "Order {OrderId} processing failed after {ElapsedMs}ms",
 orderId,
            stopwatch.ElapsedMilliseconds);
        throw;
    }
}
```

**Dashboard monitoring:**
- Failed jobs count (alert if > threshold)
- Average job duration (detect performance degradation)
- Queue length (detect backlog)

---

## 9. Troubleshooting

### 9.1: Common Issues

**Issue 1: "Jobs not processing"**
```
Solution:
1. Check Hangfire server is started (logs: "Hangfire Server started")
2. Check SQL Server connection string
3. Verify hangfire schema exists in database
4. Check worker count > 0
```

**Issue 2: "Dashboard 404 Not Found"**
```
Solution:
1. Verify UseHangfireDashboard() called in Program.cs
2. Check DashboardPath setting (/jobs)
3. Ensure UseHangfireDashboard() after UseRouting()
4. Check authorization filter (localhost allowed?)
```

**Issue 3: "Jobs retry infinitely"**
```csharp
// Solution: Limit retry attempts
[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
public async Task MyJobAsync(...)
```

**Issue 4: "Memory leak in background jobs"**
```
Solution:
1. Dispose IDisposable resources
2. Avoid static collections in job classes
3. Use cancellation tokens properly
4. Check job retention settings (delete old jobs)
```

---

### 9.2: Debugging Tips

**Enable detailed logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Hangfire": "Debug",
      "ECO.WebApi.Infrastructure.BackgroundJobs": "Debug"
    }
  }
}
```

**Check Hangfire tables:**
```sql
-- View pending jobs
SELECT * FROM hangfire.Job WHERE StateName = 'Enqueued'

-- View failed jobs
SELECT * FROM hangfire.Job WHERE StateName = 'Failed'

-- View job history
SELECT * FROM hangfire.State ORDER BY CreatedAt DESC
```

---

## 10. Summary

### ✅ Đã hoàn thành trong bước này:

**Application Layer:**
- ✅ IJobService interface với 12 methods
- ✅ Fire-and-forget, Delayed, Recurring, Continuation jobs

**Infrastructure Layer:**
- ✅ HangfireService implementation
- ✅ HangfireStorageSettings configuration
- ✅ SQL Server storage với persistent jobs
- ✅ Automatic retry với exponential backoff
- ✅ Job filters (logging, error handling)
- ✅ Dashboard authorization

**Configuration:**
- ✅ SQL Server storage setup
- ✅ In-memory storage cho development
- ✅ Worker count, retention, retry settings

**Usage Examples:**
- ✅ Send welcome email (Fire-and-Forget)
- ✅ Password reset reminder (Delayed)
- ✅ Daily cleanup temp files (Recurring)
- ✅ Generate report → Send email (Continuation)
- ✅ Bulk notifications (Batch processing)

**Testing:**
- ✅ JobsController với test endpoints
- ✅ Hangfire Dashboard
- ✅ Test job service

---

### 📊 Architecture Diagram:

```
┌─────────────────────────────────────────────────┐
│       Application Layer          │
│  ┌──────────────────────────────────────────┐  │
│  │       IJobService (Interface)            │  │
│  │  - Enqueue (Fire-and-Forget)   │  │
│  │  - Schedule (Delayed)    │  │
│  │  - AddOrUpdateRecurringJob (Cron)│  │
││  - ContinueJobWith (Continuation)        ││
│  │  - Delete, Requeue       │  │
│  └──────────────────────────────────────────┘  │
└────────────────┬────────────────────────────────┘
            │ implements
      ↓
┌─────────────────────────────────────────────────┐
│      Infrastructure Layer - Hangfire       │
│  ┌──────────────────────────────────────────┐  │
│  │  HangfireService                │  │
│  │  - BackgroundJob.Enqueue()               │  │
│  │  - BackgroundJob.Schedule()              │  │
│  │  - RecurringJob.AddOrUpdate()       │  │
│  └──────────────────────────────────────────┘  │
│ │
│  ┌──────────────────────────────────────────┐  │
│  │     Hangfire Storage (SQL Server)     │  │
│  │  - Job queue    │  │
│  │  - Job state (Enqueued, Processing...)   │  │
│  │  - Job history & retries     │  │
│  └──────────────────────────────────────────┘  │
│   │
│  ┌──────────────────────────────────────────┐  │
│  │     Hangfire Server (Workers)      │  │
│  │  - WorkerCount: 20 threads     │  │
│  │  - Poll queue → Execute jobs         │  │
│  │  - Automatic retry on failure          │  │
│  └──────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
              │
          ↓
┌─────────────────────────────────────────────────┐
│         Job Execution Flow       │
│       │
│  HTTP Request              │
│      ↓          │
│  Enqueue Job (non-blocking, returns JobId)     │
│      ↓    │
│  Return Response (immediate)   │
│        │
│  [Background Thread]          │
│   ↓      │
│  Worker picks job from queue             │
│      ↓      │
│  Execute job method    │
│      ↓         │
│  Success → Mark succeeded             │
│  Failure → Retry (exponential backoff)          │
└─────────────────────────────────────────────────┘
```

---

### 📌 Key Concepts:

**Fire-and-Forget:**
- Enqueue job → Run immediately in background
- Use case: Send email, log event, update cache

**Delayed Job:**
- Schedule job → Run after delay
- Use case: Send reminder, expire session, cleanup

**Recurring Job:**
- Cron-based schedule → Run periodically
- Use case: Daily backup, hourly cleanup, weekly reports

**Continuation Job:**
- Parent job → Child job (run after parent succeeds)
- Use case: Generate report → Send email, Process payment → Update inventory

**Job Persistence:**
- SQL Server storage → Jobs survive app restart
- Auto-retry → Failed jobs retry automatically
- Dashboard → Monitor jobs real-time

---

### 📁 File Structure:

```
src/Core/Application/Common/BackgroundJobs/
└── IJobService.cs

src/Infrastructure/Infrastructure/BackgroundJobs/
├── HangfireService.cs
├── HangfireStorageSettings.cs
├── HangfireDashboardAuthorizationFilter.cs
├── HangfireJobFilter.cs
├── RecurringJobsSetup.cs
├── Startup.cs
└── Jobs/
    ├── ICleanupService.cs
    ├── CleanupService.cs
  ├── ITestJobService.cs
    └── TestJobService.cs

src/Host/Host/
├── Controllers/
│   └── JobsController.cs
└── appsettings.json
```

---

## 11. Next Steps

**Tiếp theo:** [BUILD_26 - Logging với Serilog](BUILD_26_Logging.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Setup Serilog structured logging
2. ✅ Multiple sinks (Console, File, Seq, Elasticsearch)
3. ✅ Request logging middleware
4. ✅ Exception logging
5. ✅ Performance logging
6. ✅ Log correlation với trace IDs

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
