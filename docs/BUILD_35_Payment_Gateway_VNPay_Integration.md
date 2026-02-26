# BUILD_35: Payment Gateway Integration - VNPay (Vietnam Local)

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** BUILD_34 (Payment Gateway Database Design) đã complete  
> 🎯 **Approach:** Production-ready VNPay integration  
> 🇻🇳 **Focus:** Vietnam's #1 Payment Gateway (40% market share)  
> ⚠️ **Important:** Follow BUILD_34 security patterns (encryption, idempotency, webhooks)

Tài liệu này hướng dẫn **tích hợp VNPay Payment Gateway** theo research-based patterns từ BUILD_34.

---

## 1. Overview

**Làm gì:** Tích hợp VNPay Payment Gateway để xử lý thanh toán qua chuyển khoản ngân hàng và thẻ ATM/Credit.

**Tại sao cần:**
- **VNPay là #1 tại VN:** Kết nối với 50+ ngân hàng Việt Nam (40% market share)
- **Bank Transfer:** Phương thức phổ biến nhất (80% giao dịch online VN)
- **Secure:** PCI-DSS compliant, không lưu thông tin thẻ
- **Real-time:** Webhook callback tức thì khi khách hàng thanh toán
- **Vietnamese Market:** Hỗ trợ VND, giao diện tiếng Việt
- **Integration với BUILD_34:** Sử dụng PaymentProvider, PaymentTransaction, PaymentWebhook entities

**Trong bước này chúng ta sẽ:**
- ✅ Setup VNPay configuration theo BUILD_34 pattern (encrypted)
- ✅ Create PaymentProvider seed data cho VNPay
- ✅ Implement VNPayService với proper entity references
- ✅ Tạo Payment DTOs & Specifications
- ✅ Implement Webhook handler (IPN - Instant Payment Notification)
- ✅ Integrate với Order workflow (BUILD_32) using Domain Events
- ✅ Security: Hash validation, Idempotency, Configuration encryption
- ✅ Complete testing guide với VNPay Sandbox

**Payment Flow theo BUILD_34 Design:**
```
1. Customer clicks "Thanh toán"
   → CreateOrderCommand (BUILD_32)
   
2. Generate VNPay payment URL
   → Create PaymentTransaction (Status: Pending, IdempotencyKey generated)
   → VNPayService.CreatePaymentUrlAsync()
   
3. Redirect to VNPay
   → Customer pays on VNPay gateway
   
4. VNPay sends IPN (webhook)
   → Store PaymentWebhook entity
   → Verify signature (HMACSHA512)
   → Update PaymentTransaction (Status: Completed)
   → Raise OrderPaidEvent

5. OrderPaidEventHandler
   → Confirm order
   → Deduct inventory
   → Send confirmation email
```

---

## 2. VNPay Configuration

### Bước 2.1: PaymentProvider Seed Data

**Làm gì:** Seed VNPay provider vào database (theo BUILD_34 pattern).

**File:** `src/Migrators/Migrators.MSSQL/Seeding/PaymentProviderSeeder.cs`

```csharp
using ECO.WebApi.Domain.Payment;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Migrators.MSSQL.Seeding;

public class PaymentProviderSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.PaymentProviders.AnyAsync(p => p.Code == "VNPAY"))
            return; // Already seeded
        
    // VNPay Provider (will be encrypted in production via ConfigurationEncryptionService)
   var vnpayProvider = new PaymentProvider(
      code: "VNPAY",
            name: "VNPay",
configuration: @"{
      ""TmnCode"": ""YOUR_TMNCODE_HERE"",
""HashSecret"": ""YOUR_HASHSECRET_HERE"",
         ""BaseUrl"": ""https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"",
           ""Version"": ""2.1.0"",
   ""Command"": ""pay"",
    ""CurrencyCode"": ""VND"",
                ""Locale"": ""vn"",
            description: ""Vietnam's leading payment gateway - Bank transfer & ATM/Credit cards"",
            priority: 1
 );
        
        context.PaymentProviders.Add(vnpayProvider);
    await context.SaveChangesAsync();
    }
}
```

**⚠️ Production Note:**
- Configuration phải được encrypt bằng `ConfigurationEncryptionService` (BUILD_34 Part 2, Section 10.3)
- Seed data trên chỉ dùng cho development
- Production: Encrypt before save hoặc dùng Azure Key Vault

---

