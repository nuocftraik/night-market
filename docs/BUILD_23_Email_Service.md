# Email Service - SMTP Mail với Razor Templates

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 20 (File Storage) đã hoàn thành

Tài liệu này hướng dẫn xây dựng Email Service - hệ thống gửi email với SMTP và render email templates bằng Razor Engine.

---

## 1. Overview

**Làm gì:** Xây dựng email service để:
- Gửi emails qua SMTP server (Gmail, SendGrid, MailGun...)
- Render email templates động với Razor Engine
- Support HTML emails với CSS inline
- Attachments (file đính kèm)
- CC, BCC, Reply-To
- Custom headers

**Tại sao cần:**
- **User Notifications:** Welcome emails, password reset, order confirmations
- **Transactional Emails:** Invoice, receipt, shipping notifications
- **Marketing:** Newsletters, promotions (nếu có)
- **System Alerts:** Error notifications, reports cho admins
- **Template Engine:** Dynamic content với Razor syntax (type-safe)

**Trong bước này chúng ta sẽ:**
- ✅ Tạo `MailRequest` DTO (email request model)
- ✅ Tạo `IMailService` interface (Application layer)
- ✅ Implement `SmtpMailService` với MailKit (Infrastructure layer)
- ✅ Tạo `IEmailTemplateService` interface
- ✅ Implement `EmailTemplateService` với RazorEngine
- ✅ Tạo email templates (Welcome, Password Reset)
- ✅ Setup SMTP configuration
- ✅ Usage examples (User registration, Password reset)

**Real-world example:**
```csharp
// User registration - Gửi welcome email
public class RegisterUserHandler : IRequestHandler<RegisterUserRequest, Guid>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailTemplateService _templateService;
    private readonly IMailService _mailService;

    public async Task<Guid> Handle(RegisterUserRequest request, CancellationToken ct)
    {
        // Create user
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
   await _userManager.CreateAsync(user, request.Password);

        // Generate email confirmation token
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = $"https://myapp.com/confirm-email?token={token}&email={user.Email}";

        // Render email template
    var model = new { UserName = user.UserName, Email = user.Email, Url = confirmUrl };
  var emailBody = _templateService.GenerateEmailTemplate("email-confirmation", model);

        // Send email
   var mailRequest = new MailRequest(
            to: new List<string> { user.Email },
            subject: "Welcome to ECO.WebApi - Confirm Your Email",
            body: emailBody
        );
        await _mailService.SendAsync(mailRequest, ct);

    return user.Id;
    }
}
```

---

## 2. Add Required Packages

### Bước 2.1: Infrastructure - MailKit Package

**Làm gì:** Add MailKit package cho SMTP email sending.

**Tại sao:** MailKit là modern, cross-platform email library (.NET Standard), thay thế System.Net.Mail.

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Email sending with SMTP -->
    <PackageReference Include="MailKit" Version="4.3.0" />
</ItemGroup>
```

**Giải thích:**
- `MailKit`: Modern email library, support SMTP, IMAP, POP3
- Cross-platform (Windows, Linux, macOS)
- Better performance than System.Net.Mail
- Support OAuth2 authentication (Gmail, Outlook)

---

### Bước 2.2: Infrastructure - RazorEngineCore Package

**Làm gì:** Add RazorEngineCore package cho template rendering.

**Tại sao:** Render Razor templates (.cshtml) outside of ASP.NET Core MVC context.

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Razor template engine -->
    <PackageReference Include="RazorEngineCore" Version="2022.8.1" />
</ItemGroup>
```

**Giải thích:**
- `RazorEngineCore`: Razor template engine cho .NET Core
- Compile .cshtml files to C# code dynamically
- Type-safe templates với strongly-typed models
- Standalone (không cần full MVC framework)

---

## 3. Application Layer - Interfaces & DTOs

### Bước 3.1: MailRequest DTO

**Làm gì:** Tạo DTO cho email request.

**Tại sao:** Type-safe email request model với tất cả email fields.

**File:** `src/Core/Application/Common/Mailing/MailRequest.cs`

```csharp
namespace ECO.WebApi.Application.Common.Mailing;

/// <summary>
/// Email request model
/// </summary>
public class MailRequest
{
    /// <summary>
    /// Constructor với required fields
    /// </summary>
    public MailRequest(
        List<string> to, 
        string subject, 
        string? body = null, 
        string? from = null, 
   string? displayName = null, 
 string? replyTo = null, 
 string? replyToName = null, 
        List<string>? bcc = null, 
        List<string>? cc = null, 
  IDictionary<string, byte[]>? attachmentData = null, 
        IDictionary<string, string>? headers = null)
    {
        To = to;
        Subject = subject;
        Body = body;
        From = from;
        DisplayName = displayName;
  ReplyTo = replyTo;
        ReplyToName = replyToName;
    Bcc = bcc ?? new List<string>();
     Cc = cc ?? new List<string>();
    AttachmentData = attachmentData ?? new Dictionary<string, byte[]>();
        Headers = headers ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Recipient email addresses
    /// </summary>
    public List<string> To { get; }

    /// <summary>
    /// Email subject
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Email body (HTML or plain text)
    /// </summary>
    public string? Body { get; }

    /// <summary>
    /// Sender email (nếu null, dùng default từ config)
    /// </summary>
    public string? From { get; }

    /// <summary>
    /// Sender display name
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Reply-To email address
    /// </summary>
    public string? ReplyTo { get; }

    /// <summary>
    /// Reply-To display name
    /// </summary>
    public string? ReplyToName { get; }

    /// <summary>
    /// BCC (Blind Carbon Copy) recipients
    /// </summary>
    public List<string> Bcc { get; }

  /// <summary>
    /// CC (Carbon Copy) recipients
    /// </summary>
 public List<string> Cc { get; }

/// <summary>
    /// Email attachments (filename → byte array)
    /// </summary>
    public IDictionary<string, byte[]> AttachmentData { get; }

    /// <summary>
    /// Custom email headers
    /// </summary>
    public IDictionary<string, string> Headers { get; }
}
```

**Giải thích:**

**Constructor parameters:**
- **to**: Required - danh sách recipients
- **subject**: Required - email subject
- **body**: Optional - HTML or plain text
- **from**: Optional - override default sender
- **bcc/cc**: Optional - additional recipients

**Immutable properties:**
- All properties are read-only (immutable after construction)
- Collections initialized với empty collections nếu null
- Safe to pass around without modification

**Usage example:**
```csharp
// Simple email
var request = new MailRequest(
    to: new List<string> { "user@example.com" },
    subject: "Welcome!",
    body: "<h1>Hello World</h1>"
);

// Complex email với attachments
var request = new MailRequest(
 to: new List<string> { "user@example.com" },
    subject: "Invoice #12345",
    body: htmlBody,
    cc: new List<string> { "manager@example.com" },
    attachmentData: new Dictionary<string, byte[]> 
    {
        ["invoice.pdf"] = pdfBytes
    }
);
```

---

### Bước 3.2: IMailService Interface

**Làm gì:** Tạo interface cho mail service.

**Tại sao:** Abstraction để dễ dàng switch email providers (SMTP → SendGrid → AWS SES).

