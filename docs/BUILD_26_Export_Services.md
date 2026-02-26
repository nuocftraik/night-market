# Export Services - Excel & Report Generation

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Phase 6 (Infrastructure Services) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Export Services - hỗ trợ export data ra Excel, CSV, và PDF cho reporting.

---

## 1. Overview

**Làm gì:** Xây dựng service để export data ra các format phổ biến (Excel, CSV, PDF) phục vụ reporting và data analysis.

**Tại sao cần:**
- **Data Export:** Users cần export data để phân tích offline (Excel, CSV)
- **Reporting:** Generate reports cho management (PDF với formatting)
- **Audit Compliance:** Export audit logs, user activities
- **Data Portability:** Cho phép users xuất data của họ
- **Business Intelligence:** Integration với BI tools (Excel, CSV)

**Trong bước này chúng ta sẽ:**
- ✅ Tạo IExcelWriter interface cho Excel export
- ✅ Implement ClosedXML-based Excel writer
- ✅ Tạo dynamic export từ IQueryable<T>
- ✅ Support styled Excel (headers, formatting, formulas)
- ✅ Export audit logs, users, products
- ✅ Tạo ExportController cho testing

**Real-world example:**
```csharp
// Export products to Excel
public class ExportProductsHandler : IRequestHandler<ExportProductsRequest, byte[]>
{
    private readonly IExcelWriter _excelWriter;
    private readonly IReadRepository<Product> _repository;

    public async Task<byte[]> Handle(ExportProductsRequest request, CancellationToken ct)
    {
        // Get data with specifications
        var spec = new ProductsBySearchSpec(request);
        var products = await _repository.ListAsync(spec, ct);
 
        // Map to export DTOs
        var exportData = products.Select(p => new ProductExportDto
        {
            Name = p.Name,
            Category = p.Category.Name,
            Price = p.Price,
            Stock = p.Stock,
            CreatedOn = p.CreatedOn
        }).ToList();

        // Export to Excel with styling
        return await _excelWriter.WriteAsync(
                 data: exportData,
                 sheetName: "Products",
                 headers: new() { "Product Name", "Category", "Price", "Stock", "Created Date" }
        );
    }
}

// Download: GET /api/products/export → products.xlsx
```

---

## 2. Add Required Packages

### Bước 2.1: Add ClosedXML Package

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Excel export với ClosedXML -->
    <PackageReference Include="ClosedXML" Version="0.102.2" />
    
    <!-- CSV export (optional - built-in with System.Text) -->
    <!-- PDF export (optional - future enhancement) -->
    <!-- <PackageReference Include="QuestPDF" Version="2024.1.3" /> -->
</ItemGroup>
```

**Giải thích packages:**
- `ClosedXML`: Best .NET library cho Excel manipulation (XLSX format)
  - No MS Office dependency
  - Rich formatting support
  - Easy API
  - Active maintenance

**⚠️ Lưu ý:**
- Version 0.102.x là stable cho .NET 8
- ClosedXML tạo real Excel files (not CSV renamed)
- Memory efficient cho files < 100k rows

---

## 3. Application Layer - Export Interfaces

### Bước 3.1: Tạo IExcelWriter Interface

**Làm gì:** Định nghĩa contract cho Excel export operations.

**Tại sao:** Abstraction để có thể swap implementations (ClosedXML, EPPlus, etc.) nếu cần.

**File:** `src/Core/Application/Common/Exporters/IExcelWriter.cs`

```csharp
using System.Data;

namespace ECO.WebApi.Application.Common.Exporters;

/// <summary>
/// Service for exporting data to Excel format
/// </summary>
public interface IExcelWriter : ITransientService
{
    /// <summary>
    /// Write data to Excel file
    /// </summary>
    /// <typeparam name="T">Type of data to export</typeparam>
    /// <param name="data">Collection of data</param>
    /// <param name="sheetName">Sheet name in Excel</param>
    /// <param name="headers">Optional custom headers (nếu null, dùng property names)</param>
    Task<byte[]> WriteAsync<T>(
        IEnumerable<T> data,
        string sheetName = "Sheet1",
        List<string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Write DataTable to Excel file
    /// </summary>
    Task<byte[]> WriteAsync(
    DataTable dataTable,
     string sheetName = "Sheet1",
      CancellationToken cancellationToken = default);

    /// <summary>
    /// Write multiple sheets to Excel file
    /// </summary>
    Task<byte[]> WriteAsync(
        Dictionary<string, DataTable> sheets,
        CancellationToken cancellationToken = default);
}
```

**Giải thích:**
- **WriteAsync<T>:** Export generic collection với type-safe
- **WriteAsync(DataTable):** Export từ DataTable (khi query raw SQL)
- **WriteAsync(Dictionary):** Multiple sheets trong 1 file
- **Return byte[]:** In-memory Excel file, ready to download

**Tại sao return byte[] thay vì Stream:**
- Đơn giản hơn cho API responses
- Có thể cache result
- Easy to save to blob storage

---

### Bước 3.2: Tạo Export DTOs

**File:** `src/Core/Application/Common/Exporters/ExportColumn.cs`

```csharp
namespace ECO.WebApi.Application.Common.Exporters;

/// <summary>
/// Column definition for Excel export
/// </summary>
public class ExportColumn
{
    /// <summary>
    /// Property name to read value from
    /// </summary>
    public string PropertyName { get; set; } = default!;