### Bước 2.2: VNPaySettings DTO

**Làm gì:** DTO để deserialize VNPay configuration từ PaymentProvider entity.

**File:** `src/Infrastructure/Infrastructure/Payment/VNPay/VNPaySettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Payment.VNPay;

/// <summary>
/// VNPay payment gateway settings (deserialized from PaymentProvider.Configuration)
/// </summary>
public class VNPaySettings
{
    /// <summary>
    /// Terminal Code (TmnCode) - provided by VNPay
    /// </summary>
    public string TmnCode { get; set; } = default!;
    
    /// <summary>
    /// Hash Secret Key - for HMACSHA512 signature validation
    /// </summary>
    public string HashSecret { get; set; } = default!;
    
    /// <summary>
    /// VNPay Payment Gateway URL
 /// Sandbox: https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
    /// Production: https://vnpayment.vn/paymentv2/vpcpay.html
    /// </summary>
    public string BaseUrl { get; set; } = default!;
    
    /// <summary>
    /// API Version (default: 2.1.0)
    /// </summary>
    public string Version { get; set; } = "2.1.0";
    
    /// <summary>
    /// Command (default: pay)
    /// </summary>
  public string Command { get; set; } = "pay";
    
    /// <summary>
    /// Currency Code (VND only)
    /// </summary>
    public string CurrencyCode { get; set; } = "VND";
    
    /// <summary>
    /// Locale (vn or en)
 /// </summary>
    public string Locale { get; set; } = "vn";
}
```

**Note:** ReturnUrl và IpnUrl sẽ được generate dynamically trong VNPayService (theo environment).

---

### Bước 2.3: Application URLs Configuration

**File:** `src/Host/Host/Configurations/payment.json`

```json
{
  "PaymentSettings": {
    "ReturnUrl": "https://localhost:7001/payment/vnpay/return",
    "IpnUrl": "https://your-production-domain.com/api/webhooks/vnpay"
  }
}
```

**⚠️ IPN URL Requirements:**
- Must be publicly accessible (VNPay server-to-server call)
- HTTPS required (VNPay rejects HTTP)
- For local development: Use ngrok (`ngrok http 7001`)

---

## 3. VNPay Service Implementation

### Bước 3.1: IVNPayService Interface

**File:** `src/Core/Application/Common/Interfaces/IVNPayService.cs`

```csharp
namespace ECO.WebApi.Application.Common.Interfaces;

/// <summary>
/// VNPay payment gateway service (follows BUILD_34 pattern)
/// </summary>
public interface IVNPayService
{
    /// <summary>
  /// Create VNPay payment URL for order
    /// Creates PaymentTransaction with Status: Pending
    /// </summary>
    Task<string> CreatePaymentUrlAsync(
        Guid orderId,
        string ipAddress,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process VNPay IPN (Webhook) - Server-to-server notification
    /// Updates PaymentTransaction status and raises OrderPaidEvent
    /// </summary>
    Task<VNPayIpnResponse> ProcessIpnAsync(
        Dictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// VNPay IPN response (returned to VNPay server)
/// </summary>
public record VNPayIpnResponse(
    string RspCode,  // "00" = Success, "01" = Order not found, "04" = Amount mismatch, "97" = Invalid signature
    string Message);
```

**Key Changes from original:**
- ✅ Removed `ProcessReturnCallbackAsync` (IPN is enough, return URL just redirects to frontend)
- ✅ `CreatePaymentUrlAsync` only needs orderId (gets amount from Order entity)
- ✅ Focus on IPN processing (most important for payment confirmation)

---

### Bước 3.2: VNPayService Implementation ⭐⭐⭐

**File:** `src/Infrastructure/Infrastructure/Payment/VNPay/VNPayService.cs`

