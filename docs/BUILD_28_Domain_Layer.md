# BUILD_28: Catalog Module - Domain Layer (Theoretical Design)

> 📚 [Quay lại BUILD_28 Main](BUILD_28_Catalog_Module.md)  
> 📋 **Prerequisites:** BUILD_09 (Domain Base Entities) hoàn thành

Tài liệu này hướng dẫn xây dựng **Domain Layer** cho Catalog Module theo **theoretical design tốt nhất** (research-based DDD patterns).

---

## 1. Overview

**Làm gì:** Xây dựng Domain Layer với DDD patterns cho Catalog Module (Products & Categories).

**Tại sao theoretical design:**
- ✅ **DDD Best Practices:** Rich domain models, Value Objects, Aggregate Roots
- ✅ **Type-Safe:** Strongly-typed value objects thay vì primitive types
- ✅ **Encapsulation:** Private setters + behavior methods
- ✅ **Domain Events:** Business-driven events
- ✅ **Validation:** Business rules trong domain entities

**Research sources:**
- Domain-Driven Design (Eric Evans)
- Implementing Domain-Driven Design (Vaughn Vernon)
- Microsoft .NET Microservices Architecture
- E-commerce domain patterns (Shopify, Magento architecture)

**Trong bước này chúng ta sẽ:**
- ✅ Tạo Value Objects (Money, SKU, ProductImage)
- ✅ Tạo Domain Events (ProductPriceChanged, ProductStockLow, etc.)
- ✅ Tạo Product Aggregate Root (rich domain model)
- ✅ Tạo Category Aggregate Root (hierarchical)
- ✅ Tạo Domain Enums (type-safe)

---

## 2. Analysis: Workspace Code vs Theoretical Design

### 2.1: Current Workspace Issues

**❌ Problem 1: Primitive Obsession**
```csharp
// Workspace code
public double Price { get; set; }  // double? No currency, no validation
public double? ComparePrice { get; set; }
```

**✅ Solution: Money Value Object**
```csharp
// Theoretical design
public Money Price { get; private set; }  // Type-safe với currency
public Money? CompareAtPrice { get; private set; }
```

**❌ Problem 2: Anemic Domain Model**
```csharp
// Workspace code
public class Product
{
    public double Price { get; set; }  // Public setter
    public int? Quantity { get; set; }  // Anyone can set
}
```

**✅ Solution: Rich Domain Model**
```csharp
// Theoretical design
public class Product
{
    public Money Price { get; private set; }  // Private setter
    public int Stock { get; private set; }
    
    // Business logic methods
    public void UpdatePrice(Money newPrice) { /* validation */ }
    public void DecreaseStock(int quantity) { /* validation */ }
}
```

**❌ Problem 3: No Business Events**
```csharp
// Workspace code
public void UpdateStock(int quantity)
{
    Stock = quantity;  // Silent update, no events
}
```

**✅ Solution: Domain Events**
```csharp
// Theoretical design
public void DecreaseStock(int quantity)
{
    // Validation
    if (Stock < quantity) throw new InsufficientStockException();
    
    Stock -= quantity;
    
    // Raise business event
    if (Stock <= LowStockThreshold)
        DomainEvents.Add(ProductLowStockEvent.Create(this));
}
```

---

## 3. Domain Value Objects

### Bước 3.1: Money Value Object

**Làm gì:** Tạo immutable value object cho monetary values với currency support.

**Tại sao:**
- **Type-safe:** Không thể nhầm lẫn Money với số thông thường
- **Currency support:** Hỗ trợ multi-currency
- **Business operations:** Add, Subtract, Multiply với validation
- **Immutable:** Thread-safe, predictable behavior

**File:** `src/Core/Domain/Catalog/ValueObjects/Money.cs`