    /// <summary>
    /// Header text to display
    /// </summary>
    public string Header { get; set; } = default!;

    /// <summary>
    /// Format string (e.g., "N2" for numbers, "dd/MM/yyyy" for dates)
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Column width in characters (auto if null)
    /// </summary>
    public double? Width { get; set; }
}
```

**File:** `src/Core/Application/Common/Exporters/ExportOptions.cs`

```csharp
namespace ECO.WebApi.Application.Common.Exporters;

/// <summary>
/// Options for Excel export styling
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Title row text (optional)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Freeze header row
    /// </summary>
    public bool FreezeHeader { get; set; } = true;

    /// <summary>
    /// Auto-filter on header row
    /// </summary>
    public bool AutoFilter { get; set; } = true;

    /// <summary>
    /// Auto-fit column widths
    /// </summary>
    public bool AutoFitColumns { get; set; } = true;

    /// <summary>
    /// Bold header row
    /// </summary>
    public bool BoldHeaders { get; set; } = true;

    /// <summary>
    /// Show gridlines
    /// </summary>
    public bool ShowGridLines { get; set; } = true;

    /// <summary>
    /// Column definitions (nếu null, auto-detect từ properties)
    /// </summary>
    public List<ExportColumn>? Columns { get; set; }
}
```

---

## 4. Infrastructure Layer - ClosedXML Implementation

### Bước 4.1: Implement ClosedXMLWriter

**Làm gì:** Implementation Excel writer sử dụng ClosedXML library.

**Tại sao:** ClosedXML là best choice cho .NET Excel export (no Office dependency, rich features).

**File:** `src/Infrastructure/Infrastructure/Exporters/ClosedXMLWriter.cs`

```csharp
using ClosedXML.Excel;
using ECO.WebApi.Application.Common.Exporters;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Reflection;

namespace ECO.WebApi.Infrastructure.Exporters;

/// <summary>
/// Excel writer implementation using ClosedXML
/// </summary>
public class ClosedXMLWriter : IExcelWriter
{
    private readonly ILogger<ClosedXMLWriter> _logger;

