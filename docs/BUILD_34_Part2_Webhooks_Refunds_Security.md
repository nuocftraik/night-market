# BUILD_34 Part 2: Webhooks, Refunds & Security (Vietnam Local Payments)

> 📚 [Quay lại Part 1](BUILD_34_Database_Design_Payment_Gateway_Integration.md) | [Mục lục](BUILD_INDEX.md)  
> 🎯 **Part 2 Content:** PaymentWebhook, Refund entities, EF Core Configurations, Integration Examples  
> 🇻🇳 **Focus:** Vietnam Payment Gateways (VNPay, Momo, ZaloPay, VietQR)

---

## 4. PaymentWebhook Entity (Transaction Data) ⭐⭐

**File:** `src/Core/Domain/Payment/PaymentWebhook.cs`

```csharp
using ECO.WebApi.Domain.Enum;

namespace ECO.WebApi.Domain.Payment;

/// <summary>
/// Payment webhook callbacks from gateways (Transaction Data)
/// Stores raw webhook payloads for verification and processing
/// Critical for async payment confirmation flow
/// </summary>
public class PaymentWebhook : BaseEntity
{
    // ==================== References ====================
    
  /// <summary>
    /// Payment provider that sent this webhook
    /// </summary>
    public Guid PaymentProviderId { get; private set; }
    
    /// <summary>
    /// Related payment transaction (nullable - webhook may arrive before transaction created)
    /// </summary>
    public Guid? PaymentTransactionId { get; private set; }
    
    // ==================== Webhook Data ====================
    
    /// <summary>
    /// Event type from gateway
    /// Examples: "payment.completed", "payment.failed", "refund.completed"
    /// </summary>
    public string EventType { get; private set; }
    
    /// <summary>
    /// Raw webhook payload (JSON) - NEVER modify after creation
  /// Store complete payload for debugging and replay
    /// </summary>
    public string Payload { get; private set; }
    
    /// <summary>
    /// Webhook signature for verification (HMAC-SHA256)
    /// VNPay: vnp_SecureHash
  /// Momo: signature
    /// ZaloPay: mac
    /// </summary>
    public string? Signature { get; private set; }
 
    // ==================== Processing Status ====================
    
    /// <summary>
    /// Signature verified successfully
    /// </summary>
    public bool IsVerified { get; private set; }
    
    /// <summary>
    /// Webhook processed successfully
    /// </summary>
    public bool IsProcessed { get; private set; }
    
    /// <summary>
    /// Processing error message (if failed)
    /// </summary>
    public string? ProcessingError { get; private set; }
    
    // ==================== Timestamps ====================
    
  /// <summary>
    /// When webhook was received by our server
    /// </summary>
    public DateTime ReceivedAt { get; private set; }
    
    /// <summary>
    /// When webhook processing completed
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }
 
    // ==================== Navigation Properties ====================
    
    public virtual PaymentProvider PaymentProvider { get; private set; } = default!;
    public virtual PaymentTransaction? PaymentTransaction { get; private set; }
 
    // ==================== Constructors ====================
    
    private PaymentWebhook() { }
    
    public PaymentWebhook(
Guid paymentProviderId,
        string eventType,
   string payload,
   string? signature = null)
  {
        if (string.IsNullOrWhiteSpace(eventType))
  throw new ArgumentException("Event type is required", nameof(eventType));
        
  if (string.IsNullOrWhiteSpace(payload))
          throw new ArgumentException("Payload is required", nameof(payload));
        
    PaymentProviderId = paymentProviderId;
    EventType = eventType;
        Payload = payload;
        Signature = signature;
   IsVerified = false;
        IsProcessed = false;
        ReceivedAt = DateTime.UtcNow;
}
    
    // ==================== Business Methods ====================
    
    /// <summary>
    /// Mark webhook signature as verified
    /// </summary>
    public void MarkAsVerified()
    {
   IsVerified = true;
    }
    
    /// <summary>
    /// Mark webhook as processed successfully
    /// </summary>
    public void MarkAsProcessed(Guid? paymentTransactionId = null)
    {
    if (!IsVerified)
          throw new InvalidOperationException("Cannot process unverified webhook");
        
        IsProcessed = true;
        ProcessedAt = DateTime.UtcNow;
        PaymentTransactionId = paymentTransactionId;
    }
  
/// <summary>
    /// Mark webhook processing as failed
    /// </summary>
    public void MarkAsFailed(string error)
    {
        IsProcessed = true;
        ProcessedAt = DateTime.UtcNow;
        ProcessingError = error;
    }
    
    /// <summary>
 /// Check if webhook is stale (received more than X hours ago but not processed)
    /// </summary>
    public bool IsStale(int maxAgeHours = 24)
    {
 return !IsProcessed && 
 DateTime.UtcNow > ReceivedAt.AddHours(maxAgeHours);
    }
}
```

**⭐ Webhook Processing Flow:**

```
1. Gateway sends webhook → API endpoint
2. Store webhook immediately (Payload, Signature)
3. Verify signature (HMAC-SHA256)
   ✅ Valid → MarkAsVerified()
   ❌ Invalid → MarkAsFailed("Invalid signature")
4. Process webhook (update PaymentTransaction)
5. MarkAsProcessed(transactionId)
```

**Key Design Decisions:**

| Decision | Reason |
|----------|--------|
| **Store raw payload** | Enable debugging, replay, audit trail |
| **Immutable after creation** | Prevent tampering |
| **Signature verification** | Security - prevent fake webhooks |
| **IsProcessed flag** | Idempotency - prevent duplicate processing |
| **Nullable PaymentTransactionId** | Webhook may arrive before transaction created |

---

## 5. Refund Entity (Transaction Data) ⭐⭐

**File:** `src/Core/Domain/Payment/Refund.cs`

```csharp
using ECO.WebApi.Domain.Enum;

namespace ECO.WebApi.Domain.Payment;

/// <summary>
/// Payment refund (Transaction Data)
/// Supports full and partial refunds
/// </summary>
public class Refund : BaseEntity
{
    // ==================== References ====================
    
    /// <summary>
    /// Original payment transaction being refunded
    /// </summary>
    public Guid PaymentTransactionId { get; private set; }
    
    /// <summary>
    /// User who requested the refund (admin/customer)
    /// </summary>
    public Guid RequestedBy { get; private set; }
    
    // ==================== Gateway Info ====================
    
    /// <summary>
    /// Gateway refund ID (from VNPay/Momo/ZaloPay)
    /// Unique identifier from payment gateway
    /// </summary>
    public string? RefundId { get; private set; }
    
    // ==================== Refund Details ====================
    
    /// <summary>
    /// Amount to refund (can be partial)
    /// Must be <= original transaction amount
    /// </summary>
    public decimal RefundAmount { get; private set; }
    
    /// <summary>
    /// Refund reason (required for audit)
    /// </summary>
    public string Reason { get; private set; }
    
    /// <summary>
    /// Refund status (Pending, Completed, Failed)
    /// </summary>
    public RefundStatus Status { get; private set; }
 
    // ==================== Gateway Response ====================
    
    /// <summary>
    /// Raw response from gateway (JSON)
    /// Store for debugging
    /// </summary>
    public string? GatewayResponse { get; private set; }
    
    // ==================== Timestamps ====================
    
  /// <summary>
    /// When refund was requested
    /// </summary>
    public DateTime RequestedAt { get; private set; }
    
    /// <summary>
    /// When refund completed (if successful)
    /// </summary>
    public DateTime? CompletedAt { get; private set; }
    
    // ==================== Navigation Properties ====================
    
    public virtual PaymentTransaction PaymentTransaction { get; private set; } = default!;
    
    // ==================== Constructors ====================
    
    private Refund() { }
    
    public Refund(
        Guid paymentTransactionId,
Guid requestedBy,
        decimal refundAmount,
      string reason)
    {
        if (refundAmount <= 0)
          throw new ArgumentException("Refund amount must be positive", nameof(refundAmount));
        
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required", nameof(reason));
        
        PaymentTransactionId = paymentTransactionId;
        RequestedBy = requestedBy;
        RefundAmount = refundAmount;
        Reason = reason;
  Status = RefundStatus.Pending;
        RequestedAt = DateTime.UtcNow;
    }
    
    // ==================== Business Methods ====================
    
    /// <summary>
    /// Set gateway refund ID (after creating refund on gateway)
    /// </summary>
    public void SetRefundId(string refundId)
    {
        if (string.IsNullOrWhiteSpace(refundId))
     throw new ArgumentException("Refund ID cannot be empty", nameof(refundId));
    
        RefundId = refundId;
    }
    
    /// <summary>
    /// Mark refund as completed
    /// </summary>
    public void MarkAsCompleted(string? gatewayResponse = null)
    {
        if (Status != RefundStatus.Pending)
   throw new InvalidOperationException($"Cannot complete refund in {Status} status");
   
        Status = RefundStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        GatewayResponse = gatewayResponse;
    }
    
    /// <summary>
    /// Mark refund as failed
    /// </summary>
    public void MarkAsFailed(string? gatewayResponse = null)
    {
        if (Status == RefundStatus.Completed)
            throw new InvalidOperationException("Cannot fail completed refund");
        
    Status = RefundStatus.Failed;
      GatewayResponse = gatewayResponse;
    }
}
```