```csharp
namespace ECO.WebApi.Domain.Catalog.ValueObjects;

/// <summary>
/// Value Object representing monetary value with currency
/// Immutable by design - follows DDD principles
/// </summary>
public sealed record Money
{
    /// <summary>
    /// Amount in the currency (using decimal for precision)
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Currency code (ISO 4217) - e.g., USD, VND, EUR
    /// </summary>
    public string Currency { get; init; }

    // Private constructor - only factory methods can create
    private Money(decimal amount, string currency)
    {
        if (amount < 0)
    throw new ArgumentException("Amount cannot be negative", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
       throw new ArgumentException("Currency is required", nameof(currency));

        Amount = amount;
      Currency = currency.ToUpperInvariant();
    }

  /// <summary>
    /// Factory method to create Money instance
    /// </summary>
    public static Money Of(decimal amount, string currency = "VND")
        => new(amount, currency);

    /// <summary>
    /// Zero money value
    /// </summary>
    public static Money Zero(string currency = "VND") 
        => new(0, currency);

    /// <summary>
    /// Add two money values (must be same currency)
    /// </summary>
  public Money Add(Money other)
    {
 if (Currency != other.Currency)
     throw new InvalidOperationException($"Cannot add {Currency} and {other.Currency}");

        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Subtract two money values (must be same currency)
    /// </summary>
    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
   throw new InvalidOperationException($"Cannot subtract {Currency} and {other.Currency}");

        return new Money(Amount - other.Amount, Currency);
    }

  /// <summary>
    /// Multiply money by a factor
    /// </summary>
    public Money Multiply(decimal factor)
        => new(Amount * factor, Currency);

    /// <summary>
    /// Check if this money value is greater than another
    /// </summary>
    public bool IsGreaterThan(Money other)
    {
        if (Currency != other.Currency)
    throw new InvalidOperationException($"Cannot compare {Currency} and {other.Currency}");

        return Amount > other.Amount;
    }

    /// <summary>
  /// Check if this money value is less than another
    /// </summary>
    public bool IsLessThan(Money other)
    {
        if (Currency != other.Currency)
  throw new InvalidOperationException($"Cannot compare {Currency} and {other.Currency}");

   return Amount < other.Amount;
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}
```

**Giải thích:**

**1. Record Type:**
- `record` provides structural equality
- Immutable by default
- `sealed` prevents inheritance

**2. Private Constructor:**
- Only factory methods (`Of`, `Zero`) can create instances
- Ensures validation always happens

**3. Business Operations:**
- `Add/Subtract`: Validates same currency
- `Multiply`: For quantity calculations
- `IsGreaterThan/IsLessThan`: For comparisons

**4. Why Decimal:**
- `decimal` is precise for money (vs `double`)
- No floating-point errors
- Standard for financial calculations

**Real-world usage:**
```csharp
// Creating money
var price = Money.Of(999.99m, "USD");
var vndPrice = Money.Of(23500000m, "VND");

// Business operations
var totalPrice = price.Multiply(3);  // 3 items
var discount = Money.Of(100m, "USD");
var finalPrice = price.Subtract(discount);

// ❌ This will throw - different currencies
var invalid = price.Add(vndPrice);  // Exception!
```

---

### Bước 3.2: SKU Value Object

**Làm gì:** Tạo type-safe SKU (Stock Keeping Unit) value object.

**Tại sao:**
- **Type-safe:** Cannot mix SKU with regular strings
- **Validation:** Format validation (uppercase, alphanumeric)
- **Uniqueness:** Ensures SKU format consistency

**File:** `src/Core/Domain/Catalog/ValueObjects/SKU.cs`