    public ClosedXMLWriter(ILogger<ClosedXMLWriter> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> WriteAsync<T>(
        IEnumerable<T> data,
        string sheetName = "Sheet1",
        List<string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
             using var workbook = new XLWorkbook();
             var worksheet = workbook.Worksheets.Add(sheetName);

             var dataList = data.ToList();

            if (!dataList.Any())
            {
                _logger.LogWarning("No data to export for sheet '{SheetName}'", sheetName);
                return SaveWorkbookToBytes(workbook);
            }

      // Get properties
      var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && IsExportableType(p.PropertyType))
                .ToList();

            // Write headers
            var headerRow = 1;
      for (int i = 0; i < properties.Count; i++)
      {
            var header = headers != null && i < headers.Count
                        ? headers[i]
                        : FormatPropertyName(properties[i].Name);

            var cell = worksheet.Cell(headerRow, i + 1);
                cell.Value = header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
       }

        // Write data
       var currentRow = headerRow + 1;
            foreach (var item in dataList)
            {
                 for (int i = 0; i < properties.Count; i++)
                {
                     var value = properties[i].GetValue(item);
                     var cell = worksheet.Cell(currentRow, i + 1);

                     SetCellValue(cell, value, properties[i].PropertyType);
                }

                currentRow++;
             }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

       // Freeze header row
        worksheet.SheetView.FreezeRows(1);

        // Add auto-filter
        var dataRange = worksheet.Range(headerRow, 1, currentRow - 1, properties.Count);
        dataRange.SetAutoFilter();

        _logger.LogInformation(
           "Exported {Count} rows to Excel sheet '{SheetName}'",
            dataList.Count,
            sheetName);

        return SaveWorkbookToBytes(workbook);
        }, cancellationToken);
  }

    public async Task<byte[]> WriteAsync(
        DataTable dataTable,
        string sheetName = "Sheet1",
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

        // Insert DataTable
            worksheet.Cell(1, 1).InsertTable(dataTable);

        // Style header row
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

       // Auto-fit columns
       worksheet.Columns().AdjustToContents();

        // Freeze header
       worksheet.SheetView.FreezeRows(1);

        _logger.LogInformation(
                "Exported DataTable with {Count} rows to Excel sheet '{SheetName}'",
                dataTable.Rows.Count,
                sheetName);

    return SaveWorkbookToBytes(workbook);
        }, cancellationToken);
    }

    public async Task<byte[]> WriteAsync(
        Dictionary<string, DataTable> sheets,
     CancellationToken cancellationToken = default)
 {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();

            foreach (var kvp in sheets)
            {
                var worksheet = workbook.Worksheets.Add(kvp.Key);

                // Insert DataTable
                worksheet.Cell(1, 1).InsertTable(kvp.Value);

                        // Style header row
                  var headerRow = worksheet.Row(1);
                  headerRow.Style.Font.Bold = true;
                  headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                 // Auto-fit columns
                 worksheet.Columns().AdjustToContents();

                // Freeze header
                 worksheet.SheetView.FreezeRows(1);
             }

     _logger.LogInformation(
       "Exported {Count} sheets to Excel workbook",
        sheets.Count);

        return SaveWorkbookToBytes(workbook);
        }, cancellationToken);
    }

    /// <summary>
    /// Set cell value with proper type handling
    /// </summary>
    private static void SetCellValue(IXLCell cell, object? value, Type propertyType)
  {
        if (value == null)
       {
            cell.Value = string.Empty;
            return;
        }

        // Handle specific types
        if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        {
            cell.Value = (DateTime)value;
            cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        }
        else if (propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTimeOffset?))
        {
            cell.Value = ((DateTimeOffset)value).DateTime;
            cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        }
        else if (propertyType == typeof(bool) || propertyType == typeof(bool?))
        {
            cell.Value = (bool)value ? "Yes" : "No";
        }
        else if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
        {
            cell.Value = (decimal)value;
            cell.Style.NumberFormat.Format = "#,##0.00";
        }
        else if (IsNumericType(propertyType))
        {
            cell.Value = Convert.ToDouble(value);
            cell.Style.NumberFormat.Format = "#,##0";
        }
        else
        {
            cell.Value = value.ToString();
        }
    }

    /// <summary>
    /// Check if type is numeric
    /// </summary>
    private static bool IsNumericType(Type type)
{
        type = Nullable.GetUnderlyingType(type) ?? type;

     return type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(short) ||
            type == typeof(byte) ||
            type == typeof(double) ||
            type == typeof(float) ||
            type == typeof(decimal);
    }

    /// <summary>
    /// Check if type can be exported
    /// </summary>
    private static bool IsExportableType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

   return type.IsPrimitive ||
          type == typeof(string) ||
          type == typeof(DateTime) ||
          type == typeof(DateTimeOffset) ||
          type == typeof(decimal) ||
          type == typeof(Guid);
    }

    /// <summary>
    /// Format property name to readable header
    /// </summary>
    private static string FormatPropertyName(string propertyName)
    {
        // CamelCase -> Camel Case
        return System.Text.RegularExpressions.Regex.Replace(
            propertyName,
            "([a-z])([A-Z])",
            "$1 $2");
    }

 /// <summary>
    /// Save workbook to byte array
    /// </summary>
    private static byte[] SaveWorkbookToBytes(XLWorkbook workbook)
 {
        using var stream = new MemoryStream();
         workbook.SaveAs(stream);
         return stream.ToArray();
    }
}
```

**Giải thích chi tiết:**

**WriteAsync<T> Method:**
1. Get public properties từ type T
2. Filter properties (chỉ exportable types)
3. Write headers với bold + gray background
4. Write data rows với type-specific formatting
5. Auto-fit columns, freeze header, add filter

**SetCellValue Method:**
- DateTime → format "dd/MM/yyyy HH:mm"
- Decimal → format "#,##0.00"
- Bool → "Yes"/"No"
- Numbers → format "#,##0"

**Performance:**
- `Task.Run` để avoid blocking ASP.NET thread pool
- Auto-fit columns chỉ 1 lần (sau khi write xong)
- Stream handling với `using` (dispose properly)

---

### Bước 4.2: Register Service

**File:** `src/Infrastructure/Infrastructure/Exporters/Startup.cs`

```csharp
using ECO.WebApi.Application.Common.Exporters;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Exporters;

internal static class Startup
{
    internal static IServiceCollection AddExporters(this IServiceCollection services)
    {
        // Register Excel writer
        services.AddTransient<IExcelWriter, ClosedXMLWriter>();

        return services;
    }
}
```

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
// ...existing code...

using ECO.WebApi.Infrastructure.Exporters;

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
            .AddBlobStorage(config)
     .AddExporters() // ✅ Add Exporters
        .AddServices();
    }
}
```

---

## 5. Application Layer - Export Requests & Handlers

### Bước 5.1: Export Audit Logs

**Request:**

