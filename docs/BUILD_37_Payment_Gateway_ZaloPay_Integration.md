# BUILD_37: Payment Gateway Integration - ZaloPay (Zalo Ecosystem)

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** BUILD_34 (Payment Gateway Database Design), BUILD_35 (VNPay), BUILD_36 (Momo) đã complete  
> 🎯 **Approach:** Production-ready ZaloPay e-wallet integration  
> 🇻🇳 **Focus:** Vietnam's #3 E-Wallet - Zalo Ecosystem (15% market share)  
> 💬 **Unique Advantage:** Tích hợp với Zalo (100M+ users) - Chat, Mini App, Official Account  
> ⚠️ **Important:** Follow BUILD_34 security patterns (encryption, idempotency, webhooks)

Tài liệu này hướng dẫn **tích hợp ZaloPay Payment Gateway** theo research-based patterns từ BUILD_34.

---

## 1. Overview

**Làm gì:** Tích hợp ZaloPay E-Wallet để xử lý thanh toán qua ví điện tử ZaloPay với tích hợp sâu vào Zalo ecosystem.

**Tại sao cần:**
- **Zalo Ecosystem:** 100M+ Zalo users, 20M+ ZaloPay users (15% market share)
- **Unique Integration:** Zalo Mini App, Zalo Official Account, Zalo Chat payment links
- **Youth Demographic:** 70% users aged 18-35 (social commerce)
- **High Engagement:** Payment + Social + Messaging in one ecosystem
- **Quick Adoption:** Install ZaloPay in 3 taps from Zalo chat
- **Integration với BUILD_34:** Sử dụng PaymentProvider, PaymentTransaction, PaymentWebhook entities

**Trong bước này chúng ta sẽ:**
- ✅ Setup ZaloPay configuration theo BUILD_34 pattern (encrypted)
- ✅ Create PaymentProvider seed data cho ZaloPay
- ✅ Implement ZaloPayService với proper entity references
- ✅ Support 3 payment methods: ZaloPay App, Zalo Mini App, Web Redirect
- ✅ Tạo Payment DTOs & Specifications
- ✅ Implement Callback handler (ZaloPay uses Callback, not IPN)
- ✅ Integrate với Order workflow (BUILD_32) using Domain Events
- ✅ Security: HMACSHA256 validation, Idempotency, Configuration encryption
- ✅ Complete testing guide với ZaloPay Sandbox

**Payment Flow theo BUILD_34 Design:**
```
1. Customer clicks "Thanh toán ZaloPay"
   → CreateOrderCommand (BUILD_32)
   
2. Generate ZaloPay payment order
   → Create PaymentTransaction (Status: Pending, IdempotencyKey generated)
   → ZaloPayService.CreateOrderAsync()
   → Sign request with HMACSHA256 (MAC algorithm)
   
3. Redirect to ZaloPay
   → Option A: ZaloPay App (deeplink - best UX)
   → Option B: Zalo Mini App (in-app payment)
   → Option C: Web Redirect (ZaloPay web payment page)
   
4. Customer pays in ZaloPay/Zalo
   
5. ZaloPay sends Callback (POST request)
   → Store PaymentWebhook entity
   → Verify MAC (HMACSHA256)
   → Update PaymentTransaction (Status: Completed)
   → Raise OrderPaidEvent

6. OrderPaidEventHandler
   → Confirm order
   → Deduct inventory
   → Send confirmation email
```

**ZaloPay Unique Features:**

| Feature | Description | Benefit |
|---------|-------------|---------|
| **Zalo Mini App** | In-app payment without leaving Zalo | Seamless UX, high conversion |
| **Zalo Chat Payment** | Send payment link in Zalo chat | Social commerce, peer-to-peer |
| **Official Account Integration** | Payment in Zalo OA | Customer engagement |
| **Zalo Ecosystem** | Connect with 100M+ Zalo users | Massive reach |

**ZaloPay vs Momo vs VNPay:**

| Feature | ZaloPay | Momo | VNPay |
|---------|---------|------|-------|
| **Primary Use** | Social commerce, Zalo ecosystem | Mobile payments, general | Bank cards, transfers |
| **User Base** | 20M users (Zalo-first) | 30M users (mobile-first) | 50M transactions/month |
| **Unique Strength** | Zalo integration (chat, mini app) | Highest market share | Bank integration |
| **Best For** | Social sellers, chat commerce | Mobile e-commerce | Traditional e-commerce |
| **Age Group** | 18-35 (70% users) | 18-45 | All demographics |
| **Integration Complexity** | Medium (MAC validation) | Medium (signature) | Medium (hash) |

---

## 2. ZaloPay Configuration

### Bước 2.1: PaymentProvider Seed Data

**Làm gì:** Seed ZaloPay provider vào database (theo BUILD_34 pattern).