```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Domain.Payment;
using ECO.WebApi.Domain.Enum;
using ECO.WebApi.Shared.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Infrastructure.Payment.VNPay;

public class VNPayService : IVNPayService
{
    private readonly IRepository<PaymentProvider> _providerRepository;
    private readonly IRepository<PaymentTransaction> _transactionRepository;
    private readonly IRepository<PaymentWebhook> _webhookRepository;
    private readonly IRepository<Domain.Orders.Order> _orderRepository;
    private readonly IConfiguration _configuration;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<VNPayService> _logger;
    
    // Cache provider to avoid repeated DB queries
private PaymentProvider? _cachedProvider;
    
    public VNPayService(
     IRepository<PaymentProvider> providerRepository,
      IRepository<PaymentTransaction> transactionRepository,
 IRepository<PaymentWebhook> webhookRepository,
        IRepository<Domain.Orders.Order> orderRepository,
      IConfiguration configuration,
     IEventPublisher eventPublisher,
    ILogger<VNPayService> logger)
    {
        _providerRepository = providerRepository;
        _transactionRepository = transactionRepository;
        _webhookRepository = webhookRepository;
        _orderRepository = orderRepository;
        _configuration = configuration;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }
    
    // ==================== CREATE PAYMENT URL ⭐ ====================
    
    public async Task<string> CreatePaymentUrlAsync(
   Guid orderId,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
  // 1. Get PaymentProvider (VNPay)
    var provider = await GetVNPayProviderAsync(cancellationToken);
     var settings = DeserializeSettings(provider.Configuration);
        
        // 2. Get Order
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
        ?? throw new NotFoundException($"Order {orderId} not found");
        
if (order.Status != OrderStatus.Pending)
    throw new InvalidOperationException($"Order {orderId} is not pending (Status: {order.Status})");
  
     // 3. Create PaymentTransaction
      var transaction = new PaymentTransaction(
            orderId: orderId,
        paymentProviderId: provider.Id,
            idempotencyKey: GenerateIdempotencyKey(orderId),
  transactionId: "", // Set after VNPay callback
          paymentMethod: PaymentMethod.BankTransfer,
            status: PaymentStatus.Pending,
     amount: order.TotalAmount,
       currency: "VND");
        
        await _transactionRepository.AddAsync(transaction, cancellationToken);
        
  // 4. Build VNPay parameters
     var vnpParams = new SortedDictionary<string, string>
        {
 { "vnp_Version", settings.Version },
      { "vnp_Command", settings.Command },
            { "vnp_TmnCode", settings.TmnCode },
       { "vnp_Amount", ((long)(order.TotalAmount * 100)).ToString() }, // VNPay: amount * 100
    { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
   { "vnp_CurrCode", settings.CurrencyCode },
            { "vnp_IpAddr", ipAddress },
       { "vnp_Locale", settings.Locale },
        { "vnp_OrderInfo", $"Thanh toan don hang {order.OrderNumber}" },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", _configuration["PaymentSettings:ReturnUrl"] ?? "" },
    { "vnp_TxnRef", transaction.IdempotencyKey },
            { "vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss") }
        };
        
      // 5. Generate secure hash
    var queryString = BuildQueryString(vnpParams);
        var secureHash = GenerateSecureHash(queryString, settings.HashSecret);
    
     // 6. Build payment URL
  var paymentUrl = $"{settings.BaseUrl}?{queryString}&vnp_SecureHash={secureHash}";
        
        _logger.LogInformation(
 "Created VNPay payment URL: OrderId={OrderId}, TransactionId={TxnRef}, Amount={Amount}",
  orderId, transaction.IdempotencyKey, order.TotalAmount);
        
        return paymentUrl;
    }

    // ==================== PROCESS IPN (Webhook) ⭐⭐⭐ ====================
    
 public async Task<VNPayIpnResponse> ProcessIpnAsync(
        Dictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
      // 1. Get provider & settings
   var provider = await GetVNPayProviderAsync(cancellationToken);
        var settings = DeserializeSettings(provider.Configuration);
        
        // 2. Create webhook record (audit trail)
        var webhook = new PaymentWebhook(
            paymentProviderId: provider.Id,
            eventType: "payment.ipn",
  payload: JsonSerializer.Serialize(queryParams),
            signature: queryParams.GetValueOrDefault("vnp_SecureHash"));
        
        await _webhookRepository.AddAsync(webhook, cancellationToken);
        
     // 3. Validate signature
        if (!ValidateSecureHash(queryParams, settings.HashSecret))
      {
            _logger.LogWarning("VNPay IPN: Invalid signature");
          webhook.MarkAsFailed("Invalid signature");
     await _webhookRepository.UpdateAsync(webhook, cancellationToken);
            
     return new VNPayIpnResponse("97", "Invalid signature");
        }
 
        webhook.MarkAsVerified();
        
     // 4. Extract parameters
        var txnRef = queryParams["vnp_TxnRef"]; // Our IdempotencyKey
        var vnpTransactionNo = queryParams["vnp_TransactionNo"];
        var responseCode = queryParams["vnp_ResponseCode"];
     var amount = long.Parse(queryParams["vnp_Amount"]) / 100m;
 
        // 5. Find transaction
        var transaction = await _transactionRepository
            .FirstOrDefaultAsync(new PaymentTransactionByIdempotencyKeySpec(txnRef), cancellationToken);
        
        if (transaction == null)
    {
            _logger.LogWarning("VNPay IPN: Transaction {TxnRef} not found", txnRef);
     webhook.MarkAsFailed("Transaction not found");
 await _webhookRepository.UpdateAsync(webhook, cancellationToken);
            
          return new VNPayIpnResponse("01", "Order not found");
   }
        
        // 6. Validate amount
        if (transaction.Amount != amount)
        {
            _logger.LogWarning("VNPay IPN: Amount mismatch. Expected={Expected}, Actual={Actual}",
        transaction.Amount, amount);
        webhook.MarkAsFailed("Amount mismatch");
            await _webhookRepository.UpdateAsync(webhook, cancellationToken);
            
 return new VNPayIpnResponse("04", "Amount mismatch");
        }
        
      // 7. Idempotency check
        if (transaction.Status == PaymentStatus.Completed)
 {
       _logger.LogInformation("VNPay IPN: Transaction {TxnRef} already processed", txnRef);
  webhook.MarkAsProcessed(transaction.Id);
await _webhookRepository.UpdateAsync(webhook, cancellationToken);
       
    return new VNPayIpnResponse("00", "Confirm Success");
        }
        
 // 8. Process payment result
        if (responseCode == "00") // Success
  {
            transaction.SetTransactionId(vnpTransactionNo);
  transaction.SetCompleted();
         await _transactionRepository.UpdateAsync(transaction, cancellationToken);
     
            webhook.MarkAsProcessed(transaction.Id);
       await _webhookRepository.UpdateAsync(webhook, cancellationToken);
  
            // 9. Raise domain event for order confirmation
      await _eventPublisher.PublishAsync(
  new OrderPaidEvent(transaction.OrderId, transaction.Id),
    cancellationToken);
 
    _logger.LogInformation(
         "VNPay payment SUCCESS: OrderId={OrderId}, TransactionId={VnpTxnNo}",
   transaction.OrderId, vnpTransactionNo);
            
            return new VNPayIpnResponse("00", "Confirm Success");
        }
        else // Failed
        {
            transaction.SetFailed();
            await _transactionRepository.UpdateAsync(transaction, cancellationToken);
            
            webhook.MarkAsProcessed(transaction.Id);
            await _webhookRepository.UpdateAsync(webhook, cancellationToken);
  
       _logger.LogWarning(
 "VNPay payment FAILED: OrderId={OrderId}, ResponseCode={Code}",
      transaction.OrderId, responseCode);
            
            return new VNPayIpnResponse("00", "Confirm Success"); // Still return success to VNPay
        }
    }
    
    // ==================== HELPER METHODS ====================
    
    private async Task<PaymentProvider> GetVNPayProviderAsync(CancellationToken ct)
    {
        if (_cachedProvider != null)
            return _cachedProvider;
        
     _cachedProvider = await _providerRepository
   .FirstOrDefaultAsync(new PaymentProviderByCodeSpec("VNPAY"), ct)
            ?? throw new NotFoundException("VNPay provider not configured");
        
        return _cachedProvider;
    }
  
    private VNPaySettings DeserializeSettings(string configJson)
    {
        // In production: Decrypt first using ConfigurationEncryptionService (BUILD_34 Part 2)
        return JsonSerializer.Deserialize<VNPaySettings>(configJson)
       ?? throw new InvalidOperationException("Invalid VNPay configuration");
    }
    
    private string GenerateIdempotencyKey(Guid orderId)
    {
        var timestamp = DateTime.UtcNow.Ticks;
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{orderId}:{timestamp}:{random}";
    }
    
    private string BuildQueryString(SortedDictionary<string, string> parameters)
    {
        return string.Join("&", 
    parameters.Select(kvp => $"{kvp.Key}={WebUtility.UrlEncode(kvp.Value)}"));
    }
    
    private string GenerateSecureHash(string data, string hashSecret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(hashSecret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
  
  using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
        return BitConverter.ToString(hashBytes)
            .Replace("-", "")
            .ToLower();
    }
    
    private bool ValidateSecureHash(Dictionary<string, string> queryParams, string hashSecret)
    {
     if (!queryParams.TryGetValue("vnp_SecureHash", out var receivedHash))
            return false;
        
        var paramsToValidate = new SortedDictionary<string, string>(queryParams);
        paramsToValidate.Remove("vnp_SecureHash");
        paramsToValidate.Remove("vnp_SecureHashType");
   
        var queryString = BuildQueryString(paramsToValidate);
   var expectedHash = GenerateSecureHash(queryString, hashSecret);

        return string.Equals(receivedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
```

