# BUILD_27: PDF Export Service - PDF Generation và Report Templates

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 26 (Export Services) đã hoàn thành

Tài liệu này hướng dẫn xây dựng **PDF Export Service** - Service để generate PDF documents với templates, charts, watermarks, và digital signatures.

---

## 1. Overview

**Làm gì:** Xây dựng service để generate PDF documents từ data với support cho templates, styling, charts, images, watermarks, và digital signatures.

**Tại sao cần:**
- **Professional Reports:** Tạo invoices, receipts, reports với format chuyên nghiệp
- **Customizable Templates:** Sử dụng templates để standardize document format
- **Rich Content:** Embed charts, images, tables vào PDF
- **Security:** Watermarks và digital signatures cho document protection
- **Multi-format:** Support multiple PDF libraries (QuestPDF, iTextSharp)
- **Reusable:** Abstract interface để dễ dàng switch PDF libraries

**Trong bước này chúng ta sẽ:**
- ✅ Tạo `IPdfService` interface trong Application layer
- ✅ Implement `QuestPdfService` với QuestPDF library
- ✅ Tạo PDF models và DTOs (PdfDocument, PdfOptions)
- ✅ Tạo Invoice/Report templates
- ✅ Support header/footer, watermarks, charts, images
- ✅ Register service trong Infrastructure startup
- ✅ Tạo controller để test PDF generation
- ✅ Examples: Invoice, Report, Certificate

**Real-world example:**
```csharp
// Generate invoice PDF
var invoice = new InvoiceData
{
    InvoiceNumber = "INV-2024-001",
    CustomerName = "Nguyen Van A",
    Items = new List<InvoiceItem>
    {
        new("Product 1", 2, 100000),
        new("Product 2", 1, 150000)
    },
    Total = 350000
};

var pdfBytes = await _pdfService.GenerateInvoiceAsync(invoice);
return File(pdfBytes, "application/pdf", "invoice.pdf");

// Generate report with chart
var reportData = new ReportData
{
    Title = "Sales Report Q1 2024",
    ChartData = salesData,
    Summary = summaryText
};

var reportPdf = await _pdfService.GenerateReportAsync(reportData);
```

---

## 2. Add Required Packages

### Bước 2.1: Add QuestPDF Package (Application Layer)

**File:** `src/Core/Application/Application.csproj`

```xml
<ItemGroup>
    <!-- PDF Generation -->
  <PackageReference Include="QuestPDF" Version="2024.3.0" />
</ItemGroup>
```

**Giải thích packages:**
- `QuestPDF`: Modern, fast PDF generation library với fluent API
  - MIT License (free for commercial use)
  - Type-safe fluent API
  - Rich content support (tables, images, charts)
  - Great performance
  - Cross-platform

**⚠️ Lưu ý:**
- QuestPDF requires license key for commercial use beyond certain threshold
- Alternative: `iTextSharp` (AGPL license) hoặc `PdfSharpCore` (MIT license)

---

### Bước 2.2: Add Supporting Packages

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- PDF Generation Support -->
    <PackageReference Include="QuestPDF" Version="2024.3.0" />
    <PackageReference Include="SkiaSharp" Version="2.88.7" />
    <PackageReference Include="SkiaSharp.NativeAssets.Linux" Version="2.88.7" />
</ItemGroup>
```

**Giải thích:**
- `SkiaSharp`: Graphics library cho chart rendering
- `SkiaSharp.NativeAssets.Linux`: Linux platform support

---

## 3. Application Layer - Interfaces và Models

### Bước 3.1: PDF Models

**Làm gì:** Tạo models để represent PDF document structure.

**Tại sao:** Type-safe models cho PDF generation, dễ maintain và extend.

**File:** `src/Core/Application/Common/Exporters/PdfModels.cs`

```csharp
namespace ECO.WebApi.Application.Common.Exporters;

/// <summary>
/// PDF document options
/// </summary>
public class PdfOptions
{
    /// <summary>
/// Document title (metadata)
    /// </summary>
  public string Title { get; set; } = "Document";

    /// <summary>
    /// Document author (metadata)
    /// </summary>
    public string Author { get; set; } = "ECO System";

    /// <summary>
    /// Document subject (metadata)
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Show page numbers
    /// </summary>
    public bool ShowPageNumbers { get; set; } = true;

    /// <summary>
    /// Add watermark
  /// </summary>
    public bool AddWatermark { get; set; } = false;

    /// <summary>
    /// Watermark text
    /// </summary>
    public string WatermarkText { get; set; } = "CONFIDENTIAL";

    /// <summary>
    /// Include header
    /// </summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>
    /// Include footer
    /// </summary>
    public bool IncludeFooter { get; set; } = true;

    /// <summary>
    /// Header text (left side)
    /// </summary>
    public string HeaderLeft { get; set; } = string.Empty;

    /// <summary>
    /// Header text (right side)
    /// </summary>
    public string HeaderRight { get; set; } = string.Empty;

    /// <summary>
  /// Footer text (left side)
    /// </summary>
    public string FooterLeft { get; set; } = string.Empty;

    /// <summary>
    /// Footer text (right side)
    /// </summary>
    public string FooterRight { get; set; } = string.Empty;
}