**File:** `src/Migrators/Migrators.MSSQL/Seeding/PaymentProviderSeeder.cs` (Update)

```csharp
using ECO.WebApi.Domain.Payment;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Migrators.MSSQL.Seeding;

public class PaymentProviderSeeder
{
    public static async Task</ ZaloPay seeding (from BUILD_35, BUILD_36)
        // ...existing VNPay, Momo code...
        
        // ==================== ZALOPAY SEEDING ====================
        
        if (await context.PaymentProviders.AnyAsync(p => p.Code == "ZALOPAY"))
            return; // Already seeded
        
        // ZaloPay Provider (will be encrypted in production via ConfigurationEncryptionService)
      var zalopayProvider = new PaymentProvider(
            code: "ZALOPAY",
            name: "ZaloPay",
        configuration: @"{
      ""AppId"": ""YOUR_APP_ID"",
    ""Key1"": ""YOUR_KEY1"",
   ""Key2"": ""YOUR_KEY2"",
       ""Endpoint"": ""https://sb-openapi.zalopay.vn/v2/create"",
    ""CallbackUrl"": ""https://your-domain.com/api/webhooks/zalopay"",
    ""RedirectUrl"": ""https://your-domain.com/payment/zalopay/return""
     }",
      description: "Zalo ecosystem e-wallet - 20M+ users, Zalo Mini App support",
   priority: 3
        );
        
        context.PaymentProviders.Add(zalopayProvider);
        await context.SaveChangesAsync();
    }
}
```

**⚠️ ZaloPay Credentials:**
- Đăng ký tại: https://docs.zalopay.vn/
- Sandbox credentials: Liên hệ ZaloPay support để được cấp
- **Production Note:** Configuration phải được encrypt bằng `ConfigurationEncryptionService`

---

### Bước 2.2: ZaloPaySettings DTO

**Làm gì:** DTO để deserialize ZaloPay configuration từ PaymentProvider entity.

**File:** `src/Infrastructure/Infrastructure/Payment/ZaloPay/ZaloPaySettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Payment.ZaloPay;

/// <summary>
/// ZaloPay payment gateway settings (deserialized from PaymentProvider.Configuration)
/// </summary>
public class ZaloPaySettings
{
    /// <summary>
    /// App ID - provided by ZaloPay (e.g., "2553")
    /// </summary>
    public string AppId { get; set; } = default!;
    
    /// <summary>
    /// Key1 - for MAC (Message Authentication Code) generation
    /// </summary>
    public string Key1 { get; set; } = default!;
    
    /// <summary>
    /// Key2 - for callback MAC verification
    /// </summary>
    public string Key2 { get; set; } = default!;
    
  /// <summary>
    /// ZaloPay Create Order Endpoint
    /// Sandbox: https://sb-openapi.zalopay.vn/v2/create
    /// Production: https://openapi.zalopay.vn/v2/create
    /// </summary>
    public string Endpoint { get; set; } = default!;
    
    /// <summary>
    /// Callback URL - ZaloPay server-to-server notification
    /// MUST be publicly accessible HTTPS
    /// </summary>
    public string CallbackUrl { get; set; } = default!;
    
    /// <summary>
    /// Redirect URL - Where customer returns after payment
    /// </summary>
    public string RedirectUrl { get; set; } = default!;
}
```

---

## 3. ZaloPay DTOs

### Bước 3.1: ZaloPay Request DTOs

**File:** `src/Infrastructure/Infrastructure/Payment/ZaloPay/Dtos/ZaloPayCreateOrderRequest.cs`

```csharp
using System.Text.Json.Serialization;

namespace ECO.WebApi.Infrastructure.Payment.ZaloPay.Dtos;

/// <summary>
/// ZaloPay create order request (sent to ZaloPay API)
/// </summary>
public class ZaloPayCreateOrderRequest
{
    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = default!;
    
    [JsonPropertyName("app_trans_id")]
 public string AppTransId { get; set; } = default!; // Format: yyMMdd_xxxxxx (our IdempotencyKey)
    
    [JsonPropertyName("app_user")]
  public string AppUser { get; set; } = "user123"; // Customer ID
    
    [JsonPropertyName("app_time")]
    public long AppTime { get; set; } // Unix timestamp (milliseconds)
    
  [JsonPropertyName("amount")]
    public long Amount { get; set; } // Amount in VND (no decimals)
    
    [JsonPropertyName("item")]
    public string Item { get; set; } = "[]"; // JSON array of items
    
    [JsonPropertyName("embed_data")]
    public string EmbedData { get; set; } = "{}"; // JSON object (optional metadata)
    
    [JsonPropertyName("bank_code")]
    public string BankCode { get; set; } = ""; // Empty for ZaloPay wallet
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = default!;
    
    [JsonPropertyName("mac")]
    public string Mac { get; set; } = default!; // HMACSHA256 signature
}
```

---

### Bước 3.2: ZaloPay Response DTOs