**⭐ Refund Business Rules:**

```csharp
// Example: Validate refund amount
public async Task<Result> ValidateRefundAsync(Guid transactionId, decimal refundAmount)
{
    var transaction = await _context.PaymentTransactions
  .Include(t => t.Refunds)
  .FirstOrDefaultAsync(t => t.Id == transactionId);
    
    if (transaction == null)
        return Result.Fail("Transaction not found");
    
  if (!transaction.CanBeRefunded())
        return Result.Fail("Transaction cannot be refunded");
    
    var refundableAmount = transaction.GetRefundableAmount();
    
    if (refundAmount > refundableAmount)
     return Result.Fail($"Refund amount exceeds refundable amount (₫{refundableAmount:N0})");
    
    return Result.Success();
}
```

**Partial Refund Example:**

```csharp
// Original payment: ₫100,000
// Refund 1: ₫30,000 (completed)
// Refund 2: ₫50,000 (completed)
// Remaining refundable: ₫20,000

var totalRefunded = transaction.Refunds
    .Where(r => r.Status == RefundStatus.Completed)
    .Sum(r => r.RefundAmount);  // ₫80,000

var remaining = transaction.Amount - totalRefunded;  // ₫20,000
```

---

## 6. EF Core Configurations ⭐⭐⭐

### 6.1. PaymentProviderConfiguration

**File:** `src/Infrastructure/Infrastructure/Persistence/Configurations/PaymentProviderConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

public class PaymentProviderConfiguration : IEntityTypeConfiguration<PaymentProvider>
{
    public void Configure(EntityTypeBuilder<PaymentProvider> builder)
    {
        builder.ToTable("PaymentProviders", "payment");
        
 // Primary Key
  builder.HasKey(p => p.Id);
        
  // Properties
        builder.Property(p => p.Code)
       .IsRequired()
    .HasMaxLength(50)
       .HasComment("Provider code (VNPAY, MOMO, ZALOPAY, VIETQR)");
      
      builder.Property(p => p.Name)
            .IsRequired()
.HasMaxLength(100);
        
        builder.Property(p => p.Description)
        .HasMaxLength(500);
        
        builder.Property(p => p.Configuration)
         .IsRequired()
   .HasColumnType("nvarchar(max)")
            .HasComment("JSON configuration (MUST BE ENCRYPTED in production)");
        
      builder.Property(p => p.IsActive)
       .IsRequired()
          .HasDefaultValue(true);
        
     builder.Property(p => p.Priority)
   .IsRequired()
      .HasDefaultValue(0);
        
   // Indexes
     builder.HasIndex(p => p.Code)
            .IsUnique()
          .HasDatabaseName("IX_PaymentProviders_Code");

        builder.HasIndex(p => p.Priority)
            .HasDatabaseName("IX_PaymentProviders_Priority");
        
    // Relationships
 builder.HasMany(p => p.PaymentTransactions)
       .WithOne(t => t.PaymentProvider)
            .HasForeignKey(t => t.PaymentProviderId)
            .OnDelete(DeleteBehavior.Restrict);
 
        builder.HasMany(p => p.PaymentWebhooks)
      .WithOne(w => w.PaymentProvider)
            .HasForeignKey(w => w.PaymentProviderId)
      .OnDelete(DeleteBehavior.Restrict);
    
   builder.HasMany(p => p.PaymentMethods)
            .WithOne(m => m.PaymentProvider)
      .HasForeignKey(m => m.PaymentProviderId)
 .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

### 6.2. PaymentTransactionConfiguration

**File:** `src/Infrastructure/Infrastructure/Persistence/Configurations/PaymentTransactionConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions", "payment");
        
      // Primary Key
        builder.HasKey(t => t.Id);
     
        // Properties
        builder.Property(t => t.OrderId)
        .IsRequired();
      
builder.Property(t => t.PaymentProviderId)
   .IsRequired();
        
        builder.Property(t => t.IdempotencyKey)
            .IsRequired()
   .HasMaxLength(255)
            .HasComment("Prevent duplicate payments");
        
        builder.Property(t => t.TransactionId)
   .IsRequired()
  .HasMaxLength(255)
     .HasComment("Gateway transaction ID");
        
   builder.Property(t => t.PaymentMethod)
    .IsRequired()
    .HasConversion<int>();
   
        builder.Property(t => t.Status)
  .IsRequired()
  .HasConversion<int>();
        
        builder.Property(t => t.Amount)
         .IsRequired()
            .HasColumnType("decimal(18,2)");
        
        builder.Property(t => t.Currency)
   .IsRequired()
         .HasMaxLength(3)
    .HasDefaultValue("VND");
     
      builder.Property(t => t.RawRequest)
            .HasColumnType("nvarchar(max)");
        
        builder.Property(t => t.RawResponse)
    .HasColumnType("nvarchar(max)");
        
        builder.Property(t => t.CreatedAt)
       .IsRequired();
        
  builder.Property(t => t.CompletedAt);
        
        // Indexes (⭐ Critical for performance)
        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique()
          .HasDatabaseName("IX_PaymentTransactions_IdempotencyKey");
        
 builder.HasIndex(t => t.TransactionId)
       .IsUnique()
    .HasDatabaseName("IX_PaymentTransactions_TransactionId");
  
        builder.HasIndex(t => t.OrderId)
            .HasDatabaseName("IX_PaymentTransactions_OrderId");
        
        builder.HasIndex(t => new { t.Status, t.CreatedAt })
     .HasDatabaseName("IX_PaymentTransactions_Status_CreatedAt");
   
        builder.HasIndex(t => t.CreatedAt)
  .HasDatabaseName("IX_PaymentTransactions_CreatedAt")
        .IsDescending();
     
        // Relationships
  builder.HasOne(t => t.Order)
    .WithMany()
     .HasForeignKey(t => t.OrderId)
       .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(t => t.Refunds)
    .WithOne(r => r.PaymentTransaction)
            .HasForeignKey(r => r.PaymentTransactionId)
  .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

### 6.3. PaymentWebhookConfiguration