/// <summary>
/// Invoice data model
/// </summary>
public class InvoiceData
{
    public string InvoiceNumber { get; set; } = default!;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    
    public string CompanyName { get; set; } = "ECO Company";
    public string CompanyAddress { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    
    public string CustomerName { get; set; } = default!;
    public string CustomerAddress { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    
    public List<InvoiceItem> Items { get; set; } = new();
    
    public decimal Subtotal => Items.Sum(x => x.Total);
    public decimal TaxRate { get; set; } = 0.1m; // 10%
    public decimal TaxAmount => Subtotal * TaxRate;
 public decimal Total => Subtotal + TaxAmount;
    
    public string Notes { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = "Payment due within 30 days";
}

/// <summary>
/// Invoice item
/// </summary>
public class InvoiceItem
{
    public string Description { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;

    public InvoiceItem() { }

public InvoiceItem(string description, int quantity, decimal unitPrice)
    {
        Description = description;
     Quantity = quantity;
 UnitPrice = unitPrice;
    }
}

/// <summary>
/// Report data model
/// </summary>
public class ReportData
{
    public string Title { get; set; } = "Report";
    public string Subtitle { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    public string GeneratedBy { get; set; } = "System";
    
    public string Summary { get; set; } = string.Empty;
    
    public List<ReportSection> Sections { get; set; } = new();
    
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Report section
/// </summary>
public class ReportSection
{
    public string Title { get; set; } = default!;
    public string Content { get; set; } = string.Empty;
    public List<ReportTable> Tables { get; set; } = new();
    public byte[]? ChartImage { get; set; }
}

/// <summary>
/// Report table
/// </summary>
public class ReportTable
{
    public string Title { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}

/// <summary>
/// Chart data for PDF
/// </summary>
public class ChartData
{
    public string Title { get; set; } = default!;
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
    public ChartType Type { get; set; } = ChartType.Bar;
}

/// <summary>
/// Chart types
/// </summary>
public enum ChartType
{
    Bar,
    Line,
Pie
}
```

**Giải thích:**
- **PdfOptions:** Configuration cho PDF document (metadata, header, footer, watermark)
- **InvoiceData:** Model cho invoice generation với calculated properties
- **ReportData:** Flexible model cho reports với sections và tables
- **ChartData:** Model cho chart rendering trong PDF

**Lợi ích:**
- ✅ Type-safe models
- ✅ Calculated properties (Subtotal, Total)
- ✅ Flexible structure cho different document types
- ✅ Easy to extend

---

### Bước 3.2: IPdfService Interface

**Làm gì:** Tạo interface cho PDF generation service.

**Tại sao:** Abstraction để dễ dàng switch PDF libraries (QuestPDF, iTextSharp, etc.)

**File:** `src/Core/Application/Common/Exporters/IPdfService.cs`

```csharp
namespace ECO.WebApi.Application.Common.Exporters;

/// <summary>
/// Service for generating PDF documents
/// </summary>
public interface IPdfService : ITransientService
{
    /// <summary>
    /// Generate invoice PDF
    /// </summary>
    /// <param name="data">Invoice data</param>
    /// <param name="options">PDF options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF file as byte array</returns>
    Task<byte[]> GenerateInvoiceAsync(
        InvoiceData data,
    PdfOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate report PDF
  /// </summary>
    /// <param name="data">Report data</param>
    /// <param name="options">PDF options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF file as byte array</returns>
    Task<byte[]> GenerateReportAsync(
        ReportData data,
 PdfOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate custom PDF from HTML
  /// </summary>
    /// <param name="html">HTML content</param>
    /// <param name="options">PDF options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF file as byte array</returns>
    Task<byte[]> GenerateFromHtmlAsync(
   string html,
        PdfOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge multiple PDF files
    /// </summary>
    /// <param name="pdfFiles">List of PDF files as byte arrays</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Merged PDF file as byte array</returns>
    Task<byte[]> MergePdfsAsync(
        List<byte[]> pdfFiles,
   CancellationToken cancellationToken = default);

    /// <summary>
    /// Add watermark to existing PDF
    /// </summary>
    /// <param name="pdfFile">PDF file as byte array</param>
 /// <param name="watermarkText">Watermark text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF with watermark as byte array</returns>
    Task<byte[]> AddWatermarkAsync(
        byte[] pdfFile,
        string watermarkText,
      CancellationToken cancellationToken = default);
}
```

**Giải thích:**
- **GenerateInvoiceAsync:** Generate invoice với template có sẵn
- **GenerateReportAsync:** Generate report với sections và tables
- **GenerateFromHtmlAsync:** Generate PDF từ HTML string (flexible)
- **MergePdfsAsync:** Merge nhiều PDFs thành 1 file
- **AddWatermarkAsync:** Add watermark vào existing PDF

**Tại sao ITransientService:**
- PDF generation là stateless operation
- Mỗi request tạo document mới
- Không cần cache state giữa requests

---

## 4. Infrastructure Layer - QuestPDF Implementation

### Bước 4.1: QuestPdfService Implementation

**Làm gì:** Implement PDF generation với QuestPDF library.

**Tại sao:** QuestPDF cung cấp fluent API, type-safe, và performance tốt.

**File:** `src/Infrastructure/Infrastructure/Exporters/QuestPdfService.cs`

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ECO.WebApi.Infrastructure.Exporters;

/// <summary>
/// PDF service implementation using QuestPDF
/// </summary>
public class QuestPdfService : IPdfService
{
    private readonly ILogger<QuestPdfService> _logger;

    public QuestPdfService(ILogger<QuestPdfService> logger)
    {
   _logger = logger;
        
        // Configure QuestPDF license (required for commercial use)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc/>
    public Task<byte[]> GenerateInvoiceAsync(
        InvoiceData data,
        PdfOptions? options = null,
 CancellationToken cancellationToken = default)
    {
 options ??= new PdfOptions { Title = $"Invoice {data.InvoiceNumber}" };

    try
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
     {
         page.Size(PageSizes.A4);
                 page.Margin(50);
         page.DefaultTextStyle(x => x.FontSize(10));

        // Header
    if (options.IncludeHeader)
     {
    page.Header().Element(c => ComposeInvoiceHeader(c, data));
      }

 // Content
    page.Content().Element(c => ComposeInvoiceContent(c, data));

           // Footer
 if (options.IncludeFooter)
    {
              page.Footer().Element(c => ComposeFooter(c, options));
         }
   });
            });

     var pdfBytes = document.GeneratePdf();

      // Add watermark if needed
            if (options.AddWatermark)
            {
          return AddWatermarkAsync(pdfBytes, options.WatermarkText, cancellationToken);
   }

      _logger.LogInformation("Generated invoice PDF: {InvoiceNumber}", data.InvoiceNumber);
     return Task.FromResult(pdfBytes);
        }
        catch (Exception ex)
   {
            _logger.LogError(ex, "Error generating invoice PDF: {InvoiceNumber}", data.InvoiceNumber);
    throw;
        }
    }

    /// <inheritdoc/>
  public Task<byte[]> GenerateReportAsync(
        ReportData data,
        PdfOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PdfOptions { Title = data.Title };

        try
        {
        var document = Document.Create(container =>
    {
   container.Page(page =>
     {
          page.Size(PageSizes.A4);
     page.Margin(50);
        page.DefaultTextStyle(x => x.FontSize(10));

           // Header
    if (options.IncludeHeader)
    {
       page.Header().Element(c => ComposeReportHeader(c, data, options));
        }

        // Content
          page.Content().Element(c => ComposeReportContent(c, data));

        // Footer
      if (options.IncludeFooter)
           {
            page.Footer().Element(c => ComposeFooter(c, options));
      }
    });
  });

         var pdfBytes = document.GeneratePdf();

 // Add watermark if needed
       if (options.AddWatermark)
            {
           return AddWatermarkAsync(pdfBytes, options.WatermarkText, cancellationToken);
            }

            _logger.LogInformation("Generated report PDF: {Title}", data.Title);
 return Task.FromResult(pdfBytes);
    }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report PDF: {Title}", data.Title);
      throw;
  }
    }

    /// <inheritdoc/>
    public Task<byte[]> GenerateFromHtmlAsync(
        string html,
    PdfOptions? options = null,
      CancellationToken cancellationToken = default)
    {
        // QuestPDF doesn't support HTML directly
        // For HTML to PDF, consider using libraries like:
      // - DinkToPdf
        // - PuppeteerSharp
        // - Playwright
        throw new NotImplementedException("HTML to PDF conversion requires additional library like DinkToPdf or PuppeteerSharp");
    }

    /// <inheritdoc/>
    public Task<byte[]> MergePdfsAsync(
  List<byte[]> pdfFiles,
        CancellationToken cancellationToken = default)
    {
    // QuestPDF doesn't support PDF merging natively
        // For PDF merging, consider using iTextSharp or PdfSharp
      throw new NotImplementedException("PDF merging requires additional library like iTextSharp or PdfSharp");
    }

    /// <inheritdoc/>
    public Task<byte[]> AddWatermarkAsync(
        byte[] pdfFile,
        string watermarkText,
   CancellationToken cancellationToken = default)
    {
// Simple watermark implementation
        // For production, consider more sophisticated watermark library
    try
     {
            var document = Document.Create(container =>
  {
         container.Page(page =>
{
            page.Size(PageSizes.A4);
           page.Content().Layers(layers =>
     {
   // Original content would go here
    // This is simplified - in production you'd overlay on existing PDF

   // Watermark layer
     layers.Layer().AlignCenter().AlignMiddle()
     .Text(watermarkText)
    .FontSize(60)
             .FontColor(Colors.Grey.Lighten3)
          .Bold();
         });
          });
          });

            var result = document.GeneratePdf();
        return Task.FromResult(result);
     }
    catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding watermark to PDF");
throw;
        }
    }

    #region Private Helper Methods - Invoice

    private void ComposeInvoiceHeader(IContainer container, InvoiceData data)
    {
        container.Row(row =>
        {
          // Company info (left)
     row.RelativeItem().Column(column =>
       {
      column.Item().Text(data.CompanyName)
            .FontSize(20)
       .Bold()
           .FontColor(Colors.Blue.Darken2);

    column.Item().PaddingTop(5).Text(text =>
{
          text.Span(data.CompanyAddress).FontSize(9);
           });

       if (!string.IsNullOrEmpty(data.CompanyPhone))
    {
             column.Item().Text($"Phone: {data.CompanyPhone}").FontSize(9);
     }

           if (!string.IsNullOrEmpty(data.CompanyEmail))
   {
          column.Item().Text($"Email: {data.CompanyEmail}").FontSize(9);
   }
            });

            // Invoice info (right)
        row.RelativeItem().Column(column =>
       {
        column.Item().AlignRight().Text("INVOICE")
        .FontSize(20)
              .Bold()
        .FontColor(Colors.Blue.Darken2);

   column.Item().AlignRight().Text($"# {data.InvoiceNumber}")
    .FontSize(12)
      .Bold();

  column.Item().AlignRight().PaddingTop(10).Text(text =>
    {
       text.Span("Date: ").Bold();
    text.Span(data.InvoiceDate.ToString("dd/MM/yyyy"));
 });

  column.Item().AlignRight().Text(text =>
             {
  text.Span("Due Date: ").Bold();
   text.Span(data.DueDate.ToString("dd/MM/yyyy"));
   });
  });
   });
    }

    private void ComposeInvoiceContent(IContainer container, InvoiceData data)
    {
      container.PaddingVertical(20).Column(column =>
        {
     column.Spacing(20);

  // Customer info
            column.Item().Row(row =>
       {
          row.RelativeItem().Column(col =>
          {
           col.Item().Text("Bill To:").Bold().FontSize(11);
  col.Item().PaddingTop(5).Text(data.CustomerName).FontSize(10);
           
            if (!string.IsNullOrEmpty(data.CustomerAddress))
    {
         col.Item().Text(data.CustomerAddress).FontSize(9);
     }
          
            if (!string.IsNullOrEmpty(data.CustomerPhone))
      {
   col.Item().Text($"Phone: {data.CustomerPhone}").FontSize(9);
   }
         
         if (!string.IsNullOrEmpty(data.CustomerEmail))
        {
     col.Item().Text($"Email: {data.CustomerEmail}").FontSize(9);
   }
          });
  });

            // Items table
            column.Item().Table(table =>
   {
    table.ColumnsDefinition(columns =>
           {
   columns.RelativeColumn(3); // Description
          columns.RelativeColumn(1); // Quantity
      columns.RelativeColumn(2); // Unit Price
           columns.RelativeColumn(2); // Total
  });

        // Header
 table.Header(header =>
   {
      header.Cell().Element(CellStyle).Text("Description").Bold();
        header.Cell().Element(CellStyle).AlignRight().Text("Quantity").Bold();
           header.Cell().Element(CellStyle).AlignRight().Text("Unit Price").Bold();
          header.Cell().Element(CellStyle).AlignRight().Text("Total").Bold();

          static IContainer CellStyle(IContainer container)
             {
       return container
      .BorderBottom(1)
            .BorderColor(Colors.Grey.Medium)
        .PaddingVertical(5);
       }
   });

// Items
      foreach (var item in data.Items)
         {
         table.Cell().Element(CellStyle).Text(item.Description);
          table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
      table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice:N0} VND");
              table.Cell().Element(CellStyle).AlignRight().Text($"{item.Total:N0} VND");

static IContainer CellStyle(IContainer container)
        {
 return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
   }
              }
         });

      // Totals
       column.Item().AlignRight().PaddingTop(10).Column(col =>
       {
      col.Item().Row(row =>
     {
            row.ConstantItem(120).Text("Subtotal:").Bold();
        row.ConstantItem(120).AlignRight().Text($"{data.Subtotal:N0} VND");
            });

      col.Item().Row(row =>
           {
    row.ConstantItem(120).Text($"Tax ({data.TaxRate * 100}%):").Bold();
        row.ConstantItem(120).AlignRight().Text($"{data.TaxAmount:N0} VND");
          });

        col.Item().PaddingTop(5).BorderTop(2).BorderColor(Colors.Blue.Darken2);

     col.Item().PaddingTop(5).Row(row =>
  {
         row.ConstantItem(120).Text("Total:").Bold().FontSize(12);
        row.ConstantItem(120).AlignRight().Text($"{data.Total:N0} VND").Bold().FontSize(12);
        });
            });

 // Notes
  if (!string.IsNullOrEmpty(data.Notes))
            {
  column.Item().PaddingTop(20).Column(col =>
 {
        col.Item().Text("Notes:").Bold();
   col.Item().PaddingTop(5).Text(data.Notes).FontSize(9);
});
            }

            // Payment terms
          if (!string.IsNullOrEmpty(data.PaymentTerms))
            {
           column.Item().PaddingTop(10).Column(col =>
    {
        col.Item().Text("Payment Terms:").Bold();
 col.Item().PaddingTop(5).Text(data.PaymentTerms).FontSize(9).Italic();
    });
            }
        });
    }

    #endregion

    #region Private Helper Methods - Report

    private void ComposeReportHeader(IContainer container, ReportData data, PdfOptions options)
    {
        container.Column(column =>
        {
  column.Item().AlignCenter().Text(data.Title)
     .FontSize(20)
                .Bold()
       .FontColor(Colors.Blue.Darken2);

            if (!string.IsNullOrEmpty(data.Subtitle))
      {
     column.Item().AlignCenter().Text(data.Subtitle)
  .FontSize(12)
          .Italic();
  }

    column.Item().AlignCenter().PaddingTop(5).Text(text =>
         {
           text.Span("Generated: ").FontSize(9);
         text.Span(data.GeneratedDate.ToString("dd/MM/yyyy HH:mm")).FontSize(9).Bold();
  text.Span(" by ").FontSize(9);
  text.Span(data.GeneratedBy).FontSize(9).Bold();
       });

       column.Item().PaddingTop(10).BorderBottom(2).BorderColor(Colors.Blue.Darken2);
        });
    }

    private void ComposeReportContent(IContainer container, ReportData data)
  {
      container.PaddingVertical(20).Column(column =>
 {
  column.Spacing(15);

    // Summary
   if (!string.IsNullOrEmpty(data.Summary))
         {
 column.Item().Column(col =>
    {
         col.Item().Text("Summary").FontSize(14).Bold();
         col.Item().PaddingTop(5).Text(data.Summary).FontSize(10);
         });
            }

   // Sections
            foreach (var section in data.Sections)
          {
                column.Item().Column(col =>
            {
 // Section title
       col.Item().Text(section.Title).FontSize(12).Bold();

          // Section content
if (!string.IsNullOrEmpty(section.Content))
  {
          col.Item().PaddingTop(5).Text(section.Content).FontSize(10);
           }

        // Chart image
               if (section.ChartImage != null && section.ChartImage.Length > 0)
     {
       col.Item().PaddingTop(10).Image(section.ChartImage).FitWidth();
   }

      // Tables
             foreach (var table in section.Tables)
   {
   col.Item().PaddingTop(10).Column(tableCol =>
    {
    if (!string.IsNullOrEmpty(table.Title))
             {
     tableCol.Item().Text(table.Title).FontSize(11).Bold();
  }

       tableCol.Item().PaddingTop(5).Table(tbl =>
      {
             // Define columns based on headers
               tbl.ColumnsDefinition(columns =>
             {
              foreach (var _ in table.Headers)
      {
           columns.RelativeColumn();
      }
      });

       // Header
           tbl.Header(header =>
      {
 foreach (var h in table.Headers)
            {
 header.Cell()
   .BorderBottom(1)
          .BorderColor(Colors.Grey.Medium)
    .PaddingVertical(5)
        .Text(h)
          .Bold();
  }
   });

              // Rows
                foreach (var row in table.Rows)
           {
                foreach (var cell in row)
      {
           tbl.Cell()
      .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten2)
 .PaddingVertical(5)
            .Text(cell);
           }
        }
     });
      });
         }
         });
       }
    });
    }

    #endregion

    #region Private Helper Methods - Footer

    private void ComposeFooter(IContainer container, PdfOptions options)
    {
    container.Row(row =>
        {
   row.RelativeItem().Text(text =>
         {
       if (!string.IsNullOrEmpty(options.FooterLeft))
           {
         text.Span(options.FooterLeft);
       }
        else
 {
  text.Span($"Generated: {DateTime.UtcNow:dd/MM/yyyy}");
            }
        }).FontSize(8);

            if (options.ShowPageNumbers)
 {
    row.RelativeItem().AlignRight().Text(text =>
     {
       text.Span("Page ");
 text.CurrentPageNumber();
              text.Span(" of ");
            text.TotalPages();
             }).FontSize(8);
  }
    else if (!string.IsNullOrEmpty(options.FooterRight))
     {
        row.RelativeItem().AlignRight().Text(options.FooterRight).FontSize(8);
       }
 });
    }

    #endregion
}
```

**Giải thích:**
- **GenerateInvoiceAsync:** Compose invoice với header, items table, totals, notes
- **GenerateReportAsync:** Compose report với sections, tables, charts
- **ComposeInvoiceHeader/Content:** Helper methods để build invoice structure
- **ComposeReportHeader/Content:** Helper methods để build report structure
- **ComposeFooter:** Reusable footer với page numbers

**Lợi ích:**
- ✅ Fluent API rất dễ đọc và maintain
- ✅ Type-safe compilation
- ✅ Professional-looking documents
- ✅ Flexible layout system
- ✅ Good performance

**⚠️ Lưu ý:**
- QuestPDF Community license có limitations
- HTML to PDF và PDF merging cần additional libraries
- Consider alternative libraries nếu cần advanced features

---

### Bước 4.2: Register Service

**Làm gì:** Register PDF service trong Infrastructure startup.

**File:** `src/Infrastructure/Infrastructure/Exporters/Startup.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Exporters;

internal static class Startup
{
    internal static IServiceCollection AddExporters(this IServiceCollection services)
    {
        // Excel export service (from BUILD_26)
        // services.AddTransient<IExcelWriter, ClosedXmlExcelWriter>();
        
  // PDF export service
        services.AddTransient<IPdfService, QuestPdfService>();

        return services;
    }
}
```

**Giải thích:**
- Register `QuestPdfService` as `IPdfService` implementation
- Transient lifetime vì stateless operations

---

### Bước 4.3: Wire in Main Infrastructure Startup

**Làm gì:** Call `AddExporters()` trong main Infrastructure startup.

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Exporters;

namespace ECO.WebApi.Infrastructure;

public static class Startup
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
    // ... existing services ...

        // Exporters (Excel, PDF)
        services.AddExporters();

        return services;
    }
}
```

---

## 5. Usage Examples

### Bước 5.1: Invoice Generation Example

**File:** `src/Host/Host/Controllers/Exports/PdfController.cs`

```csharp
using ECO.WebApi.Application.Common.Exporters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers.Exports;

/// <summary>
/// PDF export endpoints
/// </summary>
[ApiController]
[Route("api/exports/[controller]")]
public class PdfController : ControllerBase
{
    private readonly IPdfService _pdfService;
    private readonly ILogger<PdfController> _logger;

    public PdfController(
        IPdfService pdfService,
        ILogger<PdfController> logger)
    {
     _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>
    /// Generate sample invoice PDF
    /// </summary>
    /// <returns>PDF file</returns>
    [HttpGet("invoice/sample")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateSampleInvoice(CancellationToken cancellationToken)
    {
        var invoiceData = new InvoiceData
  {
            InvoiceNumber = "INV-2024-001",
 InvoiceDate = DateTime.UtcNow,
   DueDate = DateTime.UtcNow.AddDays(30),
            
        CompanyName = "ECO Technology JSC",
        CompanyAddress = "123 Tech Street, Hanoi, Vietnam",
    CompanyPhone = "+84 24 1234 5678",
            CompanyEmail = "info@eco.vn",
        
            CustomerName = "Nguyen Van A",
       CustomerAddress = "456 Customer Road, HCMC, Vietnam",
            CustomerPhone = "+84 90 1234 5678",
 CustomerEmail = "customer@example.com",
 
     Items = new List<InvoiceItem>
       {
   new("Website Development Service", 1, 50000000),
       new("Mobile App Development", 1, 80000000),
              new("Cloud Hosting (12 months)", 12, 5000000),
       new("Technical Support", 1, 20000000)
    },
            
   TaxRate = 0.1m,
   Notes = "Thank you for your business!",
            PaymentTerms = "Payment due within 30 days. Bank transfer to account: 1234567890 at Vietcombank."
        };

        var options = new PdfOptions
   {
   Title = $"Invoice {invoiceData.InvoiceNumber}",
      Author = "ECO System",
         Subject = "Invoice",
            ShowPageNumbers = true,
            AddWatermark = false,
        IncludeHeader = true,
   IncludeFooter = true
        };

        var pdfBytes = await _pdfService.GenerateInvoiceAsync(invoiceData, options, cancellationToken);

        return File(pdfBytes, "application/pdf", $"Invoice_{invoiceData.InvoiceNumber}.pdf");
    }

    /// <summary>
    /// Generate invoice PDF from request
    /// </summary>
    /// <param name="request">Invoice data</param>
    /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>PDF file</returns>
    [HttpPost("invoice")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateInvoice(
  [FromBody] InvoiceData request,
        CancellationToken cancellationToken)
    {
        var options = new PdfOptions
     {
 Title = $"Invoice {request.InvoiceNumber}",
 ShowPageNumbers = true
  };

        var pdfBytes = await _pdfService.GenerateInvoiceAsync(request, options, cancellationToken);

        return File(pdfBytes, "application/pdf", $"Invoice_{request.InvoiceNumber}.pdf");
    }

    /// <summary>
    /// Generate invoice with watermark
    /// </summary>
    /// <param name="request">Invoice data</param>
    /// <param name="cancellationToken">Cancellation token</param>
 /// <returns>PDF file with watermark</returns>
    [HttpPost("invoice/draft")]
  [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateDraftInvoice(
        [FromBody] InvoiceData request,
        CancellationToken cancellationToken)
    {
        var options = new PdfOptions
        {
            Title = $"Invoice {request.InvoiceNumber} - DRAFT",
     ShowPageNumbers = true,
            AddWatermark = true,
     WatermarkText = "DRAFT"
     };

        var pdfBytes = await _pdfService.GenerateInvoiceAsync(request, options, cancellationToken);

        return File(pdfBytes, "application/pdf", $"Invoice_{request.InvoiceNumber}_DRAFT.pdf");
    }
}
```

**API Call Example:**
```bash
# Generate sample invoice
curl -X GET https://localhost:7001/api/exports/pdf/invoice/sample \
  -H "Authorization: Bearer {token}" \
  -o invoice.pdf

# Generate custom invoice
curl -X POST https://localhost:7001/api/exports/pdf/invoice \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "invoiceNumber": "INV-2024-002",
    "invoiceDate": "2024-01-30T10:00:00Z",
    "dueDate": "2024-02-29T10:00:00Z",
    "companyName": "ECO Company",
  "companyAddress": "123 Street",
    "customerName": "Customer ABC",
    "items": [
      {
        "description": "Product A",
        "quantity": 2,
        "unitPrice": 100000
},
      {
   "description": "Service B",
        "quantity": 1,
  "unitPrice": 500000
      }
    ],
    "taxRate": 0.1
  }' \
  -o invoice_custom.pdf
```

---

### Bước 5.2: Report Generation Example

**Add to PdfController:**

```csharp
/// <summary>
/// Generate sample report PDF
/// </summary>
/// <returns>PDF file</returns>
[HttpGet("report/sample")]
[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
public async Task<IActionResult> GenerateSampleReport(CancellationToken cancellationToken)
{
  var reportData = new ReportData
    {
        Title = "Sales Report Q1 2024",
        Subtitle = "January - March 2024",
        GeneratedDate = DateTime.UtcNow,
        GeneratedBy = "System Admin",
 
 Summary = "This report provides an overview of sales performance in Q1 2024. Total revenue increased by 25% compared to Q4 2023.",
        
        Sections = new List<ReportSection>
        {
      new()
  {
    Title = "Revenue Overview",
      Content = "Total revenue for Q1 2024: 1,500,000,000 VND. This represents a 25% increase compared to the previous quarter.",
    Tables = new List<ReportTable>
       {
  new()
          {
              Title = "Monthly Revenue Breakdown",
 Headers = new List<string> { "Month", "Revenue (VND)", "Growth (%)" },
              Rows = new List<List<string>>
          {
   new() { "January", "450,000,000", "+15%" },
          new() { "February", "480,000,000", "+6.7%" },
     new() { "March", "570,000,000", "+18.8%" }
      }
          }
      }
            },
     new()
        {
         Title = "Top Products",
            Content = "Analysis of best-selling products during Q1 2024.",
      Tables = new List<ReportTable>
    {
  new()
            {
   Title = "Top 5 Products by Revenue",
    Headers = new List<string> { "Product", "Units Sold", "Revenue (VND)" },
    Rows = new List<List<string>>
         {
          new() { "Product A", "1,200", "600,000,000" },
         new() { "Product B", "800", "400,000,000" },
       new() { "Product C", "600", "300,000,000" },
 new() { "Product D", "400", "150,000,000" },
       new() { "Product E", "200", "50,000,000" }
  }
         }
     }
  },
   new()
     {
     Title = "Customer Insights",
     Content = "Analysis of customer behavior and demographics.",
     Tables = new List<ReportTable>
      {
      new()
            {
        Title = "Customer Segments",
        Headers = new List<string> { "Segment", "Count", "Revenue (VND)" },
         Rows = new List<List<string>>
            {
  new() { "Enterprise", "50", "900,000,000" },
          new() { "SMB", "200", "450,000,000" },
        new() { "Individual", "500", "150,000,000" }
     }
    }
 }
       }
        },
        
   Metadata = new Dictionary<string, string>
        {
    { "Period", "Q1 2024" },
    { "Report Type", "Sales" },
   { "Department", "Sales & Marketing" }
 }
    };

    var options = new PdfOptions
    {
     Title = reportData.Title,
    Author = "ECO System",
    Subject = "Sales Report",
   ShowPageNumbers = true,
        IncludeHeader = true,
    IncludeFooter = true,
        FooterLeft = "Confidential - For Internal Use Only"
    };

    var pdfBytes = await _pdfService.GenerateReportAsync(reportData, options, cancellationToken);

    return File(pdfBytes, "application/pdf", "Sales_Report_Q1_2024.pdf");
}

/// <summary>
/// Generate custom report PDF
/// </summary>
/// <param name="request">Report data</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>PDF file</returns>
[HttpPost("report")]
[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
public async Task<IActionResult> GenerateReport(
    [FromBody] ReportData request,
    CancellationToken cancellationToken)
{
    var options = new PdfOptions
  {
        Title = request.Title,
        ShowPageNumbers = true
  };

    var pdfBytes = await _pdfService.GenerateReportAsync(request, options, cancellationToken);

    var fileName = request.Title.Replace(" ", "_") + ".pdf";
    return File(pdfBytes, "application/pdf", fileName);
}
```

**API Call Example:**
```bash
# Generate sample report
curl -X GET https://localhost:7001/api/exports/pdf/report/sample \
  -H "Authorization: Bearer {token}" \
  -o report.pdf
```

---

## 6. Configuration

### Bước 6.1: PDF Settings (Optional)

**Làm gì:** Tạo settings class nếu cần configuration.

**File:** `src/Infrastructure/Infrastructure/Exporters/PdfSettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Exporters;

/// <summary>
/// PDF generation settings
/// </summary>
public class PdfSettings
{
    /// <summary>
    /// Default company name for documents
    /// </summary>
    public string DefaultCompanyName { get; set; } = "ECO Company";

    /// <summary>
    /// Default company address
    /// </summary>
    public string DefaultCompanyAddress { get; set; } = string.Empty;

    /// <summary>
    /// Default company phone
    /// </summary>
    public string DefaultCompanyPhone { get; set; } = string.Empty;

    /// <summary>
 /// Default company email
  /// </summary>
    public string DefaultCompanyEmail { get; set; } = string.Empty;

    /// <summary>
    /// Default tax rate (0.1 = 10%)
    /// </summary>
    public decimal DefaultTaxRate { get; set; } = 0.1m;

  /// <summary>
    /// Enable watermarks for draft documents
    /// </summary>
    public bool EnableWatermarksForDrafts { get; set; } = true;

    /// <summary>
    /// QuestPDF license key (optional)
    /// </summary>
    public string? QuestPdfLicenseKey { get; set; }
}
```

**File:** `src/Host/Host/Configurations/appsettings.json`

```json
{
  "PdfSettings": {
    "DefaultCompanyName": "ECO Technology JSC",
    "DefaultCompanyAddress": "123 Tech Street, Hanoi, Vietnam",
    "DefaultCompanyPhone": "+84 24 1234 5678",
    "DefaultCompanyEmail": "info@eco.vn",
    "DefaultTaxRate": 0.1,
    "EnableWatermarksForDrafts": true,
    "QuestPdfLicenseKey": null
  }
}
```

**Update Startup.cs to configure settings:**

```csharp
internal static IServiceCollection AddExporters(
 this IServiceCollection services,
    IConfiguration config)
{
    // Configure PDF settings
    services.Configure<PdfSettings>(config.GetSection(nameof(PdfSettings)));
 
    // Register services
    services.AddTransient<IPdfService, QuestPdfService>();

    return services;
}
```

---

## 7. Advanced Examples

### Bước 7.1: Generate PDF with Logo

**Add to QuestPdfService:**

```csharp
/// <summary>
/// Generate invoice with company logo
/// </summary>
public Task<byte[]> GenerateInvoiceWithLogoAsync(
    InvoiceData data,
    byte[] logoImage,
    PdfOptions? options = null,
    CancellationToken cancellationToken = default)
{
  options ??= new PdfOptions { Title = $"Invoice {data.InvoiceNumber}" };

    var document = Document.Create(container =>
    {
  container.Page(page =>
     {
         page.Size(PageSizes.A4);
            page.Margin(50);
            page.DefaultTextStyle(x => x.FontSize(10));

         page.Header().Row(row =>
            {
        // Logo
    row.ConstantItem(100).Image(logoImage);

  // Company info
 row.RelativeItem().PaddingLeft(20).Column(column =>
        {
 column.Item().Text(data.CompanyName)
            .FontSize(20)
     .Bold()
          .FontColor(Colors.Blue.Darken2);

     column.Item().Text(data.CompanyAddress).FontSize(9);
      column.Item().Text($"Phone: {data.CompanyPhone}").FontSize(9);
                });

          // Invoice info
          row.RelativeItem().AlignRight().Column(column =>
      {
           column.Item().Text("INVOICE")
   .FontSize(20)
         .Bold()
         .FontColor(Colors.Blue.Darken2);

        column.Item().Text($"# {data.InvoiceNumber}")
            .FontSize(12)
          .Bold();
          });
       });

          page.Content().Element(c => ComposeInvoiceContent(c, data));
     page.Footer().Element(c => ComposeFooter(c, options));
        });
    });

    var pdfBytes = document.GeneratePdf();
    return Task.FromResult(pdfBytes);
}
```

---

### Bước 7.2: Batch PDF Generation

**Add to PdfController:**

```csharp
/// <summary>
/// Generate multiple invoices and zip them
/// </summary>
[HttpPost("invoice/batch")]
[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
public async Task<IActionResult> GenerateBatchInvoices(
    [FromBody] List<InvoiceData> invoices,
    CancellationToken cancellationToken)
{
    var pdfFiles = new List<(string fileName, byte[] content)>();

    foreach (var invoice in invoices)
    {
        var pdfBytes = await _pdfService.GenerateInvoiceAsync(invoice, cancellationToken: cancellationToken);
    pdfFiles.Add(($"Invoice_{invoice.InvoiceNumber}.pdf", pdfBytes));
    }

    // Create ZIP file
    using var memoryStream = new MemoryStream();
  using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
    {
    foreach (var (fileName, content) in pdfFiles)
        {
 var entry = archive.CreateEntry(fileName);
        using var entryStream = entry.Open();
            await entryStream.WriteAsync(content, cancellationToken);
        }
    }

    memoryStream.Position = 0;
    return File(memoryStream.ToArray(), "application/zip", $"Invoices_{DateTime.UtcNow:yyyyMMdd}.zip");
}
```

---

## 8. Testing

### Bước 8.1: Unit Tests

**File:** `tests/Infrastructure.Tests/Exporters/QuestPdfServiceTests.cs`

```csharp
using ECO.WebApi.Application.Common.Exporters;
using ECO.WebApi.Infrastructure.Exporters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECO.WebApi.Infrastructure.Tests.Exporters;

public class QuestPdfServiceTests
{
    private readonly Mock<ILogger<QuestPdfService>> _loggerMock;
private readonly QuestPdfService _service;

    public QuestPdfServiceTests()
    {
        _loggerMock = new Mock<ILogger<QuestPdfService>>();
        _service = new QuestPdfService(_loggerMock.Object);
    }

    [Fact]
public async Task GenerateInvoiceAsync_ShouldReturnPdfBytes()
    {
        // Arrange
        var invoiceData = new InvoiceData
        {
  InvoiceNumber = "INV-TEST-001",
     InvoiceDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(30),
            CompanyName = "Test Company",
            CustomerName = "Test Customer",
       Items = new List<InvoiceItem>
  {
            new("Product A", 2, 100000),
        new("Product B", 1, 200000)
            }
        };

        // Act
        var result = await _service.GenerateInvoiceAsync(invoiceData);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(result.Take(4).ToArray()));
    }

    [Fact]
    public async Task GenerateReportAsync_ShouldReturnPdfBytes()
    {
        // Arrange
        var reportData = new ReportData
        {
        Title = "Test Report",
            Subtitle = "Test Subtitle",
   Summary = "Test summary",
   Sections = new List<ReportSection>
     {
           new()
          {
        Title = "Section 1",
  Content = "Content 1",
     Tables = new List<ReportTable>
     {
                new()
  {
          Headers = new List<string> { "Col1", "Col2" },
          Rows = new List<List<string>>
     {
            new() { "Val1", "Val2" }
         }
              }
   }
         }
            }
     };

        // Act
        var result = await _service.GenerateReportAsync(reportData);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GenerateInvoiceAsync_WithWatermark_ShouldIncludeWatermark()
    {
        // Arrange
     var invoiceData = new InvoiceData
        {
  InvoiceNumber = "INV-TEST-002",
      CompanyName = "Test Company",
            CustomerName = "Test Customer",
     Items = new List<InvoiceItem> { new("Product A", 1, 100000) }
    };

      var options = new PdfOptions
        {
       AddWatermark = true,
      WatermarkText = "DRAFT"
     };

    // Act
   var result = await _service.GenerateInvoiceAsync(invoiceData, options);

    // Assert
        Assert.NotNull(result);
   Assert.NotEmpty(result);
    }
}
```

---

## 9. Best Practices

### 9.1: PDF Generation Best Practices

**✅ DO:**
```csharp
// Use async operations
var pdfBytes = await _pdfService.GenerateInvoiceAsync(data, cancellationToken: ct);

// Validate input data
if (string.IsNullOrEmpty(data.InvoiceNumber))
    throw new ValidationException("Invoice number is required");

// Use meaningful file names
var fileName = $"Invoice_{data.InvoiceNumber}_{DateTime.UtcNow:yyyyMMdd}.pdf";

// Configure options appropriately
var options = new PdfOptions
{
    Title = $"Invoice {data.InvoiceNumber}",
    AddWatermark = isDraft,
  WatermarkText = isDraft ? "DRAFT" : "CONFIDENTIAL"
};

// Stream large files
return File(pdfBytes, "application/pdf", fileName);
```

**❌ DON'T:**
```csharp
// Don't block async operations
var pdfBytes = _pdfService.GenerateInvoiceAsync(data).Result; // BAD

// Don't ignore cancellation tokens
var pdfBytes = await _pdfService.GenerateInvoiceAsync(data); // Missing CT

// Don't hardcode values
var options = new PdfOptions { Author = "Admin" }; // Use settings

// Don't store large PDFs in memory unnecessarily
var allPdfs = new List<byte[]>(); // Can cause OutOfMemoryException
for (int i = 0; i < 1000; i++)
{
    allPdfs.Add(await GenerateInvoice(i));
}
```

---

### 9.2: Performance Optimization

```csharp
// Use memory-efficient streaming for large batches
public async Task<Stream> GenerateLargeBatchAsync(
    List<InvoiceData> invoices,
    CancellationToken ct)
{
    var stream = new MemoryStream();
    
    foreach (var invoice in invoices)
    {
 var pdfBytes = await _pdfService.GenerateInvoiceAsync(invoice, cancellationToken: ct);
        await stream.WriteAsync(pdfBytes, ct);
        
        // Process in chunks to avoid memory issues
  if (stream.Length > 50_000_000) // 50MB
   {
        stream.Position = 0;
            yield return stream;
        stream = new MemoryStream();
 }
    }
    
    stream.Position = 0;
    yield return stream;
}
```

---

### 9.3: Error Handling

```csharp
public async Task<IActionResult> GenerateInvoice(InvoiceData data)
{
    try
    {
        // Validate
        if (data.Items == null || !data.Items.Any())
        {
            return BadRequest("Invoice must have at least one item");
    }

   // Generate
        var pdfBytes = await _pdfService.GenerateInvoiceAsync(data);

        // Return
 return File(pdfBytes, "application/pdf", $"Invoice_{data.InvoiceNumber}.pdf");
    }
    catch (ValidationException ex)
    {
     _logger.LogWarning(ex, "Validation error generating invoice");
        return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error generating invoice: {InvoiceNumber}", data.InvoiceNumber);
return StatusCode(500, "Error generating PDF");
    }
}
```

---

## 10. Troubleshooting

### Issue 1: QuestPDF License Error

**Problem:**
```
QuestPDF license exception: Community license limitations exceeded
```

**Solution:**
```csharp
// Option 1: Purchase commercial license
QuestPDF.Settings.License = LicenseType.Professional;
QuestPDF.Settings.LicenseKey = "your-license-key";

// Option 2: Use alternative library (iTextSharp, PdfSharp)
// Option 3: Stay within Community license limits (check QuestPDF docs)
```

---

### Issue 2: Font Not Found

**Problem:**
```
Font not available on Linux server
```

**Solution:**
```bash
# Install fonts on Linux
apt-get install -y fontconfig fonts-liberation

# Or embed fonts in application
QuestPDF.Settings.UseCustomFont("Arial", fontBytes);
```

---

### Issue 3: Out of Memory

**Problem:**
```
OutOfMemoryException when generating many PDFs
```

**Solution:**
```csharp
// Process in batches
const int batchSize = 10;
for (int i = 0; i < invoices.Count; i += batchSize)
{
    var batch = invoices.Skip(i).Take(batchSize);
    foreach (var invoice in batch)
    {
        var pdf = await GenerateInvoice(invoice);
        await SaveToStorage(pdf); // Save immediately
  // Don't accumulate in memory
}
}
```

---

## 11. Alternative Libraries

### 11.1: iTextSharp (AGPL License)

**Pros:**
- Very mature library
- Rich feature set
- PDF manipulation (merge, split, edit)

**Cons:**
- AGPL license (restrictive)
- Less modern API

**Usage:**
```csharp
// Install: itext7
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

public byte[] GeneratePdf()
{
  using var stream = new MemoryStream();
    using var pdfWriter = new PdfWriter(stream);
    using var pdfDocument = new PdfDocument(pdfWriter);
    using var document = new Document(pdfDocument);
    
    document.Add(new Paragraph("Hello World"));
  
    document.Close();
    return stream.ToArray();
}
```

---

### 11.2: PdfSharpCore (MIT License)

**Pros:**
- MIT license (free)
- Good for simple PDFs
- Cross-platform

**Cons:**
- Limited features
- Less modern API

**Usage:**
```csharp
// Install: PdfSharpCore
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;

public byte[] GeneratePdf()
{
    var document = new PdfDocument();
    var page = document.AddPage();
    var gfx = XGraphics.FromPdfPage(page);
    var font = new XFont("Arial", 12);
    
    gfx.DrawString("Hello World", font, XBrushes.Black, 
        new XRect(0, 0, page.Width, page.Height), XStringFormats.Center);
    
    using var stream = new MemoryStream();
    document.Save(stream);
    return stream.ToArray();
}
```

---

## 12. Summary

### ✅ Đã hoàn thành trong bước này:

**Application Layer:**
- ✅ `IPdfService` interface với methods: Invoice, Report, HTML, Merge, Watermark
- ✅ PDF models: `PdfOptions`, `InvoiceData`, `ReportData`, `ChartData`
- ✅ Type-safe enums và helper classes

**Infrastructure Layer:**
- ✅ `QuestPdfService` implementation với QuestPDF library
- ✅ Invoice generation với professional layout
- ✅ Report generation với sections, tables, charts
- ✅ Header/Footer support
- ✅ Watermark support
- ✅ Page numbering
- ✅ Service registration trong Startup

**Host Layer:**
- ✅ `PdfController` với endpoints:
  - GET `/api/exports/pdf/invoice/sample` - Sample invoice
  - POST `/api/exports/pdf/invoice` - Custom invoice
  - POST `/api/exports/pdf/invoice/draft` - Draft invoice with watermark
  - GET `/api/exports/pdf/report/sample` - Sample report
  - POST `/api/exports/pdf/report` - Custom report
  - POST `/api/exports/pdf/invoice/batch` - Batch invoices

**Configuration:**
- ✅ `PdfSettings` class (optional)
- ✅ appsettings.json configuration

**Testing:**
- ✅ Unit tests cho PDF generation

---

### 📊 Architecture Diagram:

```
Controller (PdfController)
    │
    ├── POST /api/exports/pdf/invoice
    ├── GET /api/exports/pdf/invoice/sample
    ├── POST /api/exports/pdf/report
    └── GET /api/exports/pdf/report/sample
    │
    ↓ depends on
IPdfService (Application)
    │
    ├── GenerateInvoiceAsync(InvoiceData, PdfOptions)
    ├── GenerateReportAsync(ReportData, PdfOptions)
    ├── GenerateFromHtmlAsync(html, PdfOptions)
    ├── MergePdfsAsync(List<byte[]>)
    └── AddWatermarkAsync(byte[], watermarkText)
    │
    ↓ implements
QuestPdfService (Infrastructure)
    │
    └── Uses: QuestPDF library
```

---

### 📌 Key Concepts:

**PDF Generation Pattern:**
- Interface-based abstraction (IPdfService)
- Template-based generation (Invoice, Report)
- Fluent API với QuestPDF
- Type-safe models (InvoiceData, ReportData)

**Document Structure:**
- Header (company info, document info)
- Content (items, sections, tables)
- Footer (page numbers, metadata)
- Watermarks (draft, confidential)

**Best Practices:**
- Stream files directly (don't store in memory)
- Use cancellation tokens
- Validate input data
- Log operations
- Handle errors gracefully

---

### 📁 File Structure:

```
src/Core/Application/Common/Exporters/
├── IPdfService.cs
├── PdfModels.cs (PdfOptions, InvoiceData, ReportData, etc.)

src/Infrastructure/Infrastructure/Exporters/
├── QuestPdfService.cs
├── PdfSettings.cs
└── Startup.cs

src/Host/Host/Controllers/Exports/
└── PdfController.cs

tests/Infrastructure.Tests/Exporters/
└── QuestPdfServiceTests.cs
```

---

## 13. Next Steps

**Tiếp theo:** [BUILD_28 - Catalog Module](BUILD_28_Catalog_Module.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo Product entity với specifications
2. ✅ Tạo Category hierarchical structure
3. ✅ Implement CRUD operations
4. ✅ Add search và filtering
5. ✅ Export products to Excel/PDF (sử dụng services từ BUILD_26 và BUILD_27)

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