**File:** `src/Infrastructure/Infrastructure/Payment/ZaloPay/Dtos/ZaloPayCreateOrderResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace ECO.WebApi.Infrastructure.Payment.ZaloPay.Dtos;

/// <summary>
/// ZaloPay create order response (from ZaloPay API)
/// </summary>
public class ZaloPayCreateOrderResponse
{
    [JsonPropertyName("return_code")]
    public int ReturnCode { get; set; } // 1 = Success
    
    [JsonPropertyName("return_message")]
    public string ReturnMessage { get; set; } = default!;
  
    [JsonPropertyName("sub_return_code")]
    public int SubReturnCode { get; set; }

    [JsonPropertyName("sub_return_message")]
    public string SubReturnMessage { get; set; } = default!;
    
    [JsonPropertyName("zp_trans_token")]
    public string ZpTransToken { get; set; } = default!; // Payment token
    
    [JsonPropertyName("order_url")]
    public string OrderUrl { get; set; } = default!; // Web payment URL
    
    [JsonPropertyName("order_token")]
    public string OrderToken { get; set; } = default!; // Token for query status
}
```

---

### Bước 3.3: ZaloPay Callback DTO

**File:** `src/Infrastructure/Infrastructure/Payment/ZaloPay/Dtos/ZaloPayCallbackRequest.cs`

```csharp
using System.Text.Json.Serialization;

namespace ECO.WebApi.Infrastructure.Payment.ZaloPay.Dtos;

/// <summary>
/// ZaloPay callback request (sent by ZaloPay to our webhook endpoint)
/// NOTE: ZaloPay sends as application/x-www-form-urlencoded, NOT JSON
/// </summary>
public class ZaloPayCallbackRequest
{
 [JsonPropertyName("app_id")]
    public string AppId { get; set; } = default!;
    
    [JsonPropertyName("app_trans_id")]
    public string AppTransId { get; set; } = default!; // Our IdempotencyKey
    
    [JsonPropertyName("app_user")]
    public string AppUser { get; set; } = default!;
    
    [JsonPropertyName("app_time")]
    public long AppTime { get; set; }
    
 [JsonPropertyName("amount")]
 public long Amount { get; set; }
    
    [JsonPropertyName("embed_data")]
    public string EmbedData { get; set; } = "{}";
    
    [JsonPropertyName("item")]
    public string Item { get; set; } = "[]";
    
    [JsonPropertyName("zp_trans_id")]
    public string ZpTransId { get; set; } = default!; // ZaloPay transaction ID
    
    [JsonPropertyName("server_time")]
    public long ServerTime { get; set; }
    
    [JsonPropertyName("channel")]
    public int Channel { get; set; } // 1=Web, 2=App, 3=MiniApp
    
    [JsonPropertyName("merchant_user_id")]
    public string MerchantUserId { get; set; } = default!;
 
    [JsonPropertyName("user_fee_amount")]
    public long UserFeeAmount { get; set; }
 
    [JsonPropertyName("discount_amount")]
    public long DiscountAmount { get; set; }
    
    [JsonPropertyName("mac")]
    public string Mac { get; set; } = default!; // HMACSHA256 signature with Key2
}
```

---

## 4. ZaloPay Service Implementation

### Bước 4.1: IZaloPayService Interface

**File:** `src/Core/Application/Common/Interfaces/IZaloPayService.cs`

```csharp
using ECO.WebApi.Infrastructure.Payment.ZaloPay.Dtos;

namespace ECO.WebApi.Application.Common.Interfaces;

/// <summary>
/// ZaloPay e-wallet payment service (follows BUILD_34 pattern)
/// </summary>
public interface IZaloPayService : ITransientService
{
    /// <summary>
    /// Create ZaloPay payment order and return payment URL
    /// Creates PaymentTransaction with Status: Pending
    /// </summary>
  Task<ZaloPayOrderInfo> CreateOrderAsync(
        Guid orderId,
        string customerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process ZaloPay Callback - Server-to-server notification
    /// Updates PaymentTransaction status and raises OrderPaidEvent
    /// </summary>
    Task<ZaloPayCallbackResponse> ProcessCallbackAsync(
        ZaloPayCallbackRequest callbackRequest,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// ZaloPay payment order information (returned to client)
/// </summary>
public record ZaloPayOrderInfo(
    string OrderUrl,        // Web payment URL
    string ZpTransToken,    // Payment token (for ZaloPay app deeplink)
    string AppTransId);     // Our IdempotencyKey

/// <summary>
/// ZaloPay callback response (returned to ZaloPay server)
/// </summary>
public record ZaloPayCallbackResponse(
    int ReturnCode,         // 1 = Success
    string ReturnMessage);
```

---

### Bước 4.2: ZaloPayService Implementation ⭐⭐⭐

**File:** `src/Infrastructure/Infrastructure/Payment/ZaloPay/ZaloPayService.cs`