**File:** `src/Core/Application/Common/Mailing/IMailService.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;

namespace ECO.WebApi.Application.Common.Mailing;

/// <summary>
/// Mail service interface
/// </summary>
public interface IMailService : ITransientService
{
    /// <summary>
  /// Send email
    /// </summary>
    /// <param name="request">Email request</param>
    /// <param name="ct">Cancellation token</param>
    Task SendAsync(MailRequest request, CancellationToken ct);
}
```

**Giải thích:**

**ITransientService:**
- Kế thừa marker interface → auto-register với DI container
- Transient lifetime: new instance mỗi lần inject

**SendAsync method:**
- Async operation (email sending có thể chậm)
- CancellationToken support (có thể cancel operation)
- Return Task (không return result, throw exception nếu fail)

**⚠️ Design decision:**
- Method không return `Task<bool>` success/failure
- Throw exception nếu send fail → caller handle
- Simpler interface, exception-based error handling

---

### Bước 3.3: IEmailTemplateService Interface

**Làm gì:** Tạo interface cho email template rendering.

**Tại sao:** Separate concerns - template rendering vs email sending.

**File:** `src/Core/Application/Common/Mailing/IEmailTemplateService.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;

namespace ECO.WebApi.Application.Common.Mailing;

/// <summary>
/// Email template service interface
/// </summary>
public interface IEmailTemplateService : ITransientService
{
    /// <summary>
    /// Generate email HTML từ Razor template
    /// </summary>
    /// <typeparam name="T">Template model type</typeparam>
    /// <param name="templateName">Template file name (without .cshtml)</param>
    /// <param name="mailTemplateModel">Template model data</param>
    /// <returns>Rendered HTML string</returns>
    string GenerateEmailTemplate<T>(string templateName, T mailTemplateModel);
}
```

**Giải thích:**

**Generic method `<T>`:**
- Type-safe template models
- Compile-time checking
- IntelliSense support trong templates

**templateName parameter:**
- File name without extension
- Example: `"email-confirmation"` → `email-confirmation.cshtml`

**Return string:**
- Rendered HTML content
- Ready to use trong MailRequest.Body

**Usage example:**
```csharp
// Define model
var model = new 
{ 
    UserName = "john.doe", 
    Email = "john@example.com",
    ConfirmUrl = "https://myapp.com/confirm?token=abc123"
};

// Render template
var html = _templateService.GenerateEmailTemplate("email-confirmation", model);

// Send email
var mailRequest = new MailRequest(
    to: new List<string> { model.Email },
    subject: "Confirm Your Email",
    body: html
);
await _mailService.SendAsync(mailRequest, ct);
```

---

## 4. Infrastructure Layer - SMTP Configuration

### Bước 4.1: SMTPEmailSettings

**Làm gì:** Tạo settings class cho SMTP configuration.

**Tại sao:** Type-safe configuration, easy to bind từ appsettings.json.

**File:** `src/Infrastructure/Infrastructure/Mailing/SMTPEmailSettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Mailing;

/// <summary>
/// SMTP email configuration settings
/// </summary>
public class SMTPEmailSettings
{
    /// <summary>
 /// Sender display name
    /// </summary>
  public string DisplayName { get; set; } = default!;

    /// <summary>
    /// Enable email verification (nếu false, không gửi email thực)
    /// </summary>
    public bool EnableVerification { get; set; }

    /// <summary>
    /// Default sender email address
    /// </summary>
    public string From { get; set; } = default!;

    /// <summary>
    /// SMTP server hostname
    /// </summary>
    public string SMTPServer { get; set; } = default!;

    /// <summary>
    /// Use SSL/TLS encryption
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// SMTP server port (25, 465, 587)
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// SMTP authentication username
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>
    /// SMTP authentication password
    /// </summary>
    public string Password { get; set; } = default!;
}
```

**Giải thích:**

**EnableVerification:**
- Development: `false` → log email thay vì gửi thực
- Production: `true` → gửi email thực

**Common SMTP ports:**
- **25**: Standard SMTP (không encrypted, ít dùng)
- **465**: SMTP over SSL (legacy, một số providers vẫn dùng)
- **587**: SMTP with STARTTLS (recommended)

**UseSsl:**
- `true`: SSL/TLS encryption
- `false`: Plain text connection (chỉ dùng development)

**⚠️ Security:**
- Không hardcode password trong code
- Dùng User Secrets (development) hoặc Azure Key Vault (production)
- Rotate credentials định kỳ

---

## 5. Infrastructure Layer - SmtpMailService

### Bước 5.1: SmtpMailService Implementation

**Làm gì:** Implement IMailService với MailKit SMTP client.

**Tại sao:** MailKit là production-ready, cross-platform email library.

**File:** `src/Infrastructure/Infrastructure/Mailing/SmtpMailService.cs`

```csharp
using ECO.WebApi.Application.Common.Mailing;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ECO.WebApi.Infrastructure.Mailing;

/// <summary>
/// SMTP mail service implementation (MailKit)
/// </summary>
public class SmtpMailService : IMailService
{
    private readonly SMTPEmailSettings _settings;
    private readonly ILogger<SmtpMailService> _logger;
    private readonly SmtpClient _smtpClient;

    public SmtpMailService(
        IOptions<SMTPEmailSettings> settings, 
 ILogger<SmtpMailService> logger)
    {
 _settings = settings.Value;
        _logger = logger;
    _smtpClient = new SmtpClient();
    }

    /// <summary>
    /// Send email via SMTP
    /// </summary>
    public async Task SendAsync(MailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
        // Create MimeMessage
            var email = new MimeMessage();

            // === Sender (From) ===
   email.From.Add(new MailboxAddress(
        _settings.DisplayName, 
   request.From ?? _settings.From));

         // === Recipients (To) ===
foreach (string address in request.To)
     email.To.Add(MailboxAddress.Parse(address));

            // === Reply To ===
  if (!string.IsNullOrEmpty(request.ReplyTo))
          email.ReplyTo.Add(new MailboxAddress(
   request.ReplyToName, 
      request.ReplyTo));

   // === BCC (Blind Carbon Copy) ===
            if (request.Bcc != null)
    {
      foreach (string address in request.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)))
        email.Bcc.Add(MailboxAddress.Parse(address.Trim()));
            }

// === CC (Carbon Copy) ===
            if (request.Cc != null)
   {
  foreach (string? address in request.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
     email.Cc.Add(MailboxAddress.Parse(address.Trim()));
        }

      // === Custom Headers ===
            if (request.Headers != null)
      {
      foreach (var header in request.Headers)
             email.Headers.Add(header.Key, header.Value);
       }

      // === Email Content ===
         var builder = new BodyBuilder();
 
            // Sender info
email.Sender = new MailboxAddress(
     request.DisplayName ?? _settings.DisplayName, 
   request.From ?? _settings.From);
            
            // Subject
 email.Subject = request.Subject;
      
            // HTML body
      builder.HtmlBody = request.Body;

          // === Attachments ===
          if (request.AttachmentData != null)
          {
  foreach (var attachment in request.AttachmentData)
  builder.Attachments.Add(attachment.Key, attachment.Value);
            }

  email.Body = builder.ToMessageBody();

  // === Send Email ===
 
       // Connect to SMTP server
   await _smtpClient.ConnectAsync(
    _settings.SMTPServer, 
              _settings.Port,
        _settings.UseSsl, 
    cancellationToken);

   // Authenticate
  await _smtpClient.AuthenticateAsync(
     _settings.Username, 
   _settings.Password, 
  cancellationToken);

   // Send message
   await _smtpClient.SendAsync(email, cancellationToken);

// Disconnect
            await _smtpClient.DisconnectAsync(quit: true, cancellationToken);
   }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}. Subject: {Subject}", 
         string.Join(", ", request.To), request.Subject);
         
    // Re-throw để caller handle
            throw;
    }
    }
}
```