**⭐ Key Improvements:**
- ✅ Uses PaymentProvider entity (not hardcoded settings)
- ✅ Creates PaymentTransaction with proper status flow
- ✅ Creates PaymentWebhook for audit trail
- ✅ Raises OrderPaidEvent (integration with BUILD_32)
- ✅ Proper idempotency check
- ✅ Amount validation
- ✅ Signature validation
- ✅ Caches provider for performance

---

## 4. Specifications

### Bước 4.1: PaymentTransactionByIdempotencyKeySpec

**File:** `src/Core/Application/Payment/Specifications/PaymentTransactionByIdempotencyKeySpec.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Domain.Payment;

namespace ECO.WebApi.Application.Payment.Specifications;

public class PaymentTransactionByIdempotencyKeySpec : Specification<PaymentTransaction>
{
    public PaymentTransactionByIdempotencyKeySpec(string idempotencyKey)
    {
     Query.Where(t => t.IdempotencyKey == idempotencyKey);
    }
}
```

---

### Bước 4.2: PaymentProviderByCodeSpec

**File:** `src/Core/Application/Payment/Specifications/PaymentProviderByCodeSpec.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Domain.Payment;

namespace ECO.WebApi.Application.Payment.Specifications;

public class PaymentProviderByCodeSpec : Specification<PaymentProvider>
{
    public PaymentProviderByCodeSpec(string code)
    {
     Query
            .Where(p => p.Code == code && p.IsActive)
            .OrderBy(p => p.Priority);
  }
}
```