```csharp
namespace ECO.WebApi.Domain.Catalog.ValueObjects;

/// <summary>
/// Value Object representing Stock Keeping Unit
/// Immutable identifier for products/variants
/// </summary>
public sealed record SKU
{
    /// <summary>
  /// The SKU code value (e.g., "PROD-001-RED-L")
    /// </summary>
    public string Value { get; init; }

    private SKU(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
    throw new ArgumentException("SKU cannot be empty", nameof(value));

        if (value.Length > 50)
     throw new ArgumentException("SKU cannot exceed 50 characters", nameof(value));

    // SKU should be alphanumeric with hyphens (customizable per business rules)
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Z0-9\-]+$"))
            throw new ArgumentException("SKU must contain only uppercase letters, numbers, and hyphens", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Factory method to create SKU instance
    /// </summary>
    public static SKU Of(string value) => new(value.ToUpperInvariant());

    /// <summary>
    /// Generate a new random SKU (for testing/default purposes)
    /// </summary>
  public static SKU Generate(string prefix = "PROD")
  {
    var timestamp = DateTime.UtcNow.Ticks;
      return new SKU($"{prefix}-{timestamp}");
    }

    public override string ToString() => Value;

    /// <summary>
    /// Implicit conversion to string (for easier usage)
    /// </summary>
    public static implicit operator string(SKU sku) => sku.Value;
}
```

**Giải thích:**

**1. Validation Rules:**
- Uppercase only (consistency)
- Alphanumeric + hyphens
- Max 50 characters
- Cannot be empty

**2. Factory Methods:**
- `Of()`: Create from existing SKU string
- `Generate()`: Generate random SKU (useful for testing)

**3. Implicit Conversion:**
- Can use SKU where string is expected
- `string sql = $"WHERE SKU = '{sku}'";`  // Works!

**Real-world usage:**
```csharp
// Creating SKU
var sku = SKU.Of("IPHONE15-BLK-128GB");
var generatedSku = SKU.Generate("PROD");

// Type-safe comparison
if (product.SKU == anotherProduct.SKU)  // Type-safe!

// ❌ Validation errors
var invalid1 = SKU.Of("lowercase");  // Exception - must be uppercase
var invalid2 = SKU.Of("PROD@123");   // Exception - invalid character @
```

---

### Bước 3.3: ProductImage Value Object

**Làm gì:** Tạo value object cho product images với URL validation.

**File:** `src/Core/Domain/Catalog/ValueObjects/ProductImage.cs`

```csharp
namespace ECO.WebApi.Domain.Catalog.ValueObjects;

/// <summary>
/// Value Object representing a product image
/// </summary>
public sealed record ProductImage
{
    /// <summary>
    /// Image URL
    /// </summary>
 public string Url { get; init; }

    /// <summary>
    /// Alt text for accessibility
    /// </summary>
    public string? AltText { get; init; }

    /// <summary>
    /// Display order
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Is this the main/primary image
    /// </summary>
    public bool IsPrimary { get; init; }

    private ProductImage(string url, string? altText, int order, bool isPrimary)
    {
        if (string.IsNullOrWhiteSpace(url))
     throw new ArgumentException("Image URL is required", nameof(url));

        // Basic URL validation
        if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
   throw new ArgumentException("Invalid URL format", nameof(url));

        Url = url;
 AltText = altText;
      Order = order;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// Factory method
    /// </summary>
    public static ProductImage Of(string url, string? altText = null, int order = 0, bool isPrimary = false)
        => new(url, altText, order, isPrimary);
}
```

**Real-world usage:**
```csharp
// Creating product images
var mainImage = ProductImage.Of(
 url: "/images/iphone15-black.jpg",
    altText: "iPhone 15 Black Front View",
    order: 0,
    isPrimary: true
);

var galleryImage = ProductImage.Of(
    url: "/images/iphone15-black-back.jpg",
    altText: "iPhone 15 Black Back View",
    order: 1,
    isPrimary: false
);

// In Product entity
product.SetImages(new[] { mainImage, galleryImage });
```

---

## 4. Domain Events

### Bước 4.1: Product Domain Events

**Làm gì:** Tạo business-specific domain events cho Product aggregate.

**Tại sao:**
- **Business Semantics:** Events reflect business language
- **Side Effects:** Decouple side effects (email, cache, etc.)
- **Audit Trail:** Track important business changes
- **Integration:** Other bounded contexts can subscribe