**Giải thích:**

**MimeMessage construction:**
- **From**: Sender email (default từ config)
- **To**: Primary recipients (required)
- **ReplyTo**: Email address nhận replies (optional)
- **Bcc**: Hidden recipients (không thấy trong email)
- **Cc**: Visible copy recipients
- **Headers**: Custom headers (e.g., `X-Priority`, `X-Custom-Id`)

**BodyBuilder:**
- `HtmlBody`: HTML content (support CSS, images)
- `TextBody`: Plain text fallback (không dùng trong example)
- `Attachments`: Files đính kèm

**SMTP connection flow:**
1. **ConnectAsync**: Kết nối đến SMTP server
2. **AuthenticateAsync**: Login với username/password
3. **SendAsync**: Gửi email
4. **DisconnectAsync**: Đóng connection (`quit: true` → proper SMTP QUIT)

**Error handling:**
- Catch all exceptions
- Log error với context (recipients, subject)
- Re-throw exception → caller handle (retry logic, notify user...)

**⚠️ Performance consideration:**
- SmtpClient được reuse (instance variable)
- Không dispose trong `SendAsync` → reuse connection
- For high-volume: consider connection pooling hoặc background queue

---

## 6. Infrastructure Layer - EmailTemplateService

### Bước 6.1: EmailTemplateService Implementation

**Làm gì:** Implement IEmailTemplateService với RazorEngineCore.

**Tại sao:** Render Razor templates (.cshtml) để generate dynamic HTML emails.

**File:** `src/Infrastructure/Infrastructure/Mailing/EmailTemplateService.cs`

```csharp
using System.Text;
using ECO.WebApi.Application.Common.Mailing;
using RazorEngineCore;

namespace ECO.WebApi.Infrastructure.Mailing;

/// <summary>
/// Email template service (RazorEngineCore)
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    /// <summary>
    /// Generate email HTML từ Razor template
    /// </summary>
    public string GenerateEmailTemplate<T>(string templateName, T mailTemplateModel)
    {
        // Load template file
        string template = GetTemplate(templateName);

        // Compile Razor template
IRazorEngine razorEngine = new RazorEngine();
        IRazorEngineCompiledTemplate modifiedTemplate = razorEngine.Compile(template);

   // Render template với model
        return modifiedTemplate.Run(mailTemplateModel);
    }

    /// <summary>
    /// Load template file từ disk
    /// </summary>
    public static string GetTemplate(string templateName)
    {
        // Base directory: {AppDirectory}/Email Templates/
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    string tmplFolder = Path.Combine(baseDirectory, "Email Templates");
    string filePath = Path.Combine(tmplFolder, $"{templateName}.cshtml");

     // Read template file
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, Encoding.Default);
        string mailText = sr.ReadToEnd();
        sr.Close();

        return mailText;
    }
}
```

**Giải thích:**

**GenerateEmailTemplate flow:**
1. **Load template**: Read `.cshtml` file từ disk
2. **Compile**: Compile Razor syntax to C# code
3. **Render**: Execute compiled template với model data
4. **Return HTML**: Output string ready for email body

**Template location:**
- **Development**: `{ProjectDirectory}/Email Templates/`
- **Published**: `{PublishDirectory}/Email Templates/`
- **Example**: `Email Templates/email-confirmation.cshtml`

**RazorEngine compilation:**
- Compile template once → cache compiled version (performance)
- Template có access to model properties: `@Model.UserName`
- Support C# code: loops, conditionals, methods

**⚠️ Performance:**
- Compilation có cost → consider caching compiled templates
- For high-volume: pre-compile templates at startup
- Current implementation: compile mỗi lần (simple, acceptable for most cases)

**FileShare.ReadWrite:**
- Allow multiple processes read template (deployment scenarios)
- Safe for concurrent access

---

## 7. Email Templates

### Bước 7.1: Email Template Structure

**Làm gì:** Tạo email templates với Razor syntax.

**Tại sao:** Dynamic, type-safe HTML emails với reusable templates.

**Template folder structure:**
```
src/Host/Host/Email Templates/
├── email-confirmation.cshtml
├── password-reset.cshtml
├── welcome-email.cshtml
└── order-confirmation.cshtml
```

**⚠️ Important:**
- Templates phải copy to output directory
- Trong `.csproj`, add:
```xml
<ItemGroup>
    <None Update="Email Templates\**\*.cshtml">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

---

### Bước 7.2: Email Confirmation Template

**Làm gì:** Tạo template cho email confirmation.

**Tại sao:** User registration cần confirm email trước khi activate account.

**File:** `src/Host/Host/Email Templates/email-confirmation.cshtml`

```razor
<!DOCTYPE html>
<html>
<head>
    <title>Confirm Your Email</title>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta http-equiv="X-UA-Compatible" content="IE=edge" />
    <style type="text/css">
        /* Reset styles */
        body, table, td, a {
            -webkit-text-size-adjust: 100%;
            -ms-text-size-adjust: 100%;
        }
        table, td {
     mso-table-lspace: 0pt;
         mso-table-rspace: 0pt;
        }
        img {
   -ms-interpolation-mode: bicubic;
            border: 0;
   height: auto;
            line-height: 100%;
     outline: none;
    text-decoration: none;
      }
      table {
   border-collapse: collapse !important;
        }
    body {
            height: 100% !important;
      margin: 0 !important;
    padding: 0 !important;
       width: 100% !important;
            background-color: #f4f4f4;
        }

    /* Responsive */
        @("@")media screen and (max-width:600px) {
     h1 {
       font-size: 32px !important;
             line-height: 32px !important;
         }
        }
    </style>
</head>
<body style="background-color: #f4f4f4; margin: 0 !important; padding: 0 !important;">
    <!-- Container -->
  <table border="0" cellpadding="0" cellspacing="0" width="100%">
        <!-- Header -->
        <tr>
            <td bgcolor="#3eaf7c" align="center" style="padding: 40px 10px 40px 10px;">
    <h1 style="color: #ffffff; font-family: Arial, sans-serif; font-size: 48px; font-weight: 400; margin: 0;">
         ECO.WebApi
     </h1>
            </td>
      </tr>

        <!-- Welcome Message -->
   <tr>
            <td bgcolor="#3eaf7c" align="center" style="padding: 0px 10px 0px 10px;">
    <table border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 600px;">
      <tr>
        <td bgcolor="#ffffff" align="center" valign="top"
      style="padding: 40px 20px 20px 20px; border-radius: 4px 4px 0px 0px; color: #111111; font-family: Arial, sans-serif; font-size: 48px; font-weight: 400; line-height: 48px;">
        <h1 style="font-size: 48px; font-weight: 400; margin: 2px;">Welcome!</h1>
          <img src="https://img.icons8.com/clouds/100/000000/handshake.png" width="125" height="120"
   style="display: block; border: 0px;" />
       </td>
       </tr>
                </table>
         </td>
        </tr>

        <!-- Body Content -->
        <tr>