**File:** `src/Core/Application/Auditing/ExportAuditLogsRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Models;
using MediatR;

namespace ECO.WebApi.Application.Auditing;

/// <summary>
/// Request to export audit logs to Excel
/// </summary>
public class ExportAuditLogsRequest : BaseFilter, IRequest<byte[]>
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? UserId { get; set; }
    public string? TableName { get; set; }
}
```

**Handler:**

**File:** `src/Core/Application/Auditing/ExportAuditLogsHandler.cs`

```csharp
using ECO.WebApi.Application.Common.Exporters;
using ECO.WebApi.Application.Common.Persistence;
using ECO.WebApi.Domain.Auditing;
using MediatR;

namespace ECO.WebApi.Application.Auditing;

public class ExportAuditLogsHandler : IRequestHandler<ExportAuditLogsRequest, byte[]>
{
    private readonly IReadRepository<Trail> _repository;
    private readonly IExcelWriter _excelWriter;

    public ExportAuditLogsHandler(
        IReadRepository<Trail> repository,
        IExcelWriter excelWriter)
    {
        _repository = repository;
        _excelWriter = excelWriter;
    }

    public async Task<byte[]> Handle(
        ExportAuditLogsRequest request,
        CancellationToken cancellationToken)
    {
        // Build specification
        var spec = new AuditLogsExportSpec(request);

  // Get data
        var trails = await _repository.ListAsync(spec, cancellationToken);

        // Map to export DTO
        var exportData = trails.Select(t => new AuditLogExportDto
      {
       DateTime = t.DateTime,
            UserId = t.UserId,
            Type = t.Type.ToString(),
            TableName = t.TableName,
            OldValues = t.OldValues,
             NewValues = t.NewValues,
            AffectedColumns = t.AffectedColumns,
            PrimaryKey = t.PrimaryKey
     }).ToList();

        // Export to Excel
        return await _excelWriter.WriteAsync(
       data: exportData,
  sheetName: "Audit Logs",
   headers: new List<string>
       {
    "Date Time",
    "User ID",
    "Action",
    "Table",
    "Old Values",
    "New Values",
    "Changed Columns",
  "Record ID"
   },
            cancellationToken: cancellationToken);
    }
}
```

**DTO:**

**File:** `src/Core/Application/Auditing/AuditLogExportDto.cs`

```csharp
namespace ECO.WebApi.Application.Auditing;

public class AuditLogExportDto
{
    public DateTime DateTime { get; set; }
    public string UserId { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string TableName { get; set; } = default!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
    public string PrimaryKey { get; set; } = default!;
}
```

**Specification:**

**File:** `src/Core/Application/Auditing/AuditLogsExportSpec.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Domain.Auditing;

namespace ECO.WebApi.Application.Auditing;

public class AuditLogsExportSpec : Specification<Trail>
{
    public AuditLogsExportSpec(ExportAuditLogsRequest request)
    {
        Query.OrderByDescending(x => x.DateTime);

        if (request.StartDate.HasValue)
        {
Query.Where(x => x.DateTime >= request.StartDate.Value);
        }

   if (request.EndDate.HasValue)
        {
    Query.Where(x => x.DateTime <= request.EndDate.Value);
        }

        if (!string.IsNullOrEmpty(request.UserId))
    {
            Query.Where(x => x.UserId == request.UserId);
      }

        if (!string.IsNullOrEmpty(request.TableName))
     {
  Query.Where(x => x.TableName == request.TableName);
      }

        // Limit to 10,000 records for export
      Query.Take(10000);
}
}
```

---

### Bước 5.2: Export Users

**Request:**

**File:** `src/Core/Application/Identity/Users/ExportUsersRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Models;
using MediatR;

namespace ECO.WebApi.Application.Identity.Users;

public class ExportUsersRequest : BaseFilter, IRequest<byte[]>
{
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
}
```

**Handler:**

**File:** `src/Core/Application/Identity/Users/ExportUsersHandler.cs`