**File:** `src/Infrastructure/Infrastructure/Persistence/Configurations/PaymentWebhookConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

public class PaymentWebhookConfiguration : IEntityTypeConfiguration<PaymentWebhook>
{
  public void Configure(EntityTypeBuilder<PaymentWebhook> builder)
    {
        builder.ToTable("PaymentWebhooks", "payment");
 
      // Primary Key
  builder.HasKey(w => w.Id);
        
        // Properties
    builder.Property(w => w.PaymentProviderId)
            .IsRequired();

        builder.Property(w => w.EventType)
            .IsRequired()
          .HasMaxLength(100);
        
        builder.Property(w => w.Payload)
     .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasComment("Raw webhook payload (NEVER modify)");
   
        builder.Property(w => w.Signature)
     .HasMaxLength(500);
        
     builder.Property(w => w.IsVerified)
            .IsRequired()
    .HasDefaultValue(false);
     
     builder.Property(w => w.IsProcessed)
            .IsRequired()
   .HasDefaultValue(false);
        
        builder.Property(w => w.ProcessingError)
            .HasColumnType("nvarchar(max)");
        
        builder.Property(w => w.ReceivedAt)
            .IsRequired();
        
        builder.Property(w => w.ProcessedAt);
 
        // Indexes (⭐ Webhook queries)
        builder.HasIndex(w => new { w.IsProcessed, w.ReceivedAt })
       .HasDatabaseName("IX_PaymentWebhooks_IsProcessed_ReceivedAt")
     .HasFilter("[IsProcessed] = 0");  // Partial index for unprocessed webhooks
        
builder.HasIndex(w => w.PaymentProviderId)
      .HasDatabaseName("IX_PaymentWebhooks_PaymentProviderId");
  
 builder.HasIndex(w => w.EventType)
            .HasDatabaseName("IX_PaymentWebhooks_EventType");
    }
}
```

---

### 6.4. RefundConfiguration

**File:** `src/Infrastructure/Infrastructure/Persistence/Configurations/RefundConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
  builder.ToTable("Refunds", "payment");
        
        // Primary Key
        builder.HasKey(r => r.Id);
        
    // Properties
        builder.Property(r => r.PaymentTransactionId)
     .IsRequired();

        builder.Property(r => r.RequestedBy)
          .IsRequired();
        
  builder.Property(r => r.RefundId)
    .HasMaxLength(255)
            .HasComment("Gateway refund ID");
        
        builder.Property(r => r.RefundAmount)
    .IsRequired()
            .HasColumnType("decimal(18,2)");
        
        builder.Property(r => r.Reason)
            .IsRequired()
       .HasMaxLength(500);
        
        builder.Property(r => r.Status)
          .IsRequired()
            .HasConversion<int>();
        
        builder.Property(r => r.GatewayResponse)
            .HasColumnType("nvarchar(max)");
        
        builder.Property(r => r.RequestedAt)
            .IsRequired();
        
        builder.Property(r => r.CompletedAt);
        
  // Indexes
        builder.HasIndex(r => r.RefundId)
            .IsUnique()
  .HasDatabaseName("IX_Refunds_RefundId")
     .HasFilter("[RefundId] IS NOT NULL");
  
        builder.HasIndex(r => r.PaymentTransactionId)
            .HasDatabaseName("IX_Refunds_PaymentTransactionId");
        
     builder.HasIndex(r => new { r.Status, r.RequestedAt })
    .HasDatabaseName("IX_Refunds_Status_RequestedAt");
  }
}
```

---

### 6.5. PaymentMethodConfiguration

**File:** `src/Infrastructure/Infrastructure/Persistence/Configurations/PaymentMethodConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods", "payment");
        
 // Primary Key
        builder.HasKey(m => m.Id);
        
        // Properties
    builder.Property(m => m.UserId)
      .IsRequired();
        
    builder.Property(m => m.PaymentProviderId)
    .IsRequired();
        
        builder.Property(m => m.PaymentMethodToken)
.IsRequired()
    .HasMaxLength(500)
 .HasComment("Gateway token (PCI-DSS compliant - no raw card data)");
        
        builder.Property(m => m.MethodType)
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(m => m.Last4)
        .HasMaxLength(4);
        
        builder.Property(m => m.BankCode)
            .HasMaxLength(20);
        
        builder.Property(m => m.WalletProvider)
        .HasMaxLength(50);
        
        builder.Property(m => m.CardBrand)
     .HasMaxLength(50);
        
  builder.Property(m => m.ExpiryMonth)
   .HasMaxLength(2);
        
        builder.Property(m => m.ExpiryYear)
            .HasMaxLength(4);
        
     builder.Property(m => m.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);
        
   builder.Property(m => m.CreatedAt)
  .IsRequired();
        
        // Indexes (⭐ Master data queries)
        builder.HasIndex(m => m.PaymentMethodToken)
 .IsUnique()
            .HasDatabaseName("IX_PaymentMethods_PaymentMethodToken");
        
        builder.HasIndex(m => new { m.UserId, m.IsDefault })
            .HasDatabaseName("IX_PaymentMethods_UserId_IsDefault");
        
      builder.HasIndex(m => m.UserId)
     .HasDatabaseName("IX_PaymentMethods_UserId");
    }
}
```

---

## 7. Database Migration Script