<td bgcolor="#f4f4f4" align="center" style="padding: 0px 10px 0px 10px;">
        <table border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 600px;">
   <!-- Main Message -->
         <tr>
             <td bgcolor="#ffffff" align="left"
              style="padding: 20px 30px 40px 30px; color: #666666; font-family: Arial, sans-serif; font-size: 18px; font-weight: 400; line-height: 25px;">
      <p style="margin: 0;">
  Hi @Model?.UserName! We're excited to have you get started. 
        First, you need to confirm your email - <strong>@Model?.Email</strong> for this account. 
                Just press the button below.
   </p>
          </td>
         </tr>

       <!-- CTA Button -->
       <tr>
    <td bgcolor="#ffffff" align="left">
   <table width="100%" border="0" cellspacing="0" cellpadding="0">
 <tr>
  <td bgcolor="#ffffff" align="center" style="padding: 20px 30px 60px 30px;">
 <table border="0" cellspacing="0" cellpadding="0">
         <tr>
 <td align="center" style="border-radius: 5px;" bgcolor="#3eaf7c">
                <a href="@Model?.Url" target="_blank"
          style="font-size: 20px; font-family: Arial, sans-serif; color: #ffffff; text-decoration: none; padding: 15px 25px; border-radius: 2px; border: 1px solid #3eaf7c; display: inline-block;">
                  Confirm Account
         </a>
      </td>
      </tr>
   </table>
      </td>
        </tr>
         </table>
           </td>
         </tr>

        <!-- Fallback Link -->
           <tr>
       <td bgcolor="#ffffff" align="left"
         style="padding: 0px 30px 0px 30px; color: #666666; font-family: Arial, sans-serif; font-size: 18px; font-weight: 400; line-height: 25px;">
<p style="margin: 0;">
         If that doesn't work, copy and paste the following link in your browser:
       </p>
          </td>
      </tr>
    <tr>
 <td bgcolor="#ffffff" align="left"
           style="padding: 20px 30px 20px 30px; color: #666666; font-family: Arial, sans-serif; font-size: 18px; font-weight: 400; line-height: 25px; overflow-wrap: anywhere;">
      <p style="margin: 0;">
  <a href="@Model?.Url" target="_blank" style="color: #3eaf7c;">@Model?.Url</a>
     </p>
       </td>
                </tr>

      <!-- Footer -->
  <tr>
       <td bgcolor="#ffffff" align="left"
              style="padding: 0px 30px 40px 30px; border-radius: 0px 0px 4px 4px; color: #666666; font-family: Arial, sans-serif; font-size: 18px; font-weight: 400; line-height: 25px;">
     <p style="margin: 0;">
            Cheers,<br>
     The ECO.WebApi Team
  </p>
  </td>
       </tr>
      </table>
            </td>
    </tr>
    </table>
</body>
</html>
```

**Giải thích:**

**Razor syntax:**
- `@Model?.UserName`: Access model properties (null-safe)
- `@Model?.Email`: Email address
- `@Model?.Url`: Confirmation URL

**Template model:**
```csharp
var model = new 
{
    UserName = "john.doe",
    Email = "john@example.com",
    Url = "https://myapp.com/confirm-email?token=abc123"
};
```

**Email design best practices:**
- **Inline CSS**: Email clients không support `<style>` tags tốt
- **Table-based layout**: Flexbox/Grid không work trong email
- **Responsive**: `@media` queries cho mobile
- **Fallback link**: Button có thể không work, provide plain link

---

### Bước 7.3: Password Reset Template

**Làm gì:** Tạo template cho password reset.

**Tại sao:** User quên password cần reset link.

**File:** `src/Host/Host/Email Templates/password-reset.cshtml`

```razor
<!DOCTYPE html>
<html>
<head>
    <title>Reset Your Password</title>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style type="text/css">
     body {
            margin: 0;
    padding: 0;
  background-color: #f4f4f4;
            font-family: Arial, sans-serif;
        }
        .container {
          max-width: 600px;
            margin: 0 auto;
         background-color: #ffffff;
        }
        .header {
            background-color: #3eaf7c;
     padding: 40px;
          text-align: center;
            color: #ffffff;
        }
    .content {
  padding: 40px;
            color: #666666;
            font-size: 16px;
     line-height: 24px;
     }
     .button {
            display: inline-block;
 padding: 15px 30px;
   background-color: #3eaf7c;
  color: #ffffff !important;
       text-decoration: none;
    border-radius: 5px;
            font-weight: bold;
            margin: 20px 0;
        }
  .footer {
          padding: 20px 40px;
   background-color: #f9f9f9;
            text-align: center;
    color: #999999;
       font-size: 14px;
        }
    </style>
</head>
<body>
    <table width="100%" cellpadding="0" cellspacing="0" border="0">
        <tr>
            <td align="center" style="padding: 20px;">
 <table class="container" width="600" cellpadding="0" cellspacing="0" border="0">
     <!-- Header -->
 <tr>
   <td class="header">
 <h1 style="margin: 0; font-size: 32px;">Password Reset</h1>
     </td>
          </tr>

        <!-- Content -->
        <tr>
       <td class="content">
            <p>Hi @Model?.UserName,</p>
   <p>
            You recently requested to reset your password for your ECO.WebApi account. 
            Click the button below to reset it.
      </p>
  
    <div style="text-align: center;">
      <a href="@Model?.Url" class="button" target="_blank">
    Reset Password
    </a>
   </div>

      <p>
  <strong>This password reset link will expire in 24 hours.</strong>
     </p>

    <p>
                If you did not request a password reset, please ignore this email or contact support if you have concerns.
            </p>

    <p>
        If the button doesn't work, copy and paste this link into your browser:
      </p>
 <p style="word-break: break-all;">
    <a href="@Model?.Url" style="color: #3eaf7c;">@Model?.Url</a>
         </p>
     </td>
       </tr>

           <!-- Footer -->
  <tr>
 <td class="footer">
     <p>
        This is an automated message, please do not reply to this email.
         </p>
 <p>
    &copy; @DateTime.Now.Year ECO.WebApi. All rights reserved.
           </p>
    </td>
   </tr>
    </table>
            </td>
   </tr>
    </table>