**File:** `src/Core/Domain/Catalog/Events/ProductEvents.cs`

```csharp
using ECO.WebApi.Domain.Common.Contracts;

namespace ECO.WebApi.Domain.Catalog.Events;

/// <summary>
/// Product price was changed - important business event
/// </summary>
public sealed class ProductPriceChangedEvent : DomainEvent
{
    public Guid ProductId { get; }
    public decimal OldPrice { get; }
    public decimal NewPrice { get; }
    public string Currency { get; }

    internal ProductPriceChangedEvent(Guid productId, decimal oldPrice, decimal newPrice, string currency)
    {
        ProductId = productId;
        OldPrice = oldPrice;
        NewPrice = newPrice;
        Currency = currency;
    }

    public static ProductPriceChangedEvent Create(Guid productId, decimal oldPrice, decimal newPrice, string currency)
    => new(productId, oldPrice, newPrice, currency);
}

/// <summary>
/// Product stock is running low - trigger reorder
/// </summary>
public sealed class ProductLowStockEvent : DomainEvent
{
    public Guid ProductId { get; }
    public string ProductName { get; }
    public int CurrentStock { get; }
    public int LowStockThreshold { get; }

    internal ProductLowStockEvent(Guid productId, string productName, int currentStock, int lowStockThreshold)
    {
        ProductId = productId;
        ProductName = productName;
        CurrentStock = currentStock;
        LowStockThreshold = lowStockThreshold;
    }

    public static ProductLowStockEvent Create(Guid productId, string productName, int currentStock, int lowStockThreshold)
        => new(productId, productName, currentStock, lowStockThreshold);
}

/// <summary>
/// Product is out of stock - notify customers
/// </summary>
public sealed class ProductOutOfStockEvent : DomainEvent
{
    public Guid ProductId { get; }
    public string ProductName { get; }

    internal ProductOutOfStockEvent(Guid productId, string productName)
 {
        ProductId = productId;
        ProductName = productName;
    }

    public static ProductOutOfStockEvent Create(Guid productId, string productName)
  => new(productId, productName);
}

/// <summary>
/// Product was published (made visible to customers)
/// </summary>
public sealed class ProductPublishedEvent : DomainEvent
{
    public Guid ProductId { get; }
    public string ProductName { get; }

internal ProductPublishedEvent(Guid productId, string productName)
    {
        ProductId = productId;
  ProductName = productName;
    }

    public static ProductPublishedEvent Create(Guid productId, string productName)
   => new(productId, productName);
}
```

**Event Handlers Examples:**

```csharp
// Email notification when stock is low
public class ProductLowStockEventHandler : INotificationHandler<ProductLowStockEvent>
{
    private readonly IMailService _mailService;

    public async Task Handle(ProductLowStockEvent notification, CancellationToken ct)
    {
        await _mailService.SendAsync(
      to: "inventory@company.com",
          subject: $"Low Stock Alert: {notification.ProductName}",
            body: $"Current stock: {notification.CurrentStock}, Threshold: {notification.LowStockThreshold}"
        );
 }
}

// Cache invalidation when price changes
public class ProductPriceChangedCacheHandler : INotificationHandler<ProductPriceChangedEvent>
{
    private readonly ICacheService _cacheService;

    public async Task Handle(ProductPriceChangedEvent notification, CancellationToken ct)
    {
        await _cacheService.RemoveAsync($"product:{notification.ProductId}");
        await _cacheService.RemoveAsync("products:all");
    }
}
```

---

## 5. Product Aggregate Root

### Bước 5.1: Product Entity - Rich Domain Model

**Làm gì:** Tạo Product aggregate root với rich domain logic, value objects, và domain events.

**File:** `src/Core/Domain/Catalog/Product.cs`