```csharp
using ECO.WebApi.Application.Common.Exporters;
using ECO.WebApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Application.Identity.Users;

public class ExportUsersHandler : IRequestHandler<ExportUsersRequest, byte[]>
{
  private readonly UserManager<ApplicationUser> _userManager;
    private readonly IExcelWriter _excelWriter;

    public ExportUsersHandler(
    UserManager<ApplicationUser> userManager,
        IExcelWriter excelWriter)
    {
  _userManager = userManager;
  _excelWriter = excelWriter;
    }

    public async Task<byte[]> Handle(
        ExportUsersRequest request,
        CancellationToken cancellationToken)
    {
        // Query users
var usersQuery = _userManager.Users.AsQueryable();

        if (request.IsActive.HasValue)
 {
        usersQuery = usersQuery.Where(u => u.IsActive == request.IsActive.Value);
        }

  if (!string.IsNullOrEmpty(request.Role))
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(request.Role);
      var userIds = usersInRole.Select(u => u.Id).ToList();
usersQuery = usersQuery.Where(u => userIds.Contains(u.Id));
 }

        var users = await usersQuery
     .OrderBy(u => u.FirstName)
    .Take(10000)
       .ToListAsync(cancellationToken);

   // Map to export DTO
        var exportData = new List<UserExportDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            exportData.Add(new UserExportDto
      {
   Email = user.Email ?? string.Empty,
      FirstName = user.FirstName,
      LastName = user.LastName,
  PhoneNumber = user.PhoneNumber ?? string.Empty,
          IsActive = user.IsActive,
       EmailConfirmed = user.EmailConfirmed,
        Roles = string.Join(", ", roles),
    CreatedOn = user.CreatedOn
   });
        }

        // Export to Excel
        return await _excelWriter.WriteAsync(
       data: exportData,
     sheetName: "Users",
  headers: new List<string>
      {
"Email",
     "First Name",
                "Last Name",
      "Phone",
          "Active",
"Email Verified",
            "Roles",
         "Created Date"
 },
   cancellationToken: cancellationToken);
    }
}
```

**DTO:**

**File:** `src/Core/Application/Identity/Users/UserExportDto.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Users;

public class UserExportDto
{
public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public string Roles { get; set; } = default!;
    public DateTime CreatedOn { get; set; }
}
```

---

## 6. Host Layer - Export Controller

### Bước 6.1: Tạo ExportController

**Làm gì:** Expose export endpoints cho testing và admin usage.

**File:** `src/Host/Host/Controllers/ExportController.cs`

```csharp
using ECO.WebApi.Application.Auditing;
using ECO.WebApi.Application.Identity.Users;
using ECO.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

/// <summary>
/// Export endpoints (Excel, CSV, PDF)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExportController : BaseApiController
{
    /// <summary>
    /// Export audit logs to Excel
    /// </summary>
    [HttpPost("audit-logs")]
    [MustHavePermission(ECOAction.Export, ECOFunction.AuditLogs)]
    public async Task<IActionResult> ExportAuditLogs(
        [FromBody] ExportAuditLogsRequest request)
    {
var fileBytes = await Mediator.Send(request);

        var fileName = $"AuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

    return File(
         fileContents: fileBytes,
contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
fileDownloadName: fileName);
    }

    /// <summary>
    /// Export users to Excel
    /// </summary>
    [HttpPost("users")]
  [MustHavePermission(ECOAction.Export, ECOFunction.Users)]
  public async Task<IActionResult> ExportUsers(
        [FromBody] ExportUsersRequest request)
    {
        var fileBytes = await Mediator.Send(request);

        var fileName = $"Users_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(
        fileContents: fileBytes,
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
 fileDownloadName: fileName);
    }

    /// <summary>
    /// Get current user's audit logs as Excel
    /// </summary>
    [HttpGet("my-audit-logs")]
    [AllowAnonymous] // Users can export their own audit logs
    public async Task<IActionResult> ExportMyAuditLogs(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var request = new ExportAuditLogsRequest
        {
 StartDate = startDate,
        EndDate = endDate,
    UserId = User.GetUserId()
        };

        var fileBytes = await Mediator.Send(request);

      var fileName = $"MyAuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(
            fileContents: fileBytes,
    contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
       fileDownloadName: fileName);
    }
}
```

**Giải thích:**
- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (Excel XLSX)
- **File naming:** Timestamp để avoid conflicts
- **Permissions:** Export actions require specific permissions
- **Return File():** ASP.NET Core file download response

---

## 7. Usage Examples

### Bước 7.1: Export Audit Logs Example

**API Call:**
```bash
curl -X POST https://localhost:7001/api/export/audit-logs \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "startDate": "2026-01-01",
    "endDate": "2026-01-31",
    "tableName": "Users"
  }' \
  --output audit-logs.xlsx
```

**Response:**
- HTTP 200
- Content-Type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- File download: `AuditLogs_20260130_143025.xlsx`

**Excel Result:**
```
| Date Time          | User ID | Action | Table | Old Values | New Values | Changed Columns | Record ID |
|--------------------|---------|--------|-------|------------|------------|-----------------|-----------|
| 30/01/2026 14:25   | user-1  | Update | Users | {...}      | {...}      | Email,Phone | guid-123  |
| 30/01/2026 14:20   | user-2  | Create | Users |       | {...}      | All    | guid-456  |
```

---

### Bước 7.2: Export Users Example

**API Call:**
```bash
curl -X POST https://localhost:7001/api/export/users \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isActive": true,
    "role": "Admin"
  }' \
  --output users.xlsx
```