</body>
</html>
```

**Giải thích:**

**Template model:**
```csharp
var model = new 
{
    UserName = "john.doe",
    Url = "https://myapp.com/reset-password?token=abc123"
};
```

**Security considerations:**
- **Expiration notice**: Link expires in 24 hours
- **Ignore instruction**: If user didn't request, ignore email
- **No sensitive data**: Không include password trong email

**Razor C# code:**
- `@DateTime.Now.Year`: Current year trong footer
- Có thể dùng bất kỳ C# code trong template

---

## 8. Configuration

### Bước 8.1: appsettings.json Configuration

**Làm gì:** Configure SMTP settings trong appsettings.json.

**Tại sao:** Externalized configuration, easy to change per environment.

**File:** `src/Host/Host/Configurations/mail.json` (hoặc trong `appsettings.json`)

```json
{
  "SMTPEmailSettings": {
    "DisplayName": "ECO.WebApi",
    "EnableVerification": true,
    "From": "noreply@ecowebapi.com",
    "SMTPServer": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "UseSsl": true
  }
}
```

**Giải thích:**

**Gmail SMTP:**
- **SMTPServer**: `smtp.gmail.com`
- **Port**: `587` (STARTTLS) or `465` (SSL)
- **Username**: Your Gmail address
- **Password**: [App Password](https://support.google.com/accounts/answer/185833) (not your regular password)

**Other SMTP providers:**

**SendGrid:**
```json
{
  "SMTPServer": "smtp.sendgrid.net",
  "Port": 587,
  "Username": "apikey",
  "Password": "your-sendgrid-api-key"
}
```

**Mailgun:**
```json
{
  "SMTPServer": "smtp.mailgun.org",
  "Port": 587,
  "Username": "postmaster@your-domain.mailgun.org",
  "Password": "your-mailgun-password"
}
```

**Microsoft 365:**
```json
{
  "SMTPServer": "smtp.office365.com",
  "Port": 587,
  "Username": "your-email@yourdomain.com",
  "Password": "your-password"
}
```

**⚠️ Security - User Secrets (Development):**
```bash
# Set password using User Secrets
cd src/Host/Host
dotnet user-secrets set "SMTPEmailSettings:Password" "your-app-password"
```

**⚠️ Security - Azure Key Vault (Production):**
```json
{
  "KeyVault": {
    "VaultUri": "https://your-vault.vault.azure.net/"
  },
  "SMTPEmailSettings": {
    "Password": "#{SMTPPassword}#"  // Reference to Key Vault secret
  }
}
```

---

### Bước 8.2: Infrastructure Startup Registration

**Làm gì:** Register email services trong DI container.

**Tại sao:** Auto-wire dependencies, configure options.

**File:** `src/Infrastructure/Infrastructure/Mailing/Startup.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Mailing;

/// <summary>
/// Mailing startup configuration
/// </summary>
internal static class Startup
{
    /// <summary>
    /// Register email services
    /// </summary>
    internal static IServiceCollection AddMailing(this IServiceCollection services, IConfiguration config)
    {
 // Bind SMTPEmailSettings từ configuration
    services.Configure<SMTPEmailSettings>(config.GetSection(nameof(SMTPEmailSettings)));

        // Services auto-registered via ITransientService:
        // - IMailService → SmtpMailService
        // - IEmailTemplateService → EmailTemplateService

        return services;
    }
}
```

**Giải thích:**

**Configure<T>:**
- Bind `SMTPEmailSettings` section từ appsettings.json
- Inject via `IOptions<SMTPEmailSettings>`

**Auto-registration:**
- `SmtpMailService` implement `IMailService : ITransientService`
- `EmailTemplateService` implement `IEmailTemplateService : ITransientService`
- Auto-discovered by Service Registration pattern (BUILD_08)

---

### Bước 8.3: Main Infrastructure Startup

**Làm gì:** Call `AddMailing()` trong main Infrastructure startup.

**Tại sao:** Modular startup pattern.

**File:** `src/Infrastructure/Infrastructure/Startup.cs`

Thêm dòng này trong `AddInfrastructure()`:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    // ... existing services ...

    // Mailing services
    services.AddMailing(config);

    // ... existing services ...

    return services;
}
```

---

### Bước 8.4: Host Project - Copy Email Templates

**Làm gì:** Configure email templates copy to output directory.

**Tại sao:** Templates phải available khi run application.

**File:** `src/Host/Host/Host.csproj`

Thêm ItemGroup này:

```xml
<ItemGroup>
    <!-- Copy email templates to output directory -->
    <None Update="Email Templates\**\*.cshtml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

**Giải thích:**

**`Email Templates\**\*.cshtml`:**
- Match all `.cshtml` files trong `Email Templates` folder và subfolders

**`PreserveNewest`:**
- Copy file to output nếu source mới hơn destination
- Không copy nếu destination up-to-date (faster builds)

**Output location:**
- Debug: `bin/Debug/net8.0/Email Templates/`
- Publish: `publish/Email Templates/`

---

## 9. Usage Examples

### Bước 9.1: User Registration - Send Welcome Email

**Scenario:** User đăng ký account mới, gửi welcome email với email confirmation link.

**Handler:**
```csharp
using ECO.WebApi.Application.Common.Mailing;
using ECO.WebApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ECO.WebApi.Application.Identity.Users;

/// <summary>
/// Register new user request
/// </summary>
public class RegisterUserRequest : IRequest<Guid>
{
    public string Email { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
}

/// <summary>
/// Register new user handler
/// </summary>
public class RegisterUserHandler : IRequestHandler<RegisterUserRequest, Guid>
{
 private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailTemplateService _templateService;
    private readonly IMailService _mailService;
    private readonly IConfiguration _configuration;

    public RegisterUserHandler(
        UserManager<ApplicationUser> userManager,
        IEmailTemplateService templateService,
        IMailService mailService,
      IConfiguration configuration)
    {
  _userManager = userManager;
        _templateService = templateService;
        _mailService = mailService;
 _configuration = configuration;
 }

    public async Task<Guid> Handle(RegisterUserRequest request, CancellationToken ct)
    {
   // 1. Create user
        var user = new ApplicationUser
        {
         Id = Guid.NewGuid(),
       UserName = request.UserName,
            Email = request.Email,
         FirstName = request.FirstName,
         LastName = request.LastName,
   EmailConfirmed = false // Chưa confirm email
        };

 var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
  throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
 }

   // 2. Generate email confirmation token
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
 var encodedToken = WebUtility.UrlEncode(token);

        // 3. Build confirmation URL
        var baseUrl = _configuration["AppUrl"] ?? "https://localhost:7001";
        var confirmUrl = $"{baseUrl}/api/identity/users/confirm-email?userId={user.Id}&token={encodedToken}";

        // 4. Render email template
        var templateModel = new
        {
            UserName = user.UserName,
  Email = user.Email,
Url = confirmUrl
        };
        var emailBody = _templateService.GenerateEmailTemplate("email-confirmation", templateModel);

        // 5. Send email
        var mailRequest = new MailRequest(
            to: new List<string> { user.Email },
     subject: "Welcome to ECO.WebApi - Confirm Your Email",
body: emailBody
     );

        await _mailService.SendAsync(mailRequest, ct);