```csharp
using ECO.WebApi.Domain.Catalog.Events;
using ECO.WebApi.Domain.Catalog.ValueObjects;
using ECO.WebApi.Domain.Common.Contracts;

namespace ECO.WebApi.Domain.Catalog;

/// <summary>
/// Product Aggregate Root
/// Rich domain model with business logic encapsulation
/// </summary>
public sealed class Product : AuditableEntity, IAggregateRoot
{
    // ==================== Value Objects & Properties ====================
    
    /// <summary>
    /// Product name (required)
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
 /// Product SKU - Stock Keeping Unit (unique identifier)
    /// </summary>
    public SKU SKU { get; private set; } = default!;

    /// <summary>
    /// Product description
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Product price with currency
    /// </summary>
    public Money Price { get; private set; } = default!;

    /// <summary>
    /// Compare-at price (original price for discount display)
    /// </summary>
    public Money? CompareAtPrice { get; private set; }

    /// <summary>
    /// Cost per unit (for profit calculation)
    /// </summary>
    public Money? Cost { get; private set; }

/// <summary>
    /// Current stock quantity
    /// </summary>
    public int Stock { get; private set; }

    /// <summary>
    /// Low stock threshold - trigger alert when stock falls below this
    /// </summary>
    public int? LowStockThreshold { get; private set; }

    /// <summary>
    /// Is product published (visible to customers)
    /// </summary>
    public bool IsPublished { get; private set; }

    /// <summary>
    /// Category ID
    /// </summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Brand name
    /// </summary>
    public string? Brand { get; private set; }

    /// <summary>
    /// Product images
    /// </summary>
    private readonly List<ProductImage> _images = new();
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    // Navigation properties
    public Category Category { get; private set; } = default!;

    // ==================== Constructor ====================
    
    // EF Core constructor
    private Product() { }

    // ==================== Factory Methods ====================

    /// <summary>
    /// Create a new product
    /// </summary>
    public static Product Create(
string name,
        SKU sku,
        Money price,
        Guid categoryId,
        int stock = 0,
        string? description = null,
string? brand = null)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
      throw new ArgumentException("Product name is required", nameof(name));

    if (name.Length > 200)
      throw new ArgumentException("Product name cannot exceed 200 characters", nameof(name));

        if (stock < 0)
       throw new ArgumentException("Stock cannot be negative", nameof(stock));

      // Create product
        var product = new Product
        {
   Name = name,
            SKU = sku,
            Description = description,
            Price = price,
            CategoryId = categoryId,
   Stock = stock,
            Brand = brand,
 IsPublished = false,
   LowStockThreshold = 10  // Default threshold
        };

    // ⚠️ EntityCreatedEvent will be auto-added by EventAddingRepositoryDecorator (BUILD_11)
        // No need to add manually

        return product;
}

    // ==================== Business Logic Methods ====================

  /// <summary>
 /// Update product information
    /// </summary>
    public void Update(
string name,
     Money price,
   Guid categoryId,
        string? description = null,
        string? brand = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required", nameof(name));

      Name = name;
        Description = description;
    CategoryId = categoryId;
        Brand = brand;

      // Price change triggers event
      if (!Price.Equals(price))
     {
     UpdatePrice(price);
        }

        // ⚠️ EntityUpdatedEvent will be auto-added by decorator
    }

    /// <summary>
    /// Update product price
    /// Business rule: Price must be positive
    /// </summary>
 public void UpdatePrice(Money newPrice)
    {
        if (newPrice.Amount <= 0)
   throw new ArgumentException("Price must be positive", nameof(newPrice));

   // Raise price changed event
   DomainEvents.Add(ProductPriceChangedEvent.Create(
         Id,
            Price.Amount,
        newPrice.Amount,
   newPrice.Currency
        ));

        Price = newPrice;
    }

    /// <summary>
    /// Update pricing (price + compare-at price + cost)
    /// Business rule: CompareAtPrice must be greater than Price
    /// </summary>
    public void UpdatePricing(Money price, Money? compareAtPrice = null, Money? cost = null)
    {
    if (price.Amount <= 0)
         throw new ArgumentException("Price must be positive", nameof(price));

        if (compareAtPrice != null && !compareAtPrice.IsGreaterThan(price))
         throw new ArgumentException("Compare-at price must be greater than price", nameof(compareAtPrice));

        // Raise price changed event if price actually changed
        if (!Price.Equals(price))
     {
   DomainEvents.Add(ProductPriceChangedEvent.Create(
   Id,
      Price.Amount,
      price.Amount,
     price.Currency
            ));
   }

     Price = price;
   CompareAtPrice = compareAtPrice;
    Cost = cost;
    }

    /// <summary>
    /// Decrease stock (when selling)
    /// Business rules:
    /// - Cannot sell more than available stock
    /// - Triggers LowStockEvent when stock falls below threshold
    /// - Triggers OutOfStockEvent when stock reaches zero
    /// </summary>
    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
     throw new ArgumentException("Quantity must be positive", nameof(quantity));

        if (Stock < quantity)
  throw new InvalidOperationException($"Insufficient stock. Available: {Stock}, Requested: {quantity}");

        var oldStock = Stock;
Stock -= quantity;

      // Check for low stock
if (LowStockThreshold.HasValue && 
    Stock <= LowStockThreshold.Value && 
            oldStock > LowStockThreshold.Value)
        {
 DomainEvents.Add(ProductLowStockEvent.Create(
            Id,
  Name,
      Stock,
       LowStockThreshold.Value
         ));
      }

        // Check for out of stock
        if (Stock == 0 && oldStock > 0)
        {
 DomainEvents.Add(ProductOutOfStockEvent.Create(Id, Name));
        }
    }

    /// <summary>
    /// Increase stock (when restocking)
    /// </summary>
    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
       throw new ArgumentException("Quantity must be positive", nameof(quantity));

        Stock += quantity;
    }

    /// <summary>
    /// Set low stock threshold
    /// </summary>
    public void SetLowStockThreshold(int threshold)
    {
        if (threshold < 0)
            throw new ArgumentException("Threshold cannot be negative", nameof(threshold));

        LowStockThreshold = threshold;
    }

    /// <summary>
    /// Publish product (make visible to customers)
    /// </summary>
    public void Publish()
    {
     if (IsPublished)
 return;  // Already published

   IsPublished = true;
  DomainEvents.Add(ProductPublishedEvent.Create(Id, Name));
    }

    /// <summary>
    /// Unpublish product (hide from customers)
    /// </summary>
    public void Unpublish()
    {
        IsPublished = false;
    }

    /// <summary>
    /// Set product images
    /// </summary>
    public void SetImages(IEnumerable<ProductImage> images)
    {
 _images.Clear();
        _images.AddRange(images);
    }

    /// <summary>
    /// Add a single image
    /// </summary>
    public void AddImage(ProductImage image)
    {
        _images.Add(image);
    }

    // ==================== Calculated Properties ====================

    /// <summary>
    /// Check if product is in stock
    /// </summary>
    public bool IsInStock() => Stock > 0;

    /// <summary>
    /// Check if product is low on stock
    /// </summary>
    public bool IsLowStock() => LowStockThreshold.HasValue && Stock <= LowStockThreshold.Value && Stock > 0;

    /// <summary>
    /// Get discount percentage (if CompareAtPrice is set)
    /// </summary>
    public decimal? GetDiscountPercentage()
 {
        if (CompareAtPrice == null || CompareAtPrice.Amount <= 0)
            return null;

        return Math.Round((CompareAtPrice.Amount - Price.Amount) / CompareAtPrice.Amount * 100, 2);
    }

    /// <summary>
    /// Get profit margin percentage
    /// </summary>
    public decimal? GetProfitMargin()
    {
if (Cost == null || Cost.Amount == 0)
   return null;

        return Math.Round((Price.Amount - Cost.Amount) / Price.Amount * 100, 2);
    }
}
```