**Excel Result:**
```
| Email    | First Name | Last Name | Phone      | Active | Email Verified | Roles | Created Date    |
|--------------------|------------|-----------|------------|--------|----------------|-------|-----------------|
| admin@eco.vn       | John       | Doe       | 0901234567 | Yes    | Yes            | Admin | 15/01/2026 10:30|
| manager@eco.vn     | Jane       | Smith     | 0907654321 | Yes    | Yes       | Admin | 20/01/2026 14:20|
```

---

### Bước 7.3: Custom Export with Product Data

**Create Product Export Request:**

**File:** `src/Core/Application/Catalog/Products/ExportProductsRequest.cs`

```csharp
using ECO.WebApi.Application.Common.Models;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

public class ExportProductsRequest : BaseFilter, IRequest<byte[]>
{
  public string? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
  public bool? InStock { get; set; }
}
```

**Handler:**

**File:** `src/Core/Application/Catalog/Products/ExportProductsHandler.cs`

```csharp
using ECO.WebApi.Application.Common.Exporters;
using ECO.WebApi.Application.Common.Persistence;
using ECO.WebApi.Domain.Catalog;
using MediatR;

namespace ECO.WebApi.Application.Catalog.Products;

public class ExportProductsHandler : IRequestHandler<ExportProductsRequest, byte[]>
{
    private readonly IReadRepository<Product> _repository;
    private readonly IExcelWriter _excelWriter;

    public ExportProductsHandler(
        IReadRepository<Product> repository,
     IExcelWriter excelWriter)
    {
        _repository = repository;
     _excelWriter = excelWriter;
    }

    public async Task<byte[]> Handle(
        ExportProductsRequest request,
        CancellationToken cancellationToken)
    {
    // Build spec
        var spec = new ProductsExportSpec(request);

  // Get data
        var products = await _repository.ListAsync(spec, cancellationToken);

        // Map to export DTO
        var exportData = products.Select(p => new ProductExportDto
        {
     Name = p.Name,
         Description = p.Description,
Category = p.Category?.Name ?? "N/A",
            Price = p.Price,
    Stock = p.Stock,
     IsActive = p.IsActive,
          CreatedOn = p.CreatedOn,
    CreatedBy = p.CreatedBy?.ToString() ?? string.Empty
        }).ToList();

        // Export
        return await _excelWriter.WriteAsync(
            data: exportData,
        sheetName: "Products",
            headers: new List<string>
            {
   "Product Name",
      "Description",
         "Category",
      "Price (VND)",
        "Stock Quantity",
       "Active",
       "Created Date",
                "Created By"
            },
         cancellationToken: cancellationToken);
    }
}
```

**Specification:**

**File:** `src/Core/Application/Catalog/Products/ProductsExportSpec.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Domain.Catalog;

namespace ECO.WebApi.Application.Catalog.Products;

public class ProductsExportSpec : Specification<Product>
{
    public ProductsExportSpec(ExportProductsRequest request)
    {
        Query.Include(p => p.Category);

        if (!string.IsNullOrEmpty(request.CategoryId))
        {
            Query.Where(p => p.CategoryId.ToString() == request.CategoryId);
        }

        if (request.MinPrice.HasValue)
        {
   Query.Where(p => p.Price >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
  Query.Where(p => p.Price <= request.MaxPrice.Value);
        }

        if (request.InStock.HasValue)
 {
         Query.Where(p => request.InStock.Value ? p.Stock > 0 : p.Stock == 0);
      }

     Query.OrderBy(p => p.Name);

        // Limit export
Query.Take(10000);
    }
}
```

**Add to Controller:**

**File:** `src/Host/Host/Controllers/ExportController.cs`

```csharp
/// <summary>
/// Export products to Excel
/// </summary>
[HttpPost("products")]
[MustHavePermission(ECOAction.Export, ECOFunction.Products)]
public async Task<IActionResult> ExportProducts(
    [FromBody] ExportProductsRequest request)
{
    var fileBytes = await Mediator.Send(request);

    var fileName = $"Products_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

    return File(
        fileContents: fileBytes,
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileDownloadName: fileName);
}
```

---

## 8. Best Practices

### 8.1: Export Performance

**1. Limit Export Size:**
```csharp
// Always limit trong specification
Query.Take(10000); // Max 10k rows

// Nếu cần export nhiều hơn -> Background job
if (count > 10000)
{
    // Queue background job để generate file
    // Email link download khi done
}
```

**2. Pagination for Large Datasets:**
```csharp
// Option: Export theo batch
public async Task<byte[]> ExportLargeDataset(int batchSize = 1000)
{
    using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("Data");

    int currentRow = 2; // Row 1 = headers
    int pageNumber = 1;

    while (true)
    {
 var batch = await _repository.ListAsync(
        new PaginatedSpec(pageNumber, batchSize));

        if (!batch.Any()) break;

        // Write batch to worksheet
        foreach (var item in batch)
        {
 // Write row
       currentRow++;
  }

        pageNumber++;
    }

    return SaveWorkbookToBytes(workbook);
}
```