```csharp
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Domain.Payment;
using ECO.WebApi.Domain.Enum;
using ECO.WebApi.Infrastructure.Payment.ZaloPay.Dtos;
using ECO.WebApi.Shared.Events;
using Microsoft.Extensions.Logging;

namespace ECO.WebApi.Infrastructure.Payment.ZaloPay;

public class ZaloPayService : IZaloPayService
{
    private readonly IRepository<PaymentProvider> _providerRepository;
    private readonly IRepository<PaymentTransaction> _transactionRepository;
    private readonly IRepository<PaymentWebhook> _webhookRepository;
    private readonly IRepository<Domain.Orders.Order> _orderRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ZaloPayService> _logger;
    
    // Cache provider to avoid repeated DB queries
    private PaymentProvider? _cachedProvider;
    
    public ZaloPayService(
      IRepository<PaymentProvider> providerRepository,
    IRepository<PaymentTransaction> transactionRepository,
        IRepository<PaymentWebhook> webhookRepository,
        IRepository<Domain.Orders.Order> orderRepository,
    IHttpClientFactory httpClientFactory,
        IEventPublisher eventPublisher,
        ILogger<ZaloPayService> logger)
  {
        _providerRepository = providerRepository;
        _transactionRepository = transactionRepository;
        _webhookRepository = webhookRepository;
    _orderRepository = orderRepository;
        _httpClientFactory = httpClientFactory;
      _eventPublisher = eventPublisher;
_logger = logger;
    }
 
 // ==================== CREATE ORDER ⭐ ====================
    
    public async Task<ZaloPayOrderInfo> CreateOrderAsync(
        Guid orderId,
        string customerId,
        CancellationToken cancellationToken = default)
    {
      // 1. Get PaymentProvider (ZaloPay)
        var provider = await GetZaloPayProviderAsync(cancellationToken);
  var settings = DeserializeSettings(provider.Configuration);
        
   // 2. Get Order
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
?? throw new NotFoundException($"Order {orderId} not found");
 
        if (order.Status != OrderStatus.Pending)
   throw new InvalidOperationException($"Order {orderId} is not pending (Status: {order.Status})");
 
        // 3. Generate app_trans_id (ZaloPay format: yyMMdd_xxxxxx)
     var appTransId = GenerateAppTransId();
        
        // 4. Create PaymentTransaction
        var transaction = new PaymentTransaction(
            orderId: orderId,
        paymentProviderId: provider.Id,
            idempotencyKey: appTransId,
         transactionId: "", // Set after ZaloPay callback
            paymentMethod: PaymentMethod.EWallet,
            status: PaymentStatus.Pending,
          amount: order.TotalAmount,
 currency: "VND");
        
        await _transactionRepository.AddAsync(transaction, cancellationToken);
        
    // 5. Build ZaloPay create order request
     var zalopayRequest = new ZaloPayCreateOrderRequest
        {
      AppId = settings.AppId,
      AppTransId = appTransId,
       AppUser = customerId,
    AppTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    Amount = (long)order.TotalAmount,
    Description = $"Thanh toan don hang {order.OrderNumber}",
            Item = JsonSerializer.Serialize(new[] 
            { 
             new { itemid = order.OrderNumber, itemname = "Order", itemprice = order.TotalAmount, itemquantity = 1 } 
            }),
            EmbedData = JsonSerializer.Serialize(new { redirecturl = settings.RedirectUrl })
      };
        
        // 6. Generate MAC (Message Authentication Code) with Key1
        zalopayRequest.Mac = GenerateMac(zalopayRequest, settings.Key1);
        
    // 7. Store raw request
        transaction.SetRawRequest(JsonSerializer.Serialize(zalopayRequest));
     await _transactionRepository.UpdateAsync(transaction, cancellationToken);
      
        // 8. Call ZaloPay API
        var httpClient = _httpClientFactory.CreateClient("ZaloPayClient");
   var response = await httpClient.PostAsJsonAsync(settings.Endpoint, zalopayRequest, cancellationToken);
  
        if (!response.IsSuccessStatusCode)
 {
   var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ZaloPay API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            throw new InvalidOperationException($"ZaloPay API error: {response.StatusCode}");
        }
        
        var zalopayResponse = await response.Content.ReadFromJsonAsync<ZaloPayCreateOrderResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Invalid ZaloPay response");
        
        // 9. Store raw response
      transaction.SetRawResponse(JsonSerializer.Serialize(zalopayResponse));
        await _transactionRepository.UpdateAsync(transaction, cancellationToken);
        
 // 10. Check return code
    if (zalopayResponse.ReturnCode != 1)
  {
       _logger.LogWarning("ZaloPay order creation failed: {ReturnCode} - {Message}",
       zalopayResponse.ReturnCode, zalopayResponse.ReturnMessage);
   
            transaction.SetFailed();
   await _transactionRepository.UpdateAsync(transaction, cancellationToken);
      
        throw new InvalidOperationException($"ZaloPay error: {zalopayResponse.ReturnMessage}");
        }
        
        _logger.LogInformation(
            "Created ZaloPay order: OrderId={OrderId}, AppTransId={AppTransId}, Amount={Amount}",
            orderId, appTransId, order.TotalAmount);
   
        return new ZaloPayOrderInfo(
  zalopayResponse.OrderUrl,
     zalopayResponse.ZpTransToken,
        appTransId);
    }
    
    // ==================== PROCESS CALLBACK ⭐⭐⭐ ====================
    
    public async Task<ZaloPayCallbackResponse> ProcessCallbackAsync(
     ZaloPayCallbackRequest callbackRequest,
        CancellationToken cancellationToken = default)
    {
        // 1. Get provider & settings
        var provider = await GetZaloPayProviderAsync(cancellationToken);
        var settings = DeserializeSettings(provider.Configuration);
        
  // 2. Create webhook record (audit trail)
        var webhook = new PaymentWebhook(
         paymentProviderId: provider.Id,
  eventType: "payment.callback",
            payload: JsonSerializer.Serialize(callbackRequest),
 signature: callbackRequest.Mac);
    
        await _webhookRepository.AddAsync(webhook, cancellationToken);
        
     // 3. Validate MAC with Key2 (ZaloPay uses different key for callback)
     if (!ValidateMac(callbackRequest, settings.Key2))
        {
    _logger.LogWarning("ZaloPay Callback: Invalid MAC");
            webhook.MarkAsFailed("Invalid MAC");
  await _webhookRepository.UpdateAsync(webhook, cancellationToken);
     
   return new ZaloPayCallbackResponse(-1, "Invalid MAC");
   }
        
        webhook.MarkAsVerified();
        
// 4. Find transaction by AppTransId (our IdempotencyKey)
     var transaction = await _transactionRepository
   .FirstOrDefaultAsync(
new PaymentTransactionByIdempotencyKeySpec(callbackRequest.AppTransId),
cancellationToken);
   
        if (transaction == null)
        {
       _logger.LogWarning("ZaloPay Callback: Transaction {AppTransId} not found", callbackRequest.AppTransId);
 webhook.MarkAsFailed("Transaction not found");
            await _webhookRepository.UpdateAsync(webhook, cancellationToken);
       
   return new ZaloPayCallbackResponse(2, "Order not found");
        }
        
        // 5. Validate amount
        if (transaction.Amount != callbackRequest.Amount)
        {
            _logger.LogWarning("ZaloPay Callback: Amount mismatch. Expected={Expected}, Actual={Actual}",
         transaction.Amount, callbackRequest.Amount);
      webhook.MarkAsFailed("Amount mismatch");
   await _webhookRepository.UpdateAsync(webhook, cancellationToken);
      
    return new ZaloPayCallbackResponse(2, "Amount mismatch");
        }
        
        // 6. Idempotency check
        if (transaction.Status == PaymentStatus.Completed)
        {
     _logger.LogInformation("ZaloPay Callback: Transaction {AppTransId} already processed", callbackRequest.AppTransId);
            webhook.MarkAsProcessed(transaction.Id);
            await _webhookRepository.UpdateAsync(webhook, cancellationToken);
      
      return new ZaloPayCallbackResponse(1, "Confirm Success");
     }
        
        // 7. Process payment (ZaloPay only sends callback for successful payments)
  transaction.SetTransactionId(callbackRequest.ZpTransId);
        transaction.SetCompleted();
   await _transactionRepository.UpdateAsync(transaction, cancellationToken);
        
      webhook.MarkAsProcessed(transaction.Id);
        await _webhookRepository.UpdateAsync(webhook, cancellationToken);
        
    // 8. Raise domain event for order confirmation
        await _eventPublisher.PublishAsync(
          new OrderPaidEvent(transaction.OrderId, transaction.Id),
        cancellationToken);
  
        _logger.LogInformation(
            "ZaloPay payment SUCCESS: OrderId={OrderId}, ZpTransId={ZpTransId}",
            transaction.OrderId, callbackRequest.ZpTransId);
        
        return new ZaloPayCallbackResponse(1, "Confirm Success");
  }
    
    // ==================== HELPER METHODS ====================
    
    private async Task<PaymentProvider> GetZaloPayProviderAsync(CancellationToken ct)
    {
        if (_cachedProvider != null)
  return _cachedProvider;
   
        _cachedProvider = await _providerRepository
  .FirstOrDefaultAsync(new PaymentProviderByCodeSpec("ZALOPAY"), ct)
      ?? throw new NotFoundException("ZaloPay provider not configured");
        
      return _cachedProvider;
    }
    
    private ZaloPaySettings DeserializeSettings(string configJson)
    {
        // In production: Decrypt first using ConfigurationEncryptionService (BUILD_34 Part 2)
    return JsonSerializer.Deserialize<ZaloPaySettings>(configJson)
  ?? throw new InvalidOperationException("Invalid ZaloPay configuration");
  }
    
    /// <summary>
    /// Generate app_trans_id (ZaloPay format: yyMMdd_xxxxxx)
    /// Example: 250201_123456789012
    /// </summary>
    private string GenerateAppTransId()
    {
        var datePrefix = DateTime.Now.ToString("yyMMdd");
   var uniqueId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        return $"{datePrefix}_{uniqueId}";
    }
    
    /// <summary>
    /// Generate MAC for create order request using HMACSHA256
    /// Format: app_id|app_trans_id|app_user|amount|app_time|embed_data|item
    /// </summary>
    private string GenerateMac(ZaloPayCreateOrderRequest request, string key)
    {
        var data = $"{request.AppId}|{request.AppTransId}|{request.AppUser}|{request.Amount}|{request.AppTime}|{request.EmbedData}|{request.Item}";
  
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
   using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
 
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }
    
    /// <summary>
    /// Validate MAC for callback using Key2
/// Format: app_id|app_trans_id|app_user|amount|app_time|embed_data|item (same as create order)
    /// NOTE: ZaloPay uses Key2 for callback validation (different from Key1 for create order)
    /// </summary>
    private bool ValidateMac(ZaloPayCallbackRequest callback, string key2)
    {
  var data = $"{callback.AppId}|{callback.AppTransId}|{callback.AppUser}|{callback.Amount}|{callback.AppTime}|{callback.EmbedData}|{callback.Item}";
        
      var keyBytes = Encoding.UTF8.GetBytes(key2);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
    using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
        var expectedMac = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        
 return string.Equals(callback.Mac, expectedMac, StringComparison.OrdinalIgnoreCase);
    }
}
```