---

## 5. Order Paid Event Handler

**File:** `src/Core/Application/Orders/EventHandlers/OrderPaidEventHandler.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Shared.Events;
using MediatR;

namespace ECO.WebApi.Application.Orders.EventHandlers;

public class OrderPaidEventHandler : INotificationHandler<OrderPaidEvent>
{
    private readonly IRepository<Domain.Orders.Order> _orderRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<OrderPaidEventHandler> _logger;
    
    public async Task Handle(OrderPaidEvent notification, CancellationToken ct)
    {
      // 1. Get order
    var order = await _orderRepository.GetByIdAsync(notification.OrderId, ct);
      if (order == null) return;
        
      // 2. Confirm order (deducts inventory, changes status to Confirmed)
 order.Confirm();
        await _orderRepository.UpdateAsync(order, ct);
        
        // 3. Send confirmation email
        await _mailService.SendOrderConfirmationEmailAsync(order, ct);
     
        _logger.LogInformation(
            "Order {OrderId} confirmed after payment {TransactionId}",
     notification.OrderId, notification.TransactionId);
    }
}
```

---

**Tiếp tục với Controllers, Testing Guide, và Summary...**

---

## 6. Payment Controllers

### Bước 6.1: WebhooksController (VNPay IPN)

**File:** `src/Host/Host/Controllers/WebhooksController.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IVNPayService _vnpayService;
    private readonly ILogger<WebhooksController> _logger;
    
    public WebhooksController(
      IVNPayService vnpayService,
     ILogger<WebhooksController> logger)
    {
     _vnpayService = vnpayService;
        _logger = logger;
    }
    
    /// <summary>
  /// VNPay IPN (Instant Payment Notification) - Webhook endpoint
    /// ⭐ MOST IMPORTANT ENDPOINT - Called by VNPay server-to-server
    /// </summary>
    [HttpGet("vnpay")]
    [AllowAnonymous] // VNPay server calls this, no authentication
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VNPayIpn(CancellationToken cancellationToken)
    {
        // Extract query parameters from VNPay
      var queryParams = Request.Query
.ToDictionary(k => k.Key, v => v.Value.ToString());
        
 _logger.LogInformation("VNPay IPN received: {Params}",
            System.Text.Json.JsonSerializer.Serialize(queryParams));
        
 // Process IPN
  var response = await _vnpayService.ProcessIpnAsync(queryParams, cancellationToken);
        
     // Return response to VNPay (JSON format)
    return Ok(new
      {
       RspCode = response.RspCode,
    Message = response.Message
   });
    }
}
```