**3. Memory Optimization:**
```csharp
// ❌ Bad - load all to memory
var allData = await _context.Users.ToListAsync();
return await _excelWriter.WriteAsync(allData);

// ✅ Good - use specification to limit
var spec = new UsersForExportSpec(maxRecords: 10000);
var data = await _repository.ListAsync(spec);
return await _excelWriter.WriteAsync(data);
```

---

### 8.2: Security Best Practices

**1. Permission-Based Access:**
```csharp
// Require specific export permission
[MustHavePermission(ECOAction.Export, ECOFunction.Users)]

// Add Export action to ECOAction
public static class ECOAction
{
    // ...existing actions...
    public const string Export = nameof(Export);
}
```

**2. Data Filtering:**
```csharp
// User chỉ export data của họ
var request = new ExportAuditLogsRequest
{
    UserId = User.GetUserId(), // Force filter by current user
    StartDate = startDate,
    EndDate = endDate
};
```

**3. Sensitive Data:**
```csharp
// Mask sensitive fields
public class UserExportDto
{
    public string Email { get; set; } // OK
    public string FirstName { get; set; } // OK

    // ❌ Don't export password hash, security stamp
    // public string PasswordHash { get; set; }

    // ✅ Mask phone số
    public string PhoneNumber => MaskPhoneNumber(_phoneNumber);
}
```

---

### 8.3: Excel Formatting Tips

**1. Custom Formatting:**
```csharp
// Format currency
cell.Style.NumberFormat.Format = "#,##0 ₫"; // Vietnamese Dong

// Format percentage
cell.Style.NumberFormat.Format = "0.00%";

// Format date
cell.Style.DateFormat.Format = "dd/MM/yyyy";

// Conditional formatting
if (value < 0)
{
    cell.Style.Font.FontColor = XLColor.Red;
}
```

**2. Add Summary Row:**
```csharp
// After data rows
var summaryRow = currentRow + 1;
worksheet.Cell(summaryRow, 1).Value = "TOTAL:";
worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;

// Sum formula
var sumCell = worksheet.Cell(summaryRow, priceColumn);
sumCell.FormulaA1 = $"=SUM({priceColumn}2:{priceColumn}{currentRow})";
sumCell.Style.Font.Bold = true;
sumCell.Style.NumberFormat.Format = "#,##0.00";
```

**3. Multiple Sheets:**
```csharp
var sheets = new Dictionary<string, DataTable>
{
    { "Summary", summaryTable },
    { "Details", detailsTable },
    { "Charts", chartsTable }
};

var fileBytes = await _excelWriter.WriteAsync(sheets);
```

---

### 8.4: Error Handling

**1. Validate Input:**
```csharp
public class ExportRequestValidator : AbstractValidator<ExportAuditLogsRequest>
{
    public ExportRequestValidator()
    {
        // Date range validation
   RuleFor(x => x.EndDate)
       .GreaterThanOrEqualTo(x => x.StartDate)
 .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("End date must be after start date");

        // Max date range
      RuleFor(x => x)
            .Must(x =>
            {
        if (!x.StartDate.HasValue || !x.EndDate.HasValue)
        return true;

           return (x.EndDate.Value - x.StartDate.Value).TotalDays <= 365;
     })
            .WithMessage("Date range cannot exceed 1 year");
    }
}
```

**2. Handle Empty Data:**
```csharp
if (!dataList.Any())
{
    _logger.LogWarning("No data to export for request {@Request}", request);

    // Return empty Excel with headers only
    // or throw NotFoundException
    throw new NotFoundException("No data found for export");
}
```

---

## 9. Troubleshooting

### 9.1: Common Issues

**Issue 1: "File size too large"**
```
Solution:
1. Limit rows: Query.Take(10000)
2. Reduce columns: Export only necessary fields
3. Use background job cho large exports
4. Compress Excel file (ClosedXML auto-compresses)
```

**Issue 2: "Format issues with dates"**
```csharp
// Ensure proper date format
cell.Value = (DateTime)value; // Assign as DateTime type
cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";

// Not as string
// cell.Value = value.ToString(); // ❌ Wrong - formats as text
```

**Issue 3: "Excel file corrupted"**
```
Solution:
1. Check MemoryStream disposal (use 'using')
2. Verify SaveAs before return
3. Check encoding issues
4. Test với small dataset trước
```

**Issue 4: "Slow export performance"**
```
Solution:
1. Use AsNoTracking() trong queries
2. Select only required fields
3. Avoid N+1 queries (use Include)
4. Cache static data (categories, roles)
```

---

### 9.2: Debugging Tips

**Enable Detailed Logging:**
```json
{
  "Logging": {
    "LogLevel": {
  "ECO.WebApi.Infrastructure.Exporters": "Debug"
    }
  }
}
```