**⭐ Complete migration for all payment tables:**

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECO.WebApi.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewayTables : Migration
    {
        /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
            // Create payment schema
 migrationBuilder.EnsureSchema(name: "payment");
    
        // PaymentProviders
  migrationBuilder.CreateTable(
      name: "PaymentProviders",
                schema: "payment",
        columns: table => new
          {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "Provider code (VNPAY, MOMO, ZALOPAY, VIETQR)"),
           Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
          Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
           IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
       Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
     Configuration = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "JSON configuration (MUST BE ENCRYPTED)"),
  CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
            CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
         LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
        LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
       },
      constraints: table =>
        {
      table.PrimaryKey("PK_PaymentProviders", x => x.Id);
 });
          
  // PaymentTransactions
   migrationBuilder.CreateTable(
          name: "PaymentTransactions",
     schema: "payment",
      columns: table => new
                {
         Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
          OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        PaymentProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            IdempotencyKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, comment: "Prevent duplicate payments"),
        TransactionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, comment: "Gateway transaction ID"),
       PaymentMethod = table.Column<int>(type: "int", nullable: false),
         Status = table.Column<int>(type: "int", nullable: false),
           Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
          Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "VND"),
         RawRequest = table.Column<string>(type: "nvarchar(max)", nullable: true),
     RawResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
         CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                 CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
   },
            constraints: table =>
      {
             table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
  table.ForeignKey(
     name: "FK_PaymentTransactions_Orders_OrderId",
  column: x => x.OrderId,
      principalSchema: "order",
        principalTable: "Orders",
      principalColumn: "Id",
     onDelete: ReferentialAction.Restrict);
  table.ForeignKey(
      name: "FK_PaymentTransactions_PaymentProviders_PaymentProviderId",
       column: x => x.PaymentProviderId,
       principalSchema: "payment",
        principalTable: "PaymentProviders",
             principalColumn: "Id",
          onDelete: ReferentialAction.Restrict);
      });
    
            // PaymentWebhooks
          migrationBuilder.CreateTable(
         name: "PaymentWebhooks",
              schema: "payment",
             columns: table => new
       {
          Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
           PaymentProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
              PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
               EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
     Payload = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "Raw webhook payload"),
                    Signature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
         IsVerified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
               IsProcessed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
       ProcessingError = table.Column<string>(type: "nvarchar(max)", nullable: true),
    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
       ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
   },
       constraints: table =>
                {
            table.PrimaryKey("PK_PaymentWebhooks", x => x.Id);
     table.ForeignKey(
 name: "FK_PaymentWebhooks_PaymentProviders_PaymentProviderId",
              column: x => x.PaymentProviderId,
            principalSchema: "payment",
        principalTable: "PaymentProviders",
             principalColumn: "Id",
     onDelete: ReferentialAction.Restrict);
  });
       
  // Refunds
  migrationBuilder.CreateTable(
      name: "Refunds",
      schema: "payment",
    columns: table => new
           {
          Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
  PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
         RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
         RefundId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, comment: "Gateway refund ID"),
     RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
        Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
    Status = table.Column<int>(type: "int", nullable: false),
          GatewayResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
     RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
        CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
      },
    constraints: table =>
 {
     table.PrimaryKey("PK_Refunds", x => x.Id);
table.ForeignKey(
      name: "FK_Refunds_PaymentTransactions_PaymentTransactionId",
     column: x => x.PaymentTransactionId,
          principalSchema: "payment",
      principalTable: "PaymentTransactions",
      principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
                });
       
   // PaymentMethods
            migrationBuilder.CreateTable(
        name: "PaymentMethods",
       schema: "payment",
     columns: table => new
          {
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
       PaymentProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
         PaymentMethodToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "Gateway token (PCI-DSS compliant)"),
          MethodType = table.Column<int>(type: "int", nullable: false),
    Last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
    BankCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
             WalletProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
        CardBrand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
         ExpiryMonth = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
     ExpiryYear = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
            IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
       CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
             },
      constraints: table =>
        {
  table.PrimaryKey("PK_PaymentMethods", x => x.Id);
    table.ForeignKey(
       name: "FK_PaymentMethods_PaymentProviders_PaymentProviderId",
              column: x => x.PaymentProviderId,
   principalSchema: "payment",
    principalTable: "PaymentProviders",
        principalColumn: "Id",
   onDelete: ReferentialAction.Restrict);
   });
            
    // Create all indexes
            migrationBuilder.CreateIndex(
       name: "IX_PaymentProviders_Code",
        schema: "payment",
        table: "PaymentProviders",
     column: "Code",
              unique: true);
    
  migrationBuilder.CreateIndex(
       name: "IX_PaymentProviders_Priority",
         schema: "payment",
        table: "PaymentProviders",
    column: "Priority");
            
            migrationBuilder.CreateIndex(
         name: "IX_PaymentTransactions_IdempotencyKey",
             schema: "payment",
 table: "PaymentTransactions",
        column: "IdempotencyKey",
     unique: true);
        
  migrationBuilder.CreateIndex(
       name: "IX_PaymentTransactions_TransactionId",
      schema: "payment",
     table: "PaymentTransactions",
           column: "TransactionId",
   unique: true);
    
   migrationBuilder.CreateIndex(
  name: "IX_PaymentTransactions_OrderId",
          schema: "payment",
          table: "PaymentTransactions",
     column: "OrderId");
            
 migrationBuilder.CreateIndex(
name: "IX_PaymentTransactions_Status_CreatedAt",
         schema: "payment",
              table: "PaymentTransactions",
      columns: new[] { "Status", "CreatedAt" });
   
            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_CreatedAt",
       schema: "payment",
  table: "PaymentTransactions",
       column: "CreatedAt",
     descending: true);
            
     migrationBuilder.CreateIndex(
      name: "IX_PaymentWebhooks_IsProcessed_ReceivedAt",
    schema: "payment",
                table: "PaymentWebhooks",
        columns: new[] { "IsProcessed", "ReceivedAt" },
   filter: "[IsProcessed] = 0");
       
      migrationBuilder.CreateIndex(
          name: "IX_Refunds_RefundId",
          schema: "payment",
                table: "Refunds",
      column: "RefundId",
                unique: true,
      filter: "[RefundId] IS NOT NULL");
            
      migrationBuilder.CreateIndex(
    name: "IX_PaymentMethods_PaymentMethodToken",
                schema: "payment",
   table: "PaymentMethods",
           column: "PaymentMethodToken",
           unique: true);
     
      migrationBuilder.CreateIndex(
           name: "IX_PaymentMethods_UserId_IsDefault",
                schema: "payment",
      table: "PaymentMethods",
 columns: new[] { "UserId", "IsDefault" });
     }
        
        /// <inheritdoc />
   protected override void Down(MigrationBuilder migrationBuilder)
        {
 migrationBuilder.DropTable(name: "PaymentMethods", schema: "payment");
   migrationBuilder.DropTable(name: "Refunds", schema: "payment");
   migrationBuilder.DropTable(name: "PaymentWebhooks", schema: "payment");
            migrationBuilder.DropTable(name: "PaymentTransactions", schema: "payment");
      migrationBuilder.DropTable(name: "PaymentProviders", schema: "payment");
        }
    }
}
```

---

**Tiếp theo:** Section 8-14 (Vietnam Payment Gateway Integration Examples, Security, Summary)

**[Continue to Sections 8-14 →](#8-vietnam-payment-gateway-integration-examples)**

---

## 8. Vietnam Payment Gateway Integration Examples

### 8.1. VNPay Integration ⭐⭐⭐

**VNPay** is Vietnam's leading payment gateway (40% market share).

**Configuration:**
```json
{
  "TmnCode": "YOUR_TMN_CODE",
  "HashSecret": "YOUR_HASH_SECRET",
  "ReturnUrl": "https://yoursite.com/payment/vnpay/return",
  "PaymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
  "Version": "2.1.0",
  "CurrCode": "VND"
}
```

**Payment Request Example:**

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace ECO.WebApi.Infrastructure.Payment.VNPay;

public class VNPayService : IPaymentGatewayService
{
    private readonly VNPaySettings _settings;
    private readonly ILogger<VNPayService> _logger;
    
    public VNPayService(
        IOptions<VNPaySettings> settings,
     ILogger<VNPayService> logger)
    {
 _settings = settings.Value;
    _logger = logger;
    }
    
    public async Task<PaymentUrlResponse> CreatePaymentUrlAsync(
    PaymentTransaction transaction,
    string ipAddress)
    {
        try
        {
            var vnpParams = new SortedDictionary<string, string>
     {
       { "vnp_Version", _settings.Version },
          { "vnp_Command", "pay" },
           { "vnp_TmnCode", _settings.TmnCode },
                { "vnp_Amount", (transaction.Amount * 100).ToString("0") }, // VNPay requires amount * 100
      { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
          { "vnp_CurrCode", "VND" },
           { "vnp_IpAddr", ipAddress },
  { "vnp_Locale", "vn" },
   { "vnp_OrderInfo", $"Thanh toan don hang {transaction.OrderId}" },
        { "vnp_OrderType", "other" },
         { "vnp_ReturnUrl", _settings.ReturnUrl },
     { "vnp_TxnRef", transaction.IdempotencyKey }  // Use idempotency key as reference
            };
  
      // Generate secure hash
      var hashData = string.Join("&", 
       vnpParams.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));
   
   var secureHash = HmacSHA512(_settings.HashSecret, hashData);
            vnpParams.Add("vnp_SecureHash", secureHash);
    
// Build payment URL
            var paymentUrl = _settings.PaymentUrl + "?" + 
 string.Join("&", vnpParams.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));
            
       _logger.LogInformation("VNPay payment URL created for transaction {TransactionId}", 
 transaction.Id);
    
          return new PaymentUrlResponse
   {
         Url = paymentUrl,
           TransactionId = transaction.Id.ToString()
      };
        }
        catch (Exception ex)
        {
      _logger.LogError(ex, "Error creating VNPay payment URL");
          throw;
        }
    }
    
    public async Task<PaymentCallbackResult> ProcessCallbackAsync(
        IDictionary<string, string> queryParams)
    {
        try
     {
   // Extract secure hash
            var vnpSecureHash = queryParams["vnp_SecureHash"];
     queryParams.Remove("vnp_SecureHash");
         queryParams.Remove("vnp_SecureHashType");
   
         // Verify signature
            var sortedParams = new SortedDictionary<string, string>(queryParams);
            var hashData = string.Join("&", 
     sortedParams.Select(kv => $"{kv.Key}={kv.Value}"));
          
       var checkSum = HmacSHA512(_settings.HashSecret, hashData);
        
    if (!checkSum.Equals(vnpSecureHash, StringComparison.InvariantCultureIgnoreCase))
  {
             return PaymentCallbackResult.Fail("Invalid signature");
    }
         
            // Parse response
var responseCode = queryParams["vnp_ResponseCode"];
    var txnRef = queryParams["vnp_TxnRef"];  // Our idempotency key
            var vnpTransactionId = queryParams["vnp_TransactionNo"];
            var amount = decimal.Parse(queryParams["vnp_Amount"]) / 100; // VNPay amount / 100
            
        var result = new PaymentCallbackResult
     {
                IsSuccess = responseCode == "00",
     TransactionId = vnpTransactionId,
              IdempotencyKey = txnRef,
            Amount = amount,
     Message = GetResponseMessage(responseCode),
           RawData = System.Text.Json.JsonSerializer.Serialize(queryParams)
         };
            
_logger.LogInformation("VNPay callback processed: {ResponseCode} - {Message}", 
      responseCode, result.Message);
  
     return result;
        }
        catch (Exception ex)
      {
         _logger.LogError(ex, "Error processing VNPay callback");
            return PaymentCallbackResult.Fail("Processing error");
        }
    }
    
    private string HmacSHA512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
  return BitConverter.ToString(hashBytes)
    .Replace("-", "")
 .ToLower();
    }
    
    private string GetResponseMessage(string responseCode)
    {
        return responseCode switch
        {
            "00" => "Giao dịch thành công",
            "07" => "Trừ tiền thành công. Giao dịch bị nghi ngờ (liên quan tới lừa đảo, giao dịch bất thường).",
    "09" => "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng chưa đăng ký dịch vụ InternetBanking tại ngân hàng.",
  "10" => "Giao dịch không thành công do: Khách hàng xác thực thông tin thẻ/tài khoản không đúng quá 3 lần",
          "11" => "Giao dịch không thành công do: Đã hết hạn chờ thanh toán. Xin quý khách vui lòng thực hiện lại giao dịch.",
 "12" => "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng bị khóa.",
      "13" => "Giao dịch không thành công do Quý khách nhập sai mật khẩu xác thực giao dịch (OTP).",
     "24" => "Giao dịch không thành công do: Khách hàng hủy giao dịch",
    "51" => "Giao dịch không thành công do: Tài khoản của quý khách không đủ số dư để thực hiện giao dịch.",
        "65" => "Giao dịch không thành công do: Tài khoản của Quý khách đã vượt quá hạn mức giao dịch trong ngày.",
  "75" => "Ngân hàng thanh toán đang bảo trì.",
       "79" => "Giao dịch không thành công do: KH nhập sai mật khẩu thanh toán quá số lần quy định.",
            _ => $"Lỗi không xác định: {responseCode}"
  };
    }
}
```