---

### Bước 6.2: PaymentController (Create Payment)

**File:** `src/Host/Host/Controllers/PaymentController.cs`

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Infrastructure.Auth.Permissions;
using ECO.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly IVNPayService _vnpayService;
    private readonly ILogger<PaymentController> _logger;
    
  public PaymentController(
        IVNPayService vnpayService,
        ILogger<PaymentController> logger)
    {
        _vnpayService = vnpayService;
   _logger = logger;
    }
    
 /// <summary>
    /// Create VNPay payment URL for order
 /// </summary>
    [HttpPost("vnpay/create")]
    [MustHavePermission(ECOAction.Create, ECOFunction.Payments)]
    [ProducesResponseType(typeof(CreatePaymentResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatePaymentResponse>> CreateVNPayPayment(
        [FromBody] CreatePaymentRequest request,
      CancellationToken cancellationToken)
    {
  // Get client IP
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        
  // Generate payment URL
     var paymentUrl = await _vnpayService.CreatePaymentUrlAsync(
request.OrderId,
    ipAddress,
       cancellationToken);
  
    _logger.LogInformation("Created VNPay payment URL for Order {OrderId}", request.OrderId);
  
        return Ok(new CreatePaymentResponse
        {
            OrderId = request.OrderId,
     PaymentUrl = paymentUrl,
   ExpiresIn = 900 // 15 minutes
      });
    }
}

public record CreatePaymentRequest(Guid OrderId);

public record CreatePaymentResponse
{
    public Guid OrderId { get; init; }
    public string PaymentUrl { get; init; } = default!;
    public int ExpiresIn { get; init; }
}
```

---

## 7. Testing Guide

### Bước 7.1: VNPay Sandbox Setup

**1. Đăng ký VNPay Sandbox:**
- URL: https://sandbox.vnpayment.vn/merchant_webapi/merchant.html
- Email: your-email@company.com
- Sau khi approve, nhận được:
  - `TmnCode`
  - `HashSecret`

**2. Configure ngrok cho IPN (local testing):**
```bash
# Install ngrok
winget install ngrok

# Run ngrok
ngrok http 7001

# Copy HTTPS URL (e.g., https://abc123.ngrok.io)
# Update PaymentSettings:IpnUrl in appsettings.json
```

**3. Seed VNPay Provider:**
```bash
# Run migration
dotnet ef database update

# Seed data
# PaymentProviderSeeder will auto-run on startup
```

---

### Bước 7.2: Test Payment Flow

**Step 1: Create Order**
```http
POST https://localhost:7001/api/orders
Content-Type: application/json

{
  "customerId": "user-guid",
  "items": [
    { "productId": "product-guid", "quantity": 2 }
  ]
}

Response:
{
  "orderId": "order-guid-123",
  "orderNumber": "ORD-20250201-001",
  "totalAmount": 500000
}
```

**Step 2: Create VNPay Payment**
```http
POST https://localhost:7001/api/payment/vnpay/create
Content-Type: application/json

{
  "orderId": "order-guid-123"
}

Response:
{
  "orderId": "order-guid-123",
  "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=50000000&...",
  "expiresIn": 900
}
```

**Step 3: Redirect to paymentUrl**
- Customer pays on VNPay sandbox
- Use test cards:
  ```
  Card Number: 9704198526191432198
  Cardholder: NGUYEN VAN A
  Issue Date: 07/15
  OTP: 123456
  ```

**Step 4: VNPay sends IPN**
```http
GET https://abc123.ngrok.io/api/webhooks/vnpay?vnp_Amount=50000000&vnp_BankCode=NCB&vnp_ResponseCode=00&...

Response:
{
  "RspCode": "00",
  "Message": "Confirm Success"
}
```

**Step 5: Verify Order Status**
```http
GET https://localhost:7001/api/orders/order-guid-123

Response:
{
  "id": "order-guid-123",
  "status": "Confirmed",  // Changed from Pending
  "paymentStatus": "Paid"
}
```

---

### Bước 7.3: Test Scenarios

**Scenario 1: Successful Payment**
- ✅ PaymentTransaction created (Status: Pending)
- ✅ Customer redirected to VNPay
- ✅ IPN received (vnp_ResponseCode = "00")
- ✅ PaymentWebhook created & verified
- ✅ PaymentTransaction updated (Status: Completed)
- ✅ OrderPaidEvent raised
- ✅ Order confirmed
- ✅ Confirmation email sent

**Scenario 2: Failed Payment**
- Customer cancels payment on VNPay
- IPN received (vnp_ResponseCode = "24")
- PaymentTransaction updated (Status: Failed)
- Order remains Pending

**Scenario 3: Duplicate IPN**
- VNPay sends same IPN twice
- First IPN: Process successfully
- Second IPN: Idempotency check → Return "00" without reprocessing

**Scenario 4: Invalid Signature**
- Tampered IPN data
- Signature validation fails
- PaymentWebhook marked as failed
- Return RspCode "97"

**Scenario 5: Amount Mismatch**
- IPN amount ≠ order amount
- Return RspCode "04"
- PaymentTransaction not updated

---

## 8. Security Checklist

### ✅ Security Measures Implemented:

| Security Measure | Status | Implementation |
|------------------|--------|----------------|
| **Signature Verification** | ✅ | HMACSHA512 validation on every IPN |
| **Idempotency** | ✅ | Check transaction status before processing |
| **Amount Validation** | ✅ | Verify IPN amount matches order amount |
| **Configuration Encryption** | ⚠️ | TODO: Implement ConfigurationEncryptionService |
| **HTTPS Only** | ✅ | IPN URL must be HTTPS |
| **Webhook Audit Trail** | ✅ | All IPNs stored in PaymentWebhook table |
| **Rate Limiting** | ⚠️ | TODO: Add rate limiting to IPN endpoint |
| **IP Whitelist** | ⚠️ | TODO: Whitelist VNPay IPs |

---

### ⚠️ Production TODO:

1. **Encrypt PaymentProvider Configuration:**
```csharp
// In PaymentProviderSeeder
var encryptionService = serviceProvider.GetRequiredService<IConfigurationEncryptionService>();
var encryptedConfig = encryptionService.Encrypt(vnpayConfigJson);
provider.UpdateConfiguration(encryptedConfig);
```

2. **Add Rate Limiting:**
```csharp
// In WebhooksController
[RateLimit(PermitLimit = 100, Window = 60)] // 100 req/min
public async Task<IActionResult> VNPayIpn(...)
```

3. **IP Whitelist (VNPay IPs):**
```csharp
// In Startup.cs
services.Configure<IpRateLimitOptions>(options =>
{
 options.IpWhitelist = new List<string>
    {
        "203.171.20.0/24", // VNPay IP range (check with VNPay)
    };
});
```

4. **Monitor Stale Webhooks:**
```csharp
// Background job
var staleWebhooks = await _webhookRepository
    .Where(w => !w.IsProcessed && w.ReceivedAt < DateTime.UtcNow.AddHours(-1))
    .ToListAsync();
    
// Alert admin
```

---

## 11. Next Steps & Production Considerations

### ✅ Core Implementation Complete:

**What you have now (BUILD_35):**
- [x] VNPay payment URL generation
- [x] IPN webhook processing
- [x] Signature verification (HMACSHA512)
- [x] Idempotency check
- [x] Amount validation
- [x] PaymentWebhook audit trail
- [x] OrderPaidEvent (domain event)
- [x] Complete testing guide

**This is ENOUGH for:**
- ✅ MVP/Prototype
- ✅ Small to medium e-commerce (< 500 orders/day)
- ✅ Development & Testing
- ✅ Production with proper infrastructure

---

### 🔐 Optional: Production Hardening (When Needed)

**1. Configuration Encryption:**
- **Instead of custom code:** Use **Azure Key Vault** or **AWS Secrets Manager**
- Store `TmnCode` and `HashSecret` in Key Vault
- Reference in code:
  ```csharp
  var tmnCode = await _keyVaultClient.GetSecretAsync("VNPay-TmnCode");
  var hashSecret = await _keyVaultClient.GetSecretAsync("VNPay-HashSecret");
  ```
- **When to use:** Production environment with team access to database

**2. Rate Limiting:**
- **Instead of AspNetCoreRateLimit:** Use **Cloudflare** or **Azure API Management**
- Cloudflare Free Plan: 100,000 requests/day rate limiting
- **When to use:** Public-facing API with high traffic (> 1000 req/day)

**3. Monitoring:**
- **Instead of custom code:** Use **Application Insights** or **Datadog**
- Auto-detect stalled payments, failed webhooks
- **When to use:** Production environment requiring alerts

---

### 📋 Production Deployment Checklist (Simplified):

**Pre-Deployment:**
1. ✅ Get VNPay production credentials (TmnCode, HashSecret)
2. ✅ Update IPN URL to production domain (HTTPS)
3. ✅ Test webhook endpoint is publicly accessible
4. ✅ (Optional) Store credentials in Azure Key Vault
5. ✅ (Optional) Enable Cloudflare for rate limiting

**Deployment Steps:**
```bash
# 1. Update PaymentProvider configuration
UPDATE payment.PaymentProviders
SET Configuration = '{
  "TmnCode": "PRODUCTION_TMNCODE",
  "HashSecret": "PRODUCTION_HASHSECRET",
  "BaseUrl": "https://vnpayment.vn/paymentv2/vpcpay.html"
}'
WHERE Code = 'VNPAY';

# 2. Update appsettings.Production.json
{
  "PaymentSettings": {
    "IpnUrl": "https://your-production-domain.com/api/webhooks/vnpay"
  }
}

# 3. Test webhook endpoint
curl https://your-production-domain.com/api/webhooks/vnpay

# 4. Register IPN URL with VNPay
# → VNPay merchant portal: Update IPN URL

# 5. Test with small transaction (₫10,000)
```

---

### 🔍 Monitoring (Manual Queries):

**Check stalled payments:**
```sql
SELECT Id, OrderId, Amount, Status, CreatedAt
FROM payment.PaymentTransactions
WHERE Status = 1 -- Pending
AND CreatedAt < DATEADD(HOUR, -1, GETUTCDATE());
```

**Check unprocessed webhooks:**
```sql
SELECT Id, ReceivedAt, ProcessingError
FROM payment.PaymentWebhooks
WHERE IsProcessed = 0
AND ReceivedAt < DATEADD(HOUR, -1, GETUTCDATE());
```

**Payment success rate (last 24h):**
```sql
SELECT 
    COUNT(*) AS Total,
    SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END) AS Failed