**⭐ Key Differences from Momo:**
- ✅ **app_trans_id Format:** Must follow `yyMMdd_xxxxxx` pattern (ZaloPay requirement)
- ✅ **Two Keys:** Key1 for create order, Key2 for callback validation
- ✅ **MAC Algorithm:** HMACSHA256 with pipe-delimited data
- ✅ **Return Code:** 1 = Success (different from Momo's 0)
- ✅ **Callback Only for Success:** ZaloPay only sends callback for successful payments (no failed payment callback)

---

## 5. Controllers

### Bước 5.1: Update WebhooksController

**File:** `src/Host/Host/Controllers/WebhooksController.cs` (Update)

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Infrastructure.Payment.ZaloPay.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IVNPayService _vnpayService;
 private readonly IMomoService _momoService;
    private readonly IZaloPayService _zalopayService;
    private readonly ILogger<WebhooksController> _logger;
    
    public WebhooksController(
        IVNPayService vnpayService,
        IMomoService momoService,
     IZaloPayService zalopayService,
     ILogger<WebhooksController> logger)
    {
        _vnpayService = vnpayService;
        _momoService = momoService;
        _zalopayService = zalopayService;
        _logger = logger;
    }
    
    /// <summary>
    /// VNPay IPN endpoint (from BUILD_35)
    /// </summary>
    [HttpGet("vnpay")]
    [AllowAnonymous]
    public async Task<IActionResult> VNPayIpn(CancellationToken cancellationToken)
    {
        // ...existing VNPay code...
    }
    
    /// <summary>
    /// Momo IPN endpoint (from BUILD_36)
    /// </summary>
    [HttpPost("momo")]
    [AllowAnonymous]
    public async Task<IActionResult> MomoIpn(
        [FromBody] MomoIpnRequest ipnRequest,
    CancellationToken cancellationToken)
    {
        // ...existing Momo code...
    }
    
    /// <summary>
    /// ZaloPay Callback endpoint
    /// ⭐ IMPORTANT: ZaloPay sends form data, NOT JSON
    /// </summary>
    [HttpPost("zalopay")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ZaloPayCallback(
        [FromForm] ZaloPayCallbackRequest callbackRequest,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("ZaloPay Callback received: {Payload}",
            System.Text.Json.JsonSerializer.Serialize(callbackRequest));
        
        var response = await _zalopayService.ProcessCallbackAsync(callbackRequest, cancellationToken);
        
        // Return JSON response to ZaloPay
        return Ok(new
        {
            return_code = response.ReturnCode,
          return_message = response.ReturnMessage
   });
    }
}
```

**⭐ Important Note:**
- ZaloPay sends callback as `application/x-www-form-urlencoded`, NOT JSON
- Use `[FromForm]` attribute, not `[FromBody]`
- This is different from Momo (which uses JSON)

---

### Bước 5.2: Update PaymentController

**File:** `src/Host/Host/Controllers/PaymentController.cs` (Update)

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
    private readonly IMomoService _momoService;
    private readonly IZaloPayService _zalopayService;
    private readonly ILogger<PaymentController> _logger;
    
    public PaymentController(
        IVNPayService vnpayService,
 IMomoService momoService,
      IZaloPayService zalopayService,
        ILogger<PaymentController> logger)
    {
        _vnpayService = vnpayService;
        _momoService = momoService;
        _zalopayService = zalopayService;
        _logger = logger;
    }
    
    /// <summary>
    /// Create VNPay payment (from BUILD_35)
    /// </summary>
    [HttpPost("vnpay/create")]
 [MustHavePermission(ECOAction.Create, ECOFunction.Payments)]
    public async Task<ActionResult<CreateVNPayPaymentResponse>> CreateVNPayPayment(...)
    {
        // ...existing VNPay code...
    }
  
    /// <summary>
    /// Create Momo payment (from BUILD_36)
    /// </summary>
    [HttpPost("momo/create")]
    [MustHavePermission(ECOAction.Create, ECOFunction.Payments)]
    public async Task<ActionResult<CreateMomoPaymentResponse>> CreateMomoPayment(...)
    {
        // ...existing Momo code...
    }
    
    /// <summary>
    /// Create ZaloPay payment
    /// Returns payment URL and ZaloPay app deeplink
    /// </summary>
    [HttpPost("zalopay/create")]
    [MustHavePermission(ECOAction.Create, ECOFunction.Payments)]
    [ProducesResponseType(typeof(CreateZaloPayPaymentResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateZaloPayPaymentResponse>> CreateZaloPayPayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        // Get customer ID from claims
        var customerId = User.FindFirst("uid")?.Value ?? "guest";
        
var orderInfo = await _zalopayService.CreateOrderAsync(
            request.OrderId,
  customerId,
          cancellationToken);
        
     _logger.LogInformation("Created ZaloPay order for Order {OrderId}", request.OrderId);
     
        // Generate deeplink for ZaloPay app
     var deeplink = $"zalopay://app?zptranstoken={orderInfo.ZpTransToken}";
        
  return Ok(new CreateZaloPayPaymentResponse
        {
            OrderId = request.OrderId,
    OrderUrl = orderInfo.OrderUrl,
  Deeplink = deeplink,
 AppTransId = orderInfo.AppTransId,
    ExpiresIn = 900 // 15 minutes
        });
    }
}

public record CreateZaloPayPaymentResponse
{
    public Guid OrderId { get; init; }
    public string OrderUrl { get; init; } = default!;      // Web payment URL
    public string Deeplink { get; init; } = default!;      // ZaloPay app deeplink
    public string AppTransId { get; init; } = default!;    // IdempotencyKey
    public int ExpiresIn { get; init; }
}
```

---

## 6. Dependency Injection

**File:** `src/Infrastructure/Infrastructure/Startup.cs` (Update)

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Infrastructure.Payment.VNPay;
using ECO.WebApi.Infrastructure.Payment.Momo;
using ECO.WebApi.Infrastructure.Payment.ZaloPay;

namespace ECO.WebApi.Infrastructure;

public static class Startup
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
 IConfiguration configuration)
    {
    // ...existing services...
        
        // Payment Services
      services.AddScoped<IVNPayService, VNPayService>();     // BUILD_35
        services.AddScoped<IMomoService, MomoService>(); // BUILD_36
     services.AddScoped<IZaloPayService, ZaloPayService>(); // BUILD_37
        
        // HTTP Client for ZaloPay API
        services.AddHttpClient("ZaloPayClient", client =>
      {
    client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
   
    return services;
    }
}
```

---

## 7. Testing Guide

### Bước 7.1: ZaloPay Sandbox Setup

**1. Đăng ký ZaloPay Developer Account:**
- URL: https://docs.zalopay.vn/
- Liên hệ ZaloPay support để xin sandbox credentials
- Nhận credentials:
- `AppId`
  - `Key1` (for create order)
  - `Key2` (for callback validation)

**2. Configure ngrok cho Callback:**
```bash
# Run ngrok
ngrok http 7001

# Copy HTTPS URL (e.g., https://abc123.ngrok.io)
# Update ZaloPay configuration in PaymentProviderSeeder:
"CallbackUrl": "https://abc123.ngrok.io/api/webhooks/zalopay"
```

---

### Bước 7.2: Test Payment Flow

**Step 1: Create Order** (same as BUILD_35, BUILD_36)

**Step 2: Create ZaloPay Payment**
```http
POST https://localhost:7001/api/payment/zalopay/create
Content-Type: application/json
Authorization: Bearer {token}

{
  "orderId": "order-guid-123"
}

Response:
{
  "orderId": "order-guid-123",
  "orderUrl": "https://sb-openapi.zalopay.vn/order/...",
  "deeplink": "zalopay://app?zptranstoken=...",
  "appTransId": "250201_1706745600000",
  "expiresIn": 900
}
```

**Step 3A: Pay via Web**
- Open `orderUrl` in browser
- Login ZaloPay sandbox account
- Confirm payment

**Step 3B: Pay via ZaloPay App**
- On mobile device, open `deeplink`
- ZaloPay app opens automatically
- One-tap payment confirmation

**Step 4: ZaloPay sends Callback**
```http
POST https://abc123.ngrok.io/api/webhooks/zalopay
Content-Type: application/x-www-form-urlencoded

app_id=2553&app_trans_id=250201_1706745600000&amount=500000&zp_trans_id=240201000000001&mac=...

Response:
{
  "return_code": 1,
  "return_message": "Confirm Success"
}
```

**Step 5: Verify Order Status** (same as BUILD_35, BUILD_36)

---

### Bước 7.3: Test Scenarios

**Scenario 1: Successful Payment**
- ✅ PaymentTransaction created (Status: Pending)
- ✅ ZaloPay API called successfully (return_code = 1)
- ✅ Customer pays via ZaloPay app/web
- ✅ Callback received
- ✅ PaymentWebhook created & MAC verified
- ✅ PaymentTransaction updated (Status: Completed)
- ✅ OrderPaidEvent raised
- ✅ Order confirmed

**Scenario 2: Failed Payment (User Cancels)**
- Customer cancels in ZaloPay app
- **NO callback sent** (ZaloPay only sends callback for success)
- PaymentTransaction remains Pending
- Order remains Pending
- **Manual query needed** to detect cancelled payments

**Scenario 3: Duplicate Callback**
- ZaloPay sends same callback twice
- First callback: Process successfully
- Second callback: Idempotency check → Return return_code 1 without reprocessing

**Scenario 4: Invalid MAC**
- Tampered callback data
- MAC validation fails
- PaymentWebhook marked as failed
- Return return_code -1

---

### Bước 7.4: ZaloPay Return Codes

| ReturnCode | Meaning | Action |
|------------|---------|--------|
| **1** | Success | Payment completed |
| **2** | Failed | Transaction not found or amount mismatch |
| **3** | Pending | Payment still processing |
| **-1** | Error | Invalid MAC or system error |

---

## 8. ZaloPay Unique Features

### 8.1. Zalo Mini App Integration

**Zalo Mini App Payment:**
```javascript
// In Zalo Mini App
ZaloPay.pay({
    zptranstoken: zpTransToken, // From backend CreateOrderAsync response
    success: function(data) {
      // Payment success
        console.log("Payment completed:", data);
 },
    fail: function(error) {
        // Payment failed
        console.error("Payment failed:", error);
    }
});
```

---

### 8.2. Zalo Chat Payment Link

**Send payment link in Zalo chat:**
```csharp
// Generate shareable payment link
var chatPaymentLink = $"https://social.zalopay.vn/spa/m/pa/{orderInfo.ZpTransToken}";

// Send to customer via Zalo OA message
await _zaloOAService.SendMessage(customerId, chatPaymentLink);
```

---

## 9. Monitoring & Production

**Check ZaloPay payments (last 24h):**
```sql
SELECT 
    COUNT(*) AS Total,
    SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS Pending,
    CAST(SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) * 100 AS SuccessRate
FROM payment.PaymentTransactions
WHERE PaymentProviderId = (SELECT Id FROM payment.PaymentProviders WHERE Code = 'ZALOPAY')
AND CreatedAt >= DATEADD(HOUR, -24, GETUTCDATE());
```

---

## 10. Summary

### ✅ BUILD_37 Complete:

**What you have now:**
- [x] ZaloPay payment order creation
- [x] Callback processing (form-urlencoded)
- [x] MAC validation (HMACSHA256 with Key1 & Key2)
- [x] Idempotency check
- [x] Amount validation
- [x] PaymentWebhook audit trail
- [x] OrderPaidEvent (domain event)
- [x] Zalo ecosystem integration (Mini App, Chat)

**Payment Gateway Support:**
- ✅ VNPay (BUILD_35): Bank cards, transfers
- ✅ Momo (BUILD_36): Mobile e-wallet
- ✅ ZaloPay (BUILD_37): Zalo ecosystem e-wallet

---

### 🎯 Next Steps:

**Phase 3: Final Payment Gateway**
- **BUILD_38:** VietQR Integration (Universal QR payment standard)

**Phase 4: Payment Features**
- **BUILD_39:** Refund Implementation
- **BUILD_40:** Payment Method Management
- **BUILD_41:** Payment Analytics Dashboard

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)

---

**Document Version:** 1.0 (ZaloPay E-Wallet Integration)  
**Last Updated:** 2025-02-01  
**Author:** ECO.WebApi Development Team  
**Status:** ✅ Production-Ready - Zalo Ecosystem Focus