**Giải thích chi tiết:**

**1. Value Objects Usage:**
- `Money Price` instead of `decimal Price`
- `SKU SKU` instead of `string SKU`
- `ProductImage` collection instead of `string ImageUrls`

**2. Encapsulation:**
- All setters are `private`
- Business logic through methods (`UpdatePrice`, `DecreaseStock`)
- Validation in domain methods

**3. Domain Events:**
- `ProductPriceChangedEvent` - When price changes
- `ProductLowStockEvent` - When stock falls below threshold
- `ProductOutOfStockEvent` - When stock reaches zero
- `ProductPublishedEvent` - When product is published

**4. Business Rules:**
- Price must be positive
- CompareAtPrice > Price
- Cannot sell more than available stock
- Automatic low stock/out of stock detection

**5. Calculated Properties:**
- `IsInStock()` - Check availability
- `IsLowStock()` - Check if needs reorder
- `GetDiscountPercentage()` - Calculate discount %
- `GetProfitMargin()` - Calculate profit %

---

## 6. Category Aggregate Root

### Bước 6.1: Category Entity - Hierarchical Structure

**File:** `src/Core/Domain/Catalog/Category.cs`

```csharp
using ECO.WebApi.Domain.Common.Contracts;

namespace ECO.WebApi.Domain.Catalog;

/// <summary>
/// Category Aggregate Root
/// Supports hierarchical structure (unlimited levels)
/// </summary>
public sealed class Category : AuditableEntity, IAggregateRoot
{
    /// <summary>
    /// Category name (required)
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Category code (unique identifier, URL-friendly)
    /// </summary>
    public string Code { get; private set; } = default!;

    /// <summary>
 /// Category description
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Parent category ID (null for root categories)
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Is category active (visible)
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Category image URL
    /// </summary>
    public string? ImageUrl { get; private set; }

  // Navigation properties
    public Category? Parent { get; private set; }
    public ICollection<Category> Children { get; private set; } = new List<Category>();
    public ICollection<Product> Products { get; private set; } = new List<Product>();

    // EF Core constructor
    private Category() { }

    // ==================== Factory Methods ====================

    /// <summary>
    /// Create a new category
    /// </summary>
    public static Category Create(
        string name,
    string code,
        string? description = null,
        Guid? parentId = null,
        int displayOrder = 0,
        string? imageUrl = null)
    {
   // Validation
    if (string.IsNullOrWhiteSpace(name))
 throw new ArgumentException("Category name is required", nameof(name));

      if (string.IsNullOrWhiteSpace(code))
    throw new ArgumentException("Category code is required", nameof(code));

  // Create category
        var category = new Category
        {
            Name = name,
     Code = code,
Description = description,
   ParentId = parentId,
       DisplayOrder = displayOrder,
       ImageUrl = imageUrl,
      IsActive = true
        };

    // ⚠️ EntityCreatedEvent auto-added by decorator

        return category;
    }

    // ==================== Business Logic Methods ====================

  /// <summary>
    /// Update category information
    /// </summary>
    public void Update(
        string name,
        string? description = null,
        Guid? parentId = null,
        int displayOrder = 0,
 string? imageUrl = null)
    {
    if (string.IsNullOrWhiteSpace(name))
          throw new ArgumentException("Category name is required", nameof(name));

        // Prevent circular reference
        if (parentId == Id)
          throw new InvalidOperationException("Category cannot be its own parent");

     Name = name;
     Description = description;
ParentId = parentId;
        DisplayOrder = displayOrder;
        ImageUrl = imageUrl;

    // ⚠️ EntityUpdatedEvent auto-added by decorator
    }

    /// <summary>
    /// Activate category
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivate category
    /// </summary>
    public void Deactivate()
  {
        IsActive = false;
    }

    /// <summary>
    /// Check if this is a root category
    /// </summary>
    public bool IsRoot() => ParentId == null;

    /// <summary>
    /// Check if this category has children
    /// </summary>
public bool HasChildren() => Children.Any();
}
```