        return user.Id;
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
[Route("api/identity/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Register new user
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<Guid>> Register([FromBody] RegisterUserRequest request)
    {
  var userId = await _mediator.Send(request);
  return CreatedAtAction(nameof(GetUser), new { id = userId }, userId);
    }

    /// <summary>
    /// Confirm email
    /// </summary>
    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
  var decodedToken = WebUtility.UrlDecode(token);
      var user = await _userManager.FindByIdAsync(userId.ToString());
        
   if (user == null)
      return NotFound("User not found");

   var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        
  if (!result.Succeeded)
            return BadRequest("Email confirmation failed");

        return Ok("Email confirmed successfully! You can now login.");
  }
}
```

**API Calls:**

**1. Register user:**
```bash
curl -X POST https://localhost:7001/api/identity/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "userName": "john.doe",
    "password": "P@ssw0rd123!",
    "firstName": "John",
    "lastName": "Doe"
  }'
```

**Response:**
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**2. User nhận email với button "Confirm Account"**

**3. User click button → redirect to confirm-email endpoint:**
```
GET https://localhost:7001/api/identity/users/confirm-email?userId=550e8400-e29b-41d4-a716-446655440000&token=CfDJ8...
```

**Response:**
```
Email confirmed successfully! You can now login.
```

---

### Bước 9.2: Password Reset - Send Reset Link

**Scenario:** User quên password, request reset link qua email.

**Request DTO:**
```csharp
using MediatR;

namespace ECO.WebApi.Application.Identity.Users;

public class ForgotPasswordRequest : IRequest<string>
{
    public string Email { get; set; } = default!;
}
```

**Handler:**
```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.Mailing;
using ECO.WebApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ECO.WebApi.Application.Identity.Users;

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordRequest, string>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailTemplateService _templateService;
    private readonly IMailService _mailService;
    private readonly IConfiguration _configuration;

    public ForgotPasswordHandler(
        UserManager<ApplicationUser> userManager,
    IEmailTemplateService templateService,
        IMailService mailService,
        IConfiguration configuration)
    {
        _userManager = userManager;
  _templateService = templateService;
        _mailService = mailService;
    _configuration = configuration;
    }

    public async Task<string> Handle(ForgotPasswordRequest request, CancellationToken ct)
    {
        // 1. Find user by email
        var user = await _userManager.FindByEmailAsync(request.Email);
      if (user == null)
        {
      // Security: Don't reveal if email exists
        return "If the email exists, a password reset link has been sent.";
     }

   // 2. Generate password reset token
    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
      var encodedToken = WebUtility.UrlEncode(token);

  // 3. Build reset URL
var baseUrl = _configuration["AppUrl"] ?? "https://localhost:7001";
        var resetUrl = $"{baseUrl}/reset-password?email={user.Email}&token={encodedToken}";

        // 4. Render email template
     var templateModel = new
     {
       UserName = user.UserName,
Url = resetUrl
        };
        var emailBody = _templateService.GenerateEmailTemplate("password-reset", templateModel);

        // 5. Send email
        var mailRequest = new MailRequest(
      to: new List<string> { user.Email },
            subject: "ECO.WebApi - Password Reset Request",
  body: emailBody
        );

    await _mailService.SendAsync(mailRequest, ct);

     return "If the email exists, a password reset link has been sent.";
    }
}
```

**Controller:**
```csharp
[HttpPost("forgot-password")]
[AllowAnonymous]
public async Task<ActionResult<string>> ForgotPassword([FromBody] ForgotPasswordRequest request)
{
    var result = await _mediator.Send(request);
    return Ok(result);
}

[HttpPost("reset-password")]
[AllowAnonymous]
public async Task<ActionResult<string>> ResetPassword([FromBody] ResetPasswordRequest request)
{
    var decodedToken = WebUtility.UrlDecode(request.Token);
    var user = await _userManager.FindByEmailAsync(request.Email);
    
    if (user == null)
   return BadRequest("Invalid request");

    var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
    
    if (!result.Succeeded)
        return BadRequest($"Failed to reset password: {string.Join(", ", result.Errors.Select(e => e.Description))}");

    return Ok("Password reset successfully! You can now login with your new password.");
}
```

**API Calls:**

**1. Request password reset:**
```bash
curl -X POST https://localhost:7001/api/identity/users/forgot-password \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com"
  }'
```

**Response:**
```json
{
  "message": "If the email exists, a password reset link has been sent."
}
```

**2. User nhận email với button "Reset Password"**

**3. User click button → frontend form để nhập new password**

**4. Frontend submit reset:**
```bash
curl -X POST https://localhost:7001/api/identity/users/reset-password \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "token": "CfDJ8...",
"newPassword": "NewP@ssw0rd123!"
  }'
```

**Response:**
```json
{
  "message": "Password reset successfully! You can now login with your new password."
}
```

---

### Bước 9.3: Order Confirmation Email với Attachment

**Scenario:** User đặt hàng, gửi email confirmation với invoice PDF attachment.

**Usage example:**
```csharp
using ECO.WebApi.Application.Common.Mailing;

public class SendOrderConfirmationEmailHandler
{
    private readonly IEmailTemplateService _templateService;
    private readonly IMailService _mailService;
private readonly IPdfGenerator _pdfGenerator; // Assume we have this

    public async Task SendOrderConfirmationAsync(Order order, CancellationToken ct)
    {
        // 1. Generate invoice PDF
        byte[] invoicePdf = await _pdfGenerator.GenerateInvoicePdfAsync(order);

        // 2. Render email template
        var templateModel = new
        {
            OrderId = order.Id,
     CustomerName = order.CustomerName,
  OrderDate = order.CreatedOn.ToString("yyyy-MM-dd"),
    TotalAmount = order.TotalAmount.ToString("C"),
          Items = order.Items.Select(i => new { i.ProductName, i.Quantity, Price = i.Price.ToString("C") })
        };
        var emailBody = _templateService.GenerateEmailTemplate("order-confirmation", templateModel);

        // 3. Send email với attachment
        var mailRequest = new MailRequest(
        to: new List<string> { order.CustomerEmail },
        subject: $"Order Confirmation - #{order.Id}",
   body: emailBody,
            attachmentData: new Dictionary<string, byte[]>
      {
                [$"Invoice-{order.Id}.pdf"] = invoicePdf
      }
        );

        await _mailService.SendAsync(mailRequest, ct);
    }
}
```

---

### Bước 9.4: Batch Email Sending (Newsletter)

**Scenario:** Gửi email đến nhiều users (newsletter, promotions).

**⚠️ Best practice:** Use background job (Hangfire) để send emails asynchronously.

```csharp
using ECO.WebApi.Application.Common.Mailing;
using Hangfire;

public class SendNewsletterHandler
{
    private readonly IEmailTemplateService _templateService;
    private readonly IBackgroundJobClient _jobClient;