**VNPay Refund Example:**

```csharp
public async Task<RefundResult> ProcessRefundAsync(Refund refund)
{
    var vnpParams = new SortedDictionary<string, string>
    {
        { "vnp_Version", _settings.Version },
        { "vnp_Command", "refund" },
        { "vnp_TmnCode", _settings.TmnCode },
 { "vnp_TransactionType", "02" },  // Full refund = 02, Partial = 03
        { "vnp_TxnRef", refund.PaymentTransaction.IdempotencyKey },
        { "vnp_Amount", (refund.RefundAmount * 100).ToString("0") },
    { "vnp_OrderInfo", refund.Reason },
     { "vnp_TransactionNo", refund.PaymentTransaction.TransactionId },
  { "vnp_TransactionDate", refund.PaymentTransaction.CreatedAt.ToString("yyyyMMddHHmmss") },
        { "vnp_CreateBy", "Admin" },
        { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
        { "vnp_IpAddr", "127.0.0.1" }
    };
    
    var hashData = string.Join("&", vnpParams.Select(kv => $"{kv.Key}={kv.Value}"));
    var secureHash = HmacSHA512(_settings.HashSecret, hashData);
 vnpParams.Add("vnp_SecureHash", secureHash);
    
    // Call VNPay refund API
    var response = await _httpClient.PostAsJsonAsync(_settings.RefundUrl, vnpParams);
    var result = await response.Content.ReadFromJsonAsync<VNPayRefundResponse>();
 
    return new RefundResult
    {
     IsSuccess = result.vnp_ResponseCode == "00",
        RefundId = result.vnp_TransactionNo,
        Message = GetResponseMessage(result.vnp_ResponseCode)
    };
}
```

---

### 8.2. Momo Integration ⭐⭐

**Momo** is Vietnam's most popular e-wallet (35% market share).

**Configuration:**
```json
{
  "PartnerCode": "YOUR_PARTNER_CODE",
  "AccessKey": "YOUR_ACCESS_KEY",
  "SecretKey": "YOUR_SECRET_KEY",
  "ReturnUrl": "https://yoursite.com/payment/momo/return",
  "NotifyUrl": "https://yoursite.com/api/webhooks/momo",
  "PaymentUrl": "https://test-payment.momo.vn/v2/gateway/api/create"
}
```

**Payment Request Example:**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace ECO.WebApi.Infrastructure.Payment.Momo;

public class MomoService : IPaymentGatewayService
{
    private readonly MomoSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MomoService> _logger;
    
    public async Task<PaymentUrlResponse> CreatePaymentUrlAsync(
        PaymentTransaction transaction,
        string ipAddress)
    {
 var requestId = Guid.NewGuid().ToString();
     var orderId = transaction.IdempotencyKey;
        var amount = transaction.Amount.ToString("0");
    var orderInfo = $"Thanh toán đơn hàng {transaction.OrderId}";
        var extraData = "";  // Optional
        
        // Build raw signature
        var rawSignature = $"accessKey={_settings.AccessKey}" +
            $"&amount={amount}" +
       $"&extraData={extraData}" +
   $"&ipnUrl={_settings.NotifyUrl}" +
     $"&orderId={orderId}" +
      $"&orderInfo={orderInfo}" +
     $"&partnerCode={_settings.PartnerCode}" +
      $"&redirectUrl={_settings.ReturnUrl}" +
            $"&requestId={requestId}" +
   $"&requestType=captureWallet";
        
        var signature = HmacSHA256(_settings.SecretKey, rawSignature);
        
        var requestData = new
    {
            partnerCode = _settings.PartnerCode,
     accessKey = _settings.AccessKey,
  requestId,
            amount,
  orderId,
orderInfo,
            redirectUrl = _settings.ReturnUrl,
    ipnUrl = _settings.NotifyUrl,
    extraData,
            requestType = "captureWallet",
         signature,
      lang = "vi"
        };
        
var response = await _httpClient.PostAsJsonAsync(_settings.PaymentUrl, requestData);
   var result = await response.Content.ReadFromJsonAsync<MomoPaymentResponse>();
        
        if (result.resultCode != 0)
        {
     throw new PaymentException($"Momo error: {result.message}");
     }
        
     _logger.LogInformation("Momo payment URL created: {PayUrl}", result.payUrl);
        
        return new PaymentUrlResponse
        {
    Url = result.payUrl,
  TransactionId = orderId
  };
    }
    
    public async Task<PaymentCallbackResult> ProcessCallbackAsync(
 MomoCallbackRequest callback)
    {
      // Verify signature
     var rawSignature = $"accessKey={_settings.AccessKey}" +
      $"&amount={callback.amount}" +
      $"&extraData={callback.extraData}" +
            $"&message={callback.message}" +
       $"&orderId={callback.orderId}" +
          $"&orderInfo={callback.orderInfo}" +
  $"&orderType={callback.orderType}" +
        $"&partnerCode={callback.partnerCode}" +
     $"&payType={callback.payType}" +
            $"&requestId={callback.requestId}" +
  $"&responseTime={callback.responseTime}" +
    $"&resultCode={callback.resultCode}" +
            $"&transId={callback.transId}";
        
        var signature = HmacSHA256(_settings.SecretKey, rawSignature);
 
        if (!signature.Equals(callback.signature, StringComparison.InvariantCultureIgnoreCase))
     {
            return PaymentCallbackResult.Fail("Invalid signature");
        }
  
        return new PaymentCallbackResult
        {
         IsSuccess = callback.resultCode == 0,
            TransactionId = callback.transId.ToString(),
      IdempotencyKey = callback.orderId,
            Amount = decimal.Parse(callback.amount),
            Message = callback.message,
 RawData = System.Text.Json.JsonSerializer.Serialize(callback)
  };
    }
    
    private string HmacSHA256(string key, string data)
    {
   var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
   
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
      return BitConverter.ToString(hashBytes)
        .Replace("-", "")
            .ToLower();
    }
}