---

## 7. Domain Enums

### Bước 7.1: Type-Safe Enums

**File:** `src/Core/Domain/Catalog/ProductStatus.cs`

```csharp
namespace ECO.WebApi.Domain.Catalog;

/// <summary>
/// Product availability status
/// </summary>
public enum ProductStatus
{
    /// <summary>
    /// Product in stock and available
    /// </summary>
    InStock = 1,

    /// <summary>
    /// Product out of stock
    /// </summary>
    OutOfStock = 2,

    /// <summary>
    /// Product discontinued (no longer available)
    /// </summary>
    Discontinued = 3
}
```

---

## 8. Summary

### ✅ Domain Layer Complete (Theoretical Design):

**Value Objects:**
- ✅ Money (with currency, operations, validation)
- ✅ SKU (type-safe product identifier)
- ✅ ProductImage (with alt text, ordering)

**Domain Events:**
- ✅ ProductPriceChangedEvent
- ✅ ProductLowStockEvent
- ✅ ProductOutOfStockEvent
- ✅ ProductPublishedEvent

**Aggregate Roots:**
- ✅ Product (rich domain model với business logic)
- ✅ Category (hierarchical structure)

**Enums:**
- ✅ ProductStatus (type-safe status)

### 📊 Comparison: Workspace vs Theoretical Design