**Test Export Locally:**
```csharp
[HttpPost("test-export")]
[AllowAnonymous] // For local testing only
public async Task<IActionResult> TestExport()
{
    var testData = new List<TestDto>
    {
      new() { Name = "Test 1", Value = 100, Date = DateTime.Now },
        new() { Name = "Test 2", Value = 200, Date = DateTime.Now }
    };

    var bytes = await _excelWriter.WriteAsync(
     testData,
        "Test Sheet");

    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "test.xlsx");
}
```

---

## 10. Summary

### ✅ Đã hoàn thành trong bước này:

**Application Layer:**
- ✅ IExcelWriter interface với 3 overloads
- ✅ ExportColumn, ExportOptions models
- ✅ Export request/handler pattern
- ✅ Export DTOs cho Audit Logs, Users, Products

**Infrastructure Layer:**
- ✅ ClosedXMLWriter implementation
- ✅ Type-safe cell formatting (DateTime, Decimal, Bool, Numbers)
- ✅ Auto-fit columns, freeze headers, auto-filter
- ✅ Multi-sheet support
- ✅ Memory-efficient stream handling

**Host Layer:**
- ✅ ExportController với 3 endpoints
- ✅ Permission-based access control
- ✅ File download responses với proper content-type

**Features:**
- ✅ Export audit logs to Excel
- ✅ Export users to Excel
- ✅ Export products to Excel (example)
- ✅ Custom headers support
- ✅ Styled Excel (bold headers, gray background, formatting)

---

### 📊 Architecture Diagram:

```
┌────────────────────────────────────────────────┐
│ Application Layer          │
│   ┌────────────────────────────────────────┐   │
│   │   IExcelWriter (Interface)     │   │
│   │   - WriteAsync<T>()               │   │
│   │   - WriteAsync(DataTable)           │   │
│   │   - WriteAsync(Dictionary<sheets>)     │   │
│   └────────────────────────────────────────┘   │
└───────────────┬────────────────────────────────┘
     │ implements
        ↓
┌────────────────────────────────────────────────┐
│       Infrastructure Layer    │
│   ┌────────────────────────────────────────┐   │
││   ClosedXMLWriter     │   │
│   │   ├─ Type-safe formatting      │   │
│   │   ├─ Auto-fit columns               │   │
│   │ ├─ Freeze headers   │   │
│   │   └─ Auto-filter       │   │
│   └────────────────────────────────────────┘   │
└───────────────┬────────────────────────────────┘
     │ uses
       ↓
┌────────────────────────────────────────────────┐
│          ClosedXML Library  │
│   - XLWorkbook, XLWorksheet             │
│   - Cell formatting, Styles       │
│   - SaveAs(Stream)       │
└────────────────────────────────────────────────┘
```

**Export Flow:**
```
1. Controller receives Export Request
2. MediatR Handler:
   - Query data via Repository + Specification
   - Map to Export DTO
   - Call IExcelWriter.WriteAsync()
3. ClosedXMLWriter:
   - Create workbook & worksheet
   - Write headers (bold, gray background)
   - Write data rows (type-specific formatting)
   - Auto-fit, freeze, filter
   - Save to byte[]
4. Controller returns File() response
5. Browser downloads .xlsx file
```

---

### 📌 Key Concepts:

**Export Pattern:**
- Request → Specification → Query → Map DTO → Export → Download
- Separation: Business logic (Handler) vs Export logic (IExcelWriter)
- Type-safe với generics

**ClosedXML Benefits:**
- No MS Office dependency
- Pure .NET library
- Rich formatting support
- Memory efficient

**Performance Considerations:**
- Limit exports (10k rows max)
- Use specifications để filter
- Async/await với Task.Run
- Proper stream disposal

---

### 📁 File Structure:

```
src/Core/Application/Common/Exporters/
├── IExcelWriter.cs
├── ExportColumn.cs
└── ExportOptions.cs

src/Core/Application/Auditing/
├── ExportAuditLogsRequest.cs
├── ExportAuditLogsHandler.cs
├── AuditLogExportDto.cs
└── AuditLogsExportSpec.cs

src/Core/Application/Identity/Users/
├── ExportUsersRequest.cs
├── ExportUsersHandler.cs
└── UserExportDto.cs

src/Infrastructure/Infrastructure/Exporters/
├── ClosedXMLWriter.cs
└── Startup.cs

src/Host/Host/Controllers/
└── ExportController.cs
```

---

## 11. Next Steps

**Tiếp theo:** [BUILD_27 - Catalog Module](BUILD_27_Catalog_Module.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Xây dựng Product entity với rich domain model
2. ✅ Implement Category hierarchical structure
3. ✅ CRUD operations cho Products và Categories
4. ✅ Search và filtering với Specifications
5. ✅ Product specifications (dynamic attributes)
6. ✅ Integration với Export Services (export products)

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