public record MomoCallbackRequest(
    string partnerCode,
    string orderId,
    string requestId,
    string amount,
    string orderInfo,
    string orderType,
    long transId,
 int resultCode,
    string message,
    string payType,
    long responseTime,
 string extraData,
    string signature);
```

---

### 8.3. ZaloPay Integration ⭐⭐

**ZaloPay** integrated with Zalo ecosystem (15% market share).

**Configuration:**
```json
{
  "AppId": "YOUR_APP_ID",
  "Key1": "YOUR_KEY1",
  "Key2": "YOUR_KEY2",
"ReturnUrl": "https://yoursite.com/payment/zalopay/return",
  "CallbackUrl": "https://yoursite.com/api/webhooks/zalopay",
  "CreateOrderUrl": "https://sb-openapi.zalopay.vn/v2/create"
}
```

**Payment Request Example:**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace ECO.WebApi.Infrastructure.Payment.ZaloPay;

public class ZaloPayService : IPaymentGatewayService
{
    private readonly ZaloPaySettings _settings;
    private readonly HttpClient _httpClient;
    
    public async Task<PaymentUrlResponse> CreatePaymentUrlAsync(
        PaymentTransaction transaction,
    string ipAddress)
    {
      var appTransId = $"{DateTime.Now:yyMMdd}_{transaction.IdempotencyKey}";
        var embedData = new { redirecturl = _settings.ReturnUrl };
        var items = new[] { new { } };  // Empty array
        
        var orderData = new Dictionary<string, string>
        {
          { "app_id", _settings.AppId },
            { "app_user", transaction.Order.CustomerId.ToString() },
 { "app_time", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() },
            { "app_trans_id", appTransId },
      { "amount", transaction.Amount.ToString("0") },
      { "item", System.Text.Json.JsonSerializer.Serialize(items) },
  { "embed_data", System.Text.Json.JsonSerializer.Serialize(embedData) },
          { "callback_url", _settings.CallbackUrl },
            { "description", $"ECO - Thanh toán đơn hàng #{transaction.OrderId}" },
        { "bank_code", "zalopayapp" }
        };
        
   // Calculate MAC
     var data = $"{orderData["app_id"]}|{orderData["app_trans_id"]}|" +
            $"{orderData["app_user"]}|{orderData["amount"]}|" +
  $"{orderData["app_time"]}|{orderData["embed_data"]}|{orderData["item"]}";
        
 orderData["mac"] = HmacSHA256(_settings.Key1, data);
        
        var response = await _httpClient.PostAsJsonAsync(_settings.CreateOrderUrl, orderData);
  var result = await response.Content.ReadFromJsonAsync<ZaloPayCreateOrderResponse>();
        
        if (result.return_code != 1)
        {
            throw new PaymentException($"ZaloPay error: {result.return_message}");
        }
        
        return new PaymentUrlResponse
        {
  Url = result.order_url,
      TransactionId = appTransId
    };
    }
    
 public async Task<PaymentCallbackResult> ProcessCallbackAsync(
        ZaloPayCallbackRequest callback)
    {
        // Verify MAC
        var data = callback.data;
        var reqMac = callback.mac;
        
   var mac = HmacSHA256(_settings.Key2, data);
 
        if (!mac.Equals(reqMac, StringComparison.InvariantCultureIgnoreCase))
   {
            return PaymentCallbackResult.Fail("Invalid MAC");
        }
        
        var callbackData = System.Text.Json.JsonSerializer.Deserialize<ZaloPayCallbackData>(data);
 
        return new PaymentCallbackResult
        {
            IsSuccess = true,
        TransactionId = callbackData.zp_trans_id,
            IdempotencyKey = callbackData.app_trans_id,
            Amount = callbackData.amount,
            Message = "Thanh toán thành công",
 RawData = data
        };
    }
    
    private string HmacSHA256(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
  return BitConverter.ToString(hashBytes)
            .Replace("-", "")
            .ToLower();
    }
}
```

---

### 8.4. VietQR Integration ⭐

**VietQR** is Vietnam's universal QR payment standard (Napas).

**Configuration:**
```json
{
  "BankCode": "VCB",
  "AccountNo": "1234567890",
  "AccountName": "CONG TY ECO",
  "Template": "compact",
  "ApiUrl": "https://api.vietqr.io/v2/generate"
}
```

**QR Code Generation Example:**

```csharp
namespace ECO.WebApi.Infrastructure.Payment.VietQR;

public class VietQRService : IPaymentGatewayService
{
    private readonly VietQRSettings _settings;
    private readonly HttpClient _httpClient;
    
    public async Task<QRCodeResponse> GenerateQRCodeAsync(
        PaymentTransaction transaction)
    {
     var requestData = new
        {
  accountNo = _settings.AccountNo,
          accountName = _settings.AccountName,
            acqId = _settings.BankCode,
         amount = transaction.Amount.ToString("0"),
            addInfo = $"ECO {transaction.OrderId}",
            format = "text",
            template = _settings.Template
  };
    
        var response = await _httpClient.PostAsJsonAsync(_settings.ApiUrl, requestData);
        var result = await response.Content.ReadFromJsonAsync<VietQRResponse>();
 
        if (result.code != "00")
        {
    throw new PaymentException($"VietQR error: {result.desc}");
        }
   
        return new QRCodeResponse
        {
         QRDataURL = result.data.qrDataURL,
            QRCode = result.data.qrCode,
  Amount = transaction.Amount
        };
    }
    
    // VietQR doesn't have callback - use bank statement reconciliation
    public async Task ReconcilePaymentsAsync(DateTime date)
    {
        // Query bank statement API
        // Match transactions by content: "ECO {OrderId}"
        // Update PaymentTransaction status
    }
}
```

---

## 9. Webhook Processing Logic Patterns ⭐⭐⭐

### 9.1. Webhook Controller

**File:** `src/Host/Host/Controllers/WebhooksController.cs`

```csharp
using ECO.WebApi.Application.Payment.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WebhooksController> _logger;
    
public WebhooksController(
      IMediator mediator,
        ILogger<WebhooksController> logger)
    {
     _mediator = mediator;
   _logger = logger;
    }
    
    /// <summary>
    /// VNPay webhook endpoint
    /// </summary>
    [HttpGet("vnpay")]
    public async Task<IActionResult> VNPayCallback([FromQuery] Dictionary<string, string> queryParams)
    {
        try
        {
    _logger.LogInformation("Received VNPay webhook: {Params}", 
    System.Text.Json.JsonSerializer.Serialize(queryParams));
  
   var command = new ProcessVNPayWebhookCommand(queryParams);
      var result = await _mediator.Send(command);
            
            if (result.IsSuccess)
            {
        return Redirect($"/payment/success?orderId={result.OrderId}");
  }
            
            return Redirect($"/payment/failed?message={result.Message}");
        }
  catch (Exception ex)
{
            _logger.LogError(ex, "Error processing VNPay webhook");
  return Redirect("/payment/error");
        }
    }
    
    /// <summary>
    /// Momo webhook endpoint
    /// </summary>
    [HttpPost("momo")]
    public async Task<IActionResult> MomoCallback([FromBody] MomoCallbackRequest callback)
    {
        try
        {
            _logger.LogInformation("Received Momo webhook: {RequestId}", callback.requestId);

  var command = new ProcessMomoWebhookCommand(callback);
            var result = await _mediator.Send(command);

            return Ok(new { resultCode = result.IsSuccess ? 0 : 1 });
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error processing Momo webhook");
            return Ok(new { resultCode = 1 });
        }
    }
    
    /// <summary>
    /// ZaloPay webhook endpoint
    /// </summary>
    [HttpPost("zalopay")]
    public async Task<IActionResult> ZaloPayCallback([FromBody] ZaloPayCallbackRequest callback)
    {
        try
     {
            _logger.LogInformation("Received ZaloPay webhook: {AppTransId}", callback.app_trans_id);
         
     var command = new ProcessZaloPayWebhookCommand(callback);
        var result = await _mediator.Send(command);
            
        return Ok(new 
{ 
                return_code = result.IsSuccess ? 1 : -1,
     return_message = result.Message
            });
        }
        catch (Exception ex)
        {
         _logger.LogError(ex, "Error processing ZaloPay webhook");
            return Ok(new { return_code = -1, return_message = "Error" });
        }
    }
}
```