FROM payment.PaymentTransactions
WHERE CreatedAt >= DATEADD(HOUR, -24, GETUTCDATE());
```

---

### 🎯 When to Add Advanced Features:

| Feature | Add When | Use Instead |
|---------|----------|-------------|
| **Encryption Service** | Team size > 5, shared DB access | Azure Key Vault, AWS Secrets Manager |
| **Rate Limiting** | API traffic > 1000 req/day | Cloudflare, Azure API Management |
| **Monitoring Dashboard** | Orders > 100/day | Application Insights, Datadog, New Relic |
| **Webhook Retry** | Payment critical business | Azure Service Bus, RabbitMQ |

---

### 📚 External Resources:

**VNPay Documentation:**
- [VNPay Sandbox Portal](https://sandbox.vnpayment.vn/)
- [VNPay API Documentation](https://sandbox.vnpayment.vn/apis/)

**Azure Services:**
- [Azure Key Vault (Credentials)](https://learn.microsoft.com/azure/key-vault/)
- [Application Insights (Monitoring)](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)

**Cloudflare:**
- [Cloudflare Rate Limiting](https://developers.cloudflare.com/waf/rate-limiting-rules/)

---

### ✅ Summary:

**BUILD_35 provides:**
1. ✅ Complete VNPay integration (payment + webhook)
2. ✅ Security (signature verification, idempotency, amount validation)
3. ✅ Testing guide (sandbox setup, 5 test scenarios)
4. ✅ Production-ready for small-medium scale

**For large-scale production:**
- Use external services (Azure Key Vault, Cloudflare, Application Insights)
- No need to implement custom encryption/rate-limiting/monitoring code

---

## 12. Phase 2 Preview: Additional Payment Gateways

### Coming Next:
- **BUILD_36:** Momo Integration (E-wallet)
- **BUILD_37:** ZaloPay Integration (E-wallet + Zalo ecosystem)
- **BUILD_38:** VietQR Integration (Universal QR payment)

### Refund Support:
- **BUILD_39:** Refund Implementation (uses BUILD_34 Refund entity)

---