| Aspect | Workspace Code | Theoretical Design |
|--------|----------------|-------------------|
| Price Type | `double` | `Money` (Value Object) |
| SKU Type | `string` | `SKU` (Value Object) |
| Encapsulation | Public setters | Private setters + methods |
| Validation | Minimal | Comprehensive business rules |
| Events | Generic lifecycle | Business-specific events |
| Currency | No support | Multi-currency support |
| Type Safety | Primitive types | Value Objects |
| Testability | Medium | High (pure domain logic) |

### 📌 Key Benefits:

**1. Type Safety:**
```csharp
// ❌ Workspace: Can mix up price with any number
product.Price = someRandomNumber;

// ✅ Theoretical: Type-safe, cannot mix up
product.UpdatePrice(Money.Of(999.99m, "USD"));
```

**2. Business Rules Enforcement:**
```csharp
// ❌ Workspace: Can set invalid state
product.Stock = -10;  // Negative stock!

// ✅ Theoretical: Business rules enforced
product.DecreaseStock(5);  // Validates stock availability
```

**3. Rich Domain Events:**
```csharp
// ❌ Workspace: Silent updates
product.Stock = 5;  // No one knows stock changed

// ✅ Theoretical: Event-driven
product.DecreaseStock(5);  
// → ProductLowStockEvent raised
// → Email notification sent
// → Cache invalidated
```

### 📁 File Structure:

```
src/Core/Domain/Catalog/
├── ValueObjects/
│   ├── Money.cs
│   ├── SKU.cs
│   └── ProductImage.cs
├── Events/
│   └── ProductEvents.cs
├── Product.cs (Aggregate Root)
├── Category.cs (Aggregate Root)
└── ProductStatus.cs (Enum)
```

---

## 9. Next Steps

**Tiếp theo:** [BUILD_28_Application_Layer.md](BUILD_28_Application_Layer.md)

Trong doc tiếp theo, chúng ta sẽ:
- ✅ CQRS với MediatR (Commands, Queries)
- ✅ DTOs (Data Transfer Objects)
- ✅ FluentValidation rules
- ✅ Mapster configuration
- ✅ Specifications (query patterns)

---

**Quay lại:** [BUILD_28 - Main](BUILD_28_Catalog_Module.md)

---

**Document Version:** 1.0 (Theoretical Design - Research-Based)  
**Last Updated:** 2026-01-30  
**Note:** This is **theoretical design**, not workspace code. Represents DDD best practices for catalog domain.