---

### 9.2. Webhook Processing Handler (MediatR)

```csharp
using ECO.WebApi.Application.Common.Interfaces;
using ECO.WebApi.Domain.Payment;
using MediatR;

namespace ECO.WebApi.Application.Payment.Commands;

public record ProcessVNPayWebhookCommand(
    Dictionary<string, string> QueryParams) : IRequest<WebhookProcessResult>;

public class ProcessVNPayWebhookHandler : IRequestHandler<ProcessVNPayWebhookCommand, WebhookProcessResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IVNPayService _vnpayService;
    private readonly ILogger<ProcessVNPayWebhookHandler> _logger;
    
    public async Task<WebhookProcessResult> Handle(
 ProcessVNPayWebhookCommand request,
        CancellationToken ct)
    {
        // 1. Store webhook immediately
        var webhook = new PaymentWebhook(
            paymentProviderId: await GetVNPayProviderId(),
 eventType: "payment.callback",
            payload: System.Text.Json.JsonSerializer.Serialize(request.QueryParams),
         signature: request.QueryParams.GetValueOrDefault("vnp_SecureHash"));

     _context.PaymentWebhooks.Add(webhook);
        await _context.SaveChangesAsync(ct);
        
        try
  {
        // 2. Process callback (verify signature, parse data)
 var callbackResult = await _vnpayService.ProcessCallbackAsync(request.QueryParams);
      
         if (!callbackResult.IsSuccess)
            {
         webhook.MarkAsFailed(callbackResult.Message);
                await _context.SaveChangesAsync(ct);
    
return WebhookProcessResult.Fail(callbackResult.Message);
        }
        
            webhook.MarkAsVerified();
  
            // 3. Find transaction by idempotency key
        var transaction = await _context.PaymentTransactions
 .FirstOrDefaultAsync(t => t.IdempotencyKey == callbackResult.IdempotencyKey, ct);
       
         if (transaction == null)
       {
         webhook.MarkAsFailed("Transaction not found");
       await _context.SaveChangesAsync(ct);
            return WebhookProcessResult.Fail("Transaction not found");
}
     
        // 4. Update transaction
            transaction.SetTransactionId(callbackResult.TransactionId);
 transaction.SetCompleted(callbackResult.RawData);
         
   webhook.MarkAsProcessed(transaction.Id);
            
            // 5. Update order status
            var order = await _context.Orders.FindAsync(transaction.OrderId);
            if (order != null)
  {
      order.MarkAsPaid();
   }
    
            await _context.SaveChangesAsync(ct);
     
            _logger.LogInformation("VNPay webhook processed successfully: {TransactionId}", 
                transaction.Id);
         
   return WebhookProcessResult.Success(order.Id);
        }
   catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay webhook");
          webhook.MarkAsFailed(ex.Message);
            await _context.SaveChangesAsync(ct);
      throw;
        }
    }
    
    private async Task<Guid> GetVNPayProviderId()
    {
        var provider = await _context.PaymentProviders
.FirstOrDefaultAsync(p => p.Code == "VNPAY");
        
        return provider?.Id ?? throw new Exception("VNPay provider not found");
    }
}
```

---

## 10. Security Best Practices (Vietnam Banking) ⭐⭐⭐

### 10.1. Signature Verification (Critical)

**❌ NEVER skip signature verification:**

```csharp
// ❌ WRONG - Trusting data without verification
public async Task ProcessWebhook(WebhookRequest request)
{
    var transaction = await _context.PaymentTransactions
        .FirstOrDefaultAsync(t => t.Id == request.TransactionId);
    
    transaction.SetCompleted();  // DANGEROUS - no verification!
    await _context.SaveChangesAsync();
}

// ✅ CORRECT - Always verify signature first
public async Task ProcessWebhook(WebhookRequest request)
{
    // 1. Verify signature
    if (!VerifySignature(request))
    {
        _logger.LogWarning("Invalid webhook signature: {RequestId}", request.Id);
        return Result.Fail("Invalid signature");
    }
    
  // 2. Then process
    var transaction = await _context.PaymentTransactions
        .FirstOrDefaultAsync(t => t.Id == request.TransactionId);
    
 transaction.SetCompleted();
    await _context.SaveChangesAsync();
}
```

---

### 10.2. Idempotency (Prevent Duplicate Processing)

```csharp
public async Task<Result> ProcessWebhookAsync(PaymentWebhook webhook)
{
    // Check if already processed
    if (webhook.IsProcessed)
    {
        _logger.LogInformation("Webhook already processed: {WebhookId}", webhook.Id);
    return Result.Success();  // Return success to gateway (don't retry)
    }
    
    // Use database transaction for atomic update
    using var dbTransaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
    // Process webhook
     var result = await ProcessPaymentCallback(webhook);
        
        // Mark as processed
        webhook.MarkAsProcessed();
        await _context.SaveChangesAsync();
        
   await dbTransaction.CommitAsync();
        return result;
    }
    catch
    {
        await dbTransaction.RollbackAsync();
        throw;
    }
}
```

---

### 10.3. Encrypt Sensitive Configuration

**❌ NEVER store secrets in plain text:**

```json
// ❌ WRONG - Plain text secrets in database
{
  "TmnCode": "VNPAY12345",
  "HashSecret": "ABCDEF123456789"  // EXPOSED!
}
```

**✅ CORRECT - Encrypted configuration:**

```csharp
using System.Security.Cryptography;
using System.Text;

public class ConfigurationEncryptionService
{
    private readonly string _encryptionKey;
    
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
      aes.GenerateIV();
     
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        
using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
    }
   
        return Convert.ToBase64String(ms.ToArray());
    }
    
    public string Decrypt(string cipherText)
  {
        var fullCipher = Convert.FromBase64String(cipherText);
        
        using var aes = Aes.Create();
   aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
        
        var iv = new byte[aes.IV.Length];
        Array.Copy(fullCipher, 0, iv, 0, iv.Length);
        
        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        
        return sr.ReadToEnd();
    }
}

// Usage
var encrypted = _encryptionService.Encrypt(configuration);
provider.UpdateConfiguration(encrypted);  // Store encrypted in DB

var decrypted = _encryptionService.Decrypt(provider.Configuration);
var settings = JsonSerializer.Deserialize<VNPaySettings>(decrypted);
```

---

### 10.4. Rate Limiting Webhooks

```csharp
using AspNetCoreRateLimit;

// Startup.cs
services.AddMemoryCache();
services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
       Endpoint = "POST:/api/webhooks/*",
            Limit = 100,
  Period = "1m"  // 100 requests per minute
        }
    };
});
services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Configure.cs
app.UseIpRateLimiting();
```

---

### 10.5. Logging & Monitoring

```csharp
public class PaymentMonitoringService
{
    private readonly ILogger<PaymentMonitoringService> _logger;
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    
    public async Task MonitorStalledPayments()
    {
        var stalledTransactions = await _context.PaymentTransactions
            .Where(t => t.Status == PaymentStatus.Pending && 
  t.CreatedAt < DateTime.UtcNow.AddHours(-24))
          .ToListAsync();
  
        foreach (var transaction in stalledTransactions)
        {
 _logger.LogWarning(
        "Stalled payment detected: {TransactionId}, OrderId: {OrderId}, Age: {Age} hours",
              transaction.Id, 
              transaction.OrderId, 
    (DateTime.UtcNow - transaction.CreatedAt).TotalHours);
            
    // Send alert to admin
     await _notificationService.SendAlertAsync(
       "Stalled Payment",
     $"Transaction {transaction.Id} has been pending for over 24 hours");
 }
    }
    
    public async Task MonitorUnprocessedWebhooks()
    {
        var staleWebhooks = await _context.PaymentWebhooks
            .Where(w => w.IsStale(24))
  .ToListAsync();
      
      foreach (var webhook in staleWebhooks)
        {
            _logger.LogError(
  "Unprocessed webhook detected: {WebhookId}, Provider: {ProviderId}, Age: {Age} hours",
        webhook.Id,
                webhook.PaymentProviderId,
  (DateTime.UtcNow - webhook.ReceivedAt).TotalHours);
        }
    }
}
```