    public async Task SendNewsletterAsync(Newsletter newsletter, List<string> recipients)
    {
    // Render template once
    var emailBody = _templateService.GenerateEmailTemplate("newsletter", newsletter);

        // Queue emails in background
        foreach (var email in recipients)
  {
  _jobClient.Enqueue<IMailService>(mailService => 
       mailService.SendAsync(
         new MailRequest(
     to: new List<string> { email },
         subject: newsletter.Subject,
              body: emailBody
        ),
      CancellationToken.None
   ));
        }
    }
}
```

**⚠️ Rate limiting:**
- SMTP providers có rate limits (e.g., Gmail: 500 emails/day)
- Use commercial email services (SendGrid, Mailgun) cho high-volume
- Implement retry logic với exponential backoff

---

## 10. Best Practices & Guidelines

### 10.1: Email Deliverability

**Problem:** Emails đi vào spam folder.

**Solutions:**

**1. SPF (Sender Policy Framework):**
```
TXT record for @yourdomain.com:
v=spf1 include:_spf.google.com ~all
```

**2. DKIM (DomainKeys Identified Mail):**
- Sign emails với private key
- Recipient verify với public key trong DNS
- Most SMTP providers handle automatically

**3. DMARC (Domain-based Message Authentication):**
```
TXT record for _dmarc.yourdomain.com:
v=DMARC1; p=quarantine; rua=mailto:postmaster@yourdomain.com
```

**4. Avoid spam triggers:**
- ❌ Không dùng ALL CAPS trong subject
- ❌ Không spam keywords: "FREE", "WINNER", "CLICK NOW"
- ✅ Include unsubscribe link
- ✅ Use real "From" address (không noreply@...)
- ✅ Balance text/image ratio
- ✅ Test emails với [Mail Tester](https://www.mail-tester.com/)

---

### 10.2: Template Maintenance

**Problem:** Templates khó maintain, duplicate code.

**Solution:** Use partial templates (layout).

**Base layout template:**
```razor
<!-- Email Templates/_Layout.cshtml -->
<!DOCTYPE html>
<html>
<head>
    @RenderSection("Head", required: false)
    <style>
 /* Common styles */
        body { font-family: Arial, sans-serif; }
     .header { background: #3eaf7c; color: white; }
        .footer { background: #f9f9f9; }
    </style>
</head>
<body>
    <div class="header">
    <h1>ECO.WebApi</h1>
    </div>
    
    <div class="content">
      @RenderBody()
    </div>
    
    <div class="footer">
     <p>&copy; @DateTime.Now.Year ECO.WebApi</p>
    </div>
</body>
</html>
```

**Child template:**
```razor
<!-- Email Templates/welcome.cshtml -->
@{
    Layout = "_Layout";
}

@section Head {
    <title>Welcome</title>
}

<p>Hi @Model.UserName,</p>
<p>Welcome to ECO.WebApi!</p>
```

---

### 10.3: Email Testing

**Problem:** Test emails trong development without spamming real users.

**Solutions:**

**1. MailHog (Local SMTP server):**
```bash
# Docker
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog

# Configuration
{
  "SMTPEmailSettings": {
    "SMTPServer": "localhost",
  "Port": 1025,
    "EnableVerification": true
  }
}

# View emails: http://localhost:8025
```

**2. Mailtrap (Online testing):**
```json
{
  "SMTPServer": "smtp.mailtrap.io",
  "Port": 587,
  "Username": "your-mailtrap-username",
  "Password": "your-mailtrap-password"
}
```

**3. Papercut (Windows SMTP server):**
- Download: https://github.com/ChangemakerStudios/Papercut-SMTP
- Capture all outgoing emails
- No configuration needed

---

### 10.4: Email Queueing

**Problem:** Email sending block request → slow API response.

**Solution:** Queue emails trong background job.

```csharp
using Hangfire;

public class EmailQueueService
{
    private readonly IBackgroundJobClient _jobClient;

 public EmailQueueService(IBackgroundJobClient jobClient)
    {
   _jobClient = jobClient;
    }

    public void QueueEmail(MailRequest request)
  {
        // Queue trong Hangfire
        _jobClient.Enqueue<IMailService>(mailService => 
   mailService.SendAsync(request, CancellationToken.None));
    }

    public void QueueScheduledEmail(MailRequest request, DateTime sendAt)
    {
        // Schedule email send
        _jobClient.Schedule<IMailService>(
        mailService => mailService.SendAsync(request, CancellationToken.None),
    sendAt);
    }
}
```

**Usage:**
```csharp
// Immediate send (background)
_emailQueue.QueueEmail(mailRequest);

// Scheduled send (e.g., reminder email)
var reminderTime = DateTime.UtcNow.AddHours(24);
_emailQueue.QueueScheduledEmail(reminderRequest, reminderTime);
```

---

### 10.5: Retry Logic

**Problem:** Email sending fail (network issue, SMTP down).

**Solution:** Implement retry with exponential backoff.

```csharp
using Polly;

public class ResilientMailService : IMailService
{
    private readonly IMailService _inner;
    private readonly IAsyncPolicy _retryPolicy;

    public ResilientMailService(IMailService inner)
    {
        _inner = inner;
        
        // Retry 3 times với exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
   .WaitAndRetryAsync(
           retryCount: 3,
    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
         onRetry: (exception, timeSpan, attempt, context) =>
        {
           Console.WriteLine($"Email send attempt {attempt} failed. Retrying in {timeSpan.TotalSeconds}s. Error: {exception.Message}");
                });
 }

    public async Task SendAsync(MailRequest request, CancellationToken ct)
    {
        await _retryPolicy.ExecuteAsync(() => _inner.SendAsync(request, ct));
    }
}
```

**Register:**
```csharp
services.Decorate<IMailService, ResilientMailService>();
```

---

### 10.6: Email Tracking

**Problem:** Không biết user có mở email không.

**Solution:** Add tracking pixel.

```csharp
public class EmailTemplateService : IEmailTemplateService
{
    public string GenerateEmailTemplate<T>(string templateName, T model)
    {
      var html = /* render template */;
        
        // Add tracking pixel
    var trackingId = Guid.NewGuid();
        var trackingPixel = $"<img src='https://myapp.com/api/email/track/{trackingId}' width='1' height='1' />";
        
   html = html.Replace("</body>", $"{trackingPixel}</body>");
        
        // Save tracking record
        // ...
        
        return html;
    }
}
```

**Tracking endpoint:**
```csharp
[HttpGet("track/{trackingId}")]
public async Task<IActionResult> TrackEmail(Guid trackingId)
{
    // Log email open
    await _emailTrackingService.RecordEmailOpenAsync(trackingId);
    
    // Return 1x1 transparent pixel
    return File(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B }, "image/gif");
}
```

---

## 11. Testing

### 11.1: Unit Test - EmailTemplateService

```csharp
using ECO.WebApi.Infrastructure.Mailing;
using Xunit;

namespace ECO.WebApi.Infrastructure.Tests.Mailing;

public class EmailTemplateServiceTests
{
    private readonly IEmailTemplateService _service;

    public EmailTemplateServiceTests()
    {
      _service = new EmailTemplateService();
    }

    [Fact]
    public void GenerateEmailTemplate_ShouldRenderModelProperties()
    {
        // Arrange
     var model = new { UserName = "john.doe", Email = "john@example.com", Url = "https://test.com" };

 // Act
        var html = _service.GenerateEmailTemplate("email-confirmation", model);

        // Assert
        Assert.Contains("john.doe", html);
        Assert.Contains("john@example.com", html);
        Assert.Contains("https://test.com", html);
    }

    [Fact]
  public void GenerateEmailTemplate_ShouldHandleNullProperties()
    {
        // Arrange
  var model = new { UserName = (string?)null, Email = "test@example.com", Url = "https://test.com" };

        // Act
        var html = _service.GenerateEmailTemplate("email-confirmation", model);

        // Assert
        Assert.NotNull(html);
    // Should not throw exception
    }
}
```

---

### 11.2: Integration Test - SmtpMailService

```csharp
using ECO.WebApi.Application.Common.Mailing;
using ECO.WebApi.Infrastructure.Mailing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECO.WebApi.Infrastructure.Tests.Mailing;

public class SmtpMailServiceTests
{
    [Fact]
 public async Task SendAsync_ShouldNotThrow_WhenValidRequest()
    {
        // Arrange
        var settings = Options.Create(new SMTPEmailSettings
      {
            DisplayName = "Test",
  From = "test@example.com",
   SMTPServer = "localhost",
        Port = 1025, // MailHog
            UseSsl = false,
 Username = "test",
            Password = "test"
        });

        var logger = Mock.Of<ILogger<SmtpMailService>>();
        var service = new SmtpMailService(settings, logger);

 var request = new MailRequest(
     to: new List<string> { "recipient@example.com" },
   subject: "Test Email",
            body: "<h1>Test</h1>"
        );

        // Act & Assert
        await service.SendAsync(request, CancellationToken.None);
   // Should not throw exception
    }
}
```

---

## 12. Troubleshooting

### Problem 1: Authentication Failed

**Error:** `AuthenticationException: 535-5.7.8 Username and Password not accepted`

**Solutions:**
```bash
# Gmail: Use App Password instead of regular password
# 1. Enable 2FA in Gmail
# 2. Generate App Password: https://myaccount.google.com/apppasswords
# 3. Use App Password in configuration

# Check credentials
curl -v --url 'smtps://smtp.gmail.com:465' --user 'your-email@gmail.com:your-app-password' --mail-from 'your-email@gmail.com' --mail-rcpt 'recipient@example.com' --upload-file email.txt
```

---

### Problem 2: Template Not Found

**Error:** `FileNotFoundException: Could not find file 'Email Templates/email-confirmation.cshtml'`

**Solutions:**
```xml
<!-- Check .csproj -->
<ItemGroup>
    <None Update="Email Templates\**\*.cshtml">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>

<!-- Verify file copied -->
ls bin/Debug/net8.0/Email Templates/
```

---

### Problem 3: SSL/TLS Error

**Error:** `System.Security.Authentication.AuthenticationException: The remote certificate is invalid`

**Solutions:**
```csharp
// Option 1: Use correct port và SSL settings
// Port 587 → UseSsl = true (STARTTLS)
// Port 465 → UseSsl = true (SSL)

// Option 2: Disable certificate validation (development only!)
// NOT RECOMMENDED FOR PRODUCTION
_smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;
```

---

### Problem 4: Emails Go to Spam

**Check:**
```bash
# Test email score
# Send email to check@mail-tester.com
# Visit https://www.mail-tester.com/ to see score

# Common issues:
# - Missing SPF/DKIM/DMARC records
# - Spam keywords in subject/body
# - No unsubscribe link
# - High image-to-text ratio
# - Blacklisted IP (check: https://mxtoolbox.com/blacklists.aspx)
```

---

### Problem 5: Slow Email Sending

**Solution:** Use background jobs
```csharp
// Don't block request
_backgroundJobClient.Enqueue<IMailService>(x => x.SendAsync(request, CancellationToken.None));

// Return immediately
return Ok("Email queued for sending");
```

---

## 13. Summary

### ✅ Đã hoàn thành trong bước này:

**Application Layer:**
- ✅ `MailRequest` DTO (email request model)
- ✅ `IMailService` interface
- ✅ `IEmailTemplateService` interface

**Infrastructure Layer:**
- ✅ `SMTPEmailSettings` (configuration model)
- ✅ `SmtpMailService` (MailKit implementation)
- ✅ `EmailTemplateService` (RazorEngineCore implementation)
- ✅ Startup configuration

**Email Templates:**
- ✅ `email-confirmation.cshtml` (welcome email)
- ✅ `password-reset.cshtml` (password reset)

**Configuration:**
- ✅ `appsettings.json` SMTP settings
- ✅ Email templates copy to output directory

**Features:**
- ✅ Send HTML emails via SMTP
- ✅ Render Razor templates với dynamic data
- ✅ Support attachments (PDF, images)
- ✅ CC, BCC, Reply-To, Custom headers
- ✅ Multiple SMTP providers support (Gmail, SendGrid, Mailgun...)

---

### 📊 Email Service Architecture:

```
Application Layer (Abstraction)
    │
    ├── IMailService (interface)
    │   └── SendAsync(MailRequest, CancellationToken)
    │
    └── IEmailTemplateService (interface)
        └── GenerateEmailTemplate<T>(templateName, model)

Infrastructure Layer (Implementation)
    │
    ├── SmtpMailService (MailKit)
    │   ├── Connect to SMTP server
    │   ├── Authenticate
    │   ├── Send email
    │   └── Disconnect
    │
    └── EmailTemplateService (RazorEngineCore)
        ├── Load .cshtml template
        ├── Compile Razor syntax
        ├── Render với model data
  └── Return HTML string

Configuration
    │
    └── SMTPEmailSettings
        ├── SMTPServer (e.g., smtp.gmail.com)
        ├── Port (587/465)
        ├── Username/Password
        └── UseSsl

Email Templates (Razor)
    │
    ├── email-confirmation.cshtml
    ├── password-reset.cshtml
    └── [custom templates]
```

---

### 📌 Key Concepts:

**SMTP (Simple Mail Transfer Protocol):**
- Standard protocol cho email delivery
- Ports: 25 (plain), 465 (SSL), 587 (STARTTLS)
- Authentication: Username/password hoặc OAuth2

**MailKit Benefits:**
- Cross-platform (.NET Standard)
- Modern API (async/await)
- Better than System.Net.Mail
- Support OAuth2, POP3, IMAP

**RazorEngineCore:**
- Standalone Razor engine (không cần MVC)
- Compile .cshtml to C# code
- Type-safe với strongly-typed models
- Support C# expressions, loops, conditionals

**Email Best Practices:**
- ✅ Use background jobs (async sending)
- ✅ Retry logic (network failures)
- ✅ Rate limiting (respect SMTP limits)
- ✅ Template caching (performance)
- ✅ Tracking (open rates)
- ✅ Testing (MailHog, Mailtrap)
- ✅ SPF/DKIM/DMARC (deliverability)

---

### 📁 File Structure:

```
src/Core/Application/Common/Mailing/
├── IMailService.cs
├── IEmailTemplateService.cs
└── MailRequest.cs

src/Infrastructure/Infrastructure/Mailing/
├── SmtpMailService.cs
├── EmailTemplateService.cs
├── SMTPEmailSettings.cs
└── Startup.cs

src/Host/Host/Email Templates/
├── email-confirmation.cshtml
├── password-reset.cshtml
└── [custom templates]

src/Host/Host/Configurations/
└── mail.json (or appsettings.json)
```

---

## 14. Next Steps

**Tiếp theo:** [BUILD_22 - Blob Storage](BUILD_22_Blob_Storage.md)

Trong bước tiếp theo, chúng ta sẽ:
1. ✅ Tạo `IBlobStorageService` interface
2. ✅ Implement Azure Blob Storage service
3. ✅ Container management (create, list, delete)
4. ✅ Blob upload/download/delete operations
5. ✅ Public vs Private containers
6. ✅ SAS token generation for secure access

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