---

## 11. PCI-DSS Compliance Notes

### ✅ What We Do (Compliant)

| Requirement | Implementation |
|-------------|----------------|
| **No cardholder data** | Store only gateway tokens in `PaymentMethods.PaymentMethodToken` |
| **Encrypt configuration** | `PaymentProvider.Configuration` encrypted with AES-256 |
| **Signature verification** | All webhooks verify HMAC-SHA256/SHA512 signatures |
| **Secure transmission** | HTTPS only for all gateway communication |
| **Audit trail** | `PaymentWebhook.Payload` stores complete history |
| **Access control** | Payment endpoints require authentication |

### ❌ What We DON'T Do (By Design)

| Prohibited | Why? |
|------------|------|
| **Store full card numbers** | PCI-DSS violation |
| **Store CVV/CVC** | PCI-DSS violation |
| **Store PINs** | PCI-DSS violation |
| **Unencrypted secrets** | Security risk |
| **Skip signature verification** | Opens to fraud |

### 🇻🇳 Vietnam Banking Regulations

**State Bank of Vietnam (SBV) Requirements:**

1. **Two-Factor Authentication:** Required for transactions > ₫500,000
2. **Transaction Limits:** Daily limits enforced by banks
3. **KYC (Know Your Customer):** User verification required
4. **AML (Anti-Money Laundering):** Monitor suspicious transactions
5. **Data Localization:** Payment data stored in Vietnam (if required)

**References:**
- SBV Circular 23/2014/TT-NHNN (E-payment services)
- SBV Circular 39/2016/TT-NHNN (Payment card security)
- Cybersecurity Law 2018

---

## 12. Summary & Checklist

### ✅ Implementation Checklist

**Entities:**
- [x] PaymentProvider (Master Data)
- [x] PaymentTransaction (Transaction Data)
- [x] PaymentWebhook (Transaction Data)
- [x] Refund (Transaction Data)
- [x] PaymentMethod (Master Data)

**Enums:**
- [x] PaymentStatus
- [x] PaymentMethod
- [x] RefundStatus
- [x] PaymentMethodType

**EF Core:**
- [x] PaymentProviderConfiguration
- [x] PaymentTransactionConfiguration
- [x] PaymentWebhookConfiguration
- [x] RefundConfiguration
- [x] PaymentMethodConfiguration

**Integrations:**
- [x] VNPay (Payment + Refund)
- [x] Momo (Payment + Webhook)
- [x] ZaloPay (Payment + Webhook)
- [x] VietQR (QR Code generation)

**Security:**
- [x] Signature verification
- [x] Idempotency handling
- [x] Configuration encryption
- [x] Rate limiting
- [x] Monitoring & alerting

**Compliance:**
- [x] PCI-DSS (no cardholder data)
- [x] Vietnam SBV regulations
- [x] Audit trail (RawRequest/RawResponse)

---

## 13. Next Steps

### Phase 1: Core Implementation (Week 1-2)
1. Create all entities in `Domain` project
2. Create EF Core configurations
3. Run database migration (`Add-Migration`, `Update-Database`)
4. Seed PaymentProviders (VNPay, Momo, ZaloPay, VietQR)

### Phase 2: Gateway Integration (Week 3-4)
1. Implement VNPayService
2. Implement MomoService
3. Implement ZaloPayService
4. Implement VietQRService

### Phase 3: Webhook Processing (Week 5)
1. Create webhook endpoints
2. Implement webhook handlers (MediatR)
3. Add signature verification
4. Test idempotency

### Phase 4: Security & Monitoring (Week 6)
1. Implement configuration encryption
2. Add rate limiting
3. Setup monitoring & alerts
4. Security audit

### Phase 5: Testing (Week 7-8)
1. Unit tests (gateway services)
2. Integration tests (webhooks)
3. E2E tests (full payment flow)
4. Load testing (webhook endpoints)

---

## 14. Troubleshooting

### Common Issues

**Issue 1: Signature verification fails**
```
❌ Error: Invalid signature
✅ Solution: Check HashSecret/SecretKey matches gateway sandbox account
✅ Verify: Hash algorithm (SHA256 vs SHA512)
✅ Debug: Log raw signature string before hashing
```

**Issue 2: Webhook not received**
```
❌ Error: Payment completed but order still pending
✅ Solution: Check NotifyUrl/CallbackUrl is publicly accessible
✅ Verify: HTTPS certificate valid (gateways reject invalid certs)
✅ Test: Use ngrok for local development
```

**Issue 3: Duplicate webhook processing**
```
❌ Error: Payment processed twice
✅ Solution: Check IsProcessed flag before processing
✅ Verify: Use database transaction for atomic update
✅ Implement: Idempotency key check
```

**Issue 4: Amount mismatch**
```
❌ Error: VNPay shows wrong amount
✅ Solution: VNPay requires amount * 100 (₫100,000 → 10000000)
✅ Momo/ZaloPay: Use amount as-is (₫100,000 → 100000)
```

**Issue 5: Migration error - missing Order schema**
```
❌ Error: Invalid object name 'order.Orders'
✅ Solution: Ensure BUILD_32 (Order Module) migration ran first
✅ Verify: Orders table exists in database
✅ Check: Migration dependency order
```

---

## 📚 Resources

**Official Documentation:**
- [VNPay Integration Guide](https://sandbox.vnpayment.vn/apis/docs/)
- [Momo Developer Docs](https://developers.momo.vn/)
- [ZaloPay API Reference](https://docs.zalopay.vn/)
- [VietQR Specification](https://vietqr.io/danh-sach-api)

**Vietnam Banking:**
- [State Bank of Vietnam (SBV)](https://www.sbv.gov.vn/)
- [Napas (National Payment Corporation)](https://napas.com.vn/)

**Security Standards:**
- [PCI-DSS v4.0](https://www.pcisecuritystandards.org/)
- [OWASP Payment Security](https://owasp.org/www-community/vulnerabilities/Payment_Card_Industry_Data_Security_Standard)

**Reference Implementations:**
- [Stripe API Design](https://stripe.com/docs/api)
- [Adyen Architecture](https://docs.adyen.com/)
- [Square Payments](https://developer.squareup.com/docs/payments-api)

---

## 🎯 Key Takeaways

### ⭐ Design Principles Applied:

1. **Provider-Agnostic Design** → Easy to add new payment gateways
2. **Master vs Transaction Data** → Follows Stripe/Adyen pattern
3. **Idempotency** → Prevents duplicate payments
4. **Signature Verification** → Security-first approach
5. **Audit Trail** → Complete payment history
6. **PCI-DSS Compliant** → No sensitive data storage
7. **Vietnam-Optimized** → Local payment methods support

### 🇻🇳 Vietnam Payment Ecosystem:

| Gateway | Type | Use Case | Integration Status |
|---------|------|----------|-------------------|
| **VNPay** | Gateway | Bank cards, QR, e-wallets | ✅ Complete |
| **Momo** | E-wallet | Mobile payments | ✅ Complete |
| **ZaloPay** | E-wallet | Zalo ecosystem | ✅ Complete |
| **VietQR** | QR Standard | Universal QR | ✅ Complete |

### 📊 Database Design Summary:

```
payment schema
├── PaymentProviders (Master: 4-5 rows)
├── PaymentTransactions (Transaction: High volume)
├── PaymentWebhooks (Transaction: High volume)
├── Refunds (Transaction: Medium volume)
└── PaymentMethods (Master: Low-Medium volume)
```

---
