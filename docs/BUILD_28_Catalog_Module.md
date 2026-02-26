# BUILD_28: Catalog Module - Products và Categories Management

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 27 (PDF Export Service) đã hoàn thành

Tài liệu này là **overview** cho Catalog Module - Module quản lý sản phẩm và danh mục theo **theoretical design tốt nhất** (research-based DDD patterns).

---

## 1. Overview

**Làm gì:** Xây dựng Catalog Module với Products và Categories management theo DDD best practices.

**Tại sao theoretical design:**
- ✅ **DDD Best Practices:** Rich domain models, Value Objects, Aggregate Roots
- ✅ **CQRS Pattern:** Tách biệt Commands (write) và Queries (read)
- ✅ **Type-Safe:** Strongly-typed value objects (Money, SKU)
- ✅ **Domain Events:** Business-driven events (ProductPriceChanged, ProductLowStock)
- ✅ **Clean Architecture:** Domain → Application → Infrastructure → Controllers

**Research sources:**
- Domain-Driven Design (Eric Evans)
- Implementing Domain-Driven Design (Vaughn Vernon)
- Microsoft .NET Microservices Architecture
- E-commerce domain patterns (Shopify, Magento)

**Trong BUILD_28:**
- ✅ Domain Layer (Value Objects, Aggregate Roots, Domain Events)
- ✅ Application Layer (CQRS, DTOs, Validators, Specifications)
- ✅ Infrastructure Layer (EF Core configurations, Migrations)
- ✅ Controllers (REST APIs with Swagger)

**⚠️ Lưu ý quan trọng:**
BUILD_28 được **chia thành 3 sub-docs** do độ phức tạp:

1. **[BUILD_28_Domain_Layer.md](BUILD_28_Domain_Layer.md)** - Domain layer with DDD patterns
2. **[BUILD_28_Application_Layer.md](BUILD_28_Application_Layer.md)** - CQRS with MediatR
3. **[BUILD_28_Infrastructure_Controllers.md](BUILD_28_Infrastructure_Controllers.md)** - EF Core & REST APIs

---

## 2. Real-World Examples

### 2.1: Create Product with Value Objects

```csharp
var command = new CreateProductCommand
{
    Name = "MacBook Air M2",
    SKU = "MBA-M2-256-SG",
    Description = "13-inch laptop with M2 chip",
  Price = 1199.99m,
    Currency = "USD",
    CompareAtPrice = 1299.99m,
    Stock = 50,
  LowStockThreshold = 10,
    CategoryId = electronicsCategory.Id,
    Brand = "Apple",
    IsPublished = true,
    Images = new List<CreateProductImageDto>
    {
        new() 
      { 
     Url = "/images/macbook-air-m2.jpg",
       AltText = "MacBook Air M2",
            Order = 0,
            IsPrimary = true
        }
    }
};

var productId = await _mediator.Send(command);
```

**Domain objects created:**
```csharp
// Value Objects (type-safe)
SKU sku = SKU.Of("MBA-M2-256-SG");
Money price = Money.Of(1199.99m, "USD");
Money compareAtPrice = Money.Of(1299.99m, "USD");
ProductImage image = ProductImage.Of("/images/macbook-air-m2.jpg", "MacBook Air M2", 0, true);

// Aggregate Root (rich domain model)
Product product = Product.Create(
    name: "MacBook Air M2",
    sku: sku,
    price: price,
    categoryId: electronicsCategory.Id,
    stock: 50
);

// Business logic methods
product.UpdatePricing(price, compareAtPrice, null);
product.SetLowStockThreshold(10);
product.Publish();
product.SetImages(new[] { image });
```

---

### 2.2: Domain Events in Action

```csharp
// When decreasing stock
product.DecreaseStock(5);

// Domain events raised automatically:
// 1. If stock falls below threshold → ProductLowStockEvent
// 2. If stock reaches zero → ProductOutOfStockEvent

// Event handlers execute:
// - ProductLowStockEventHandler → Send email to inventory manager
// - ProductPriceChangedCacheHandler → Invalidate product cache
```

---

### 2.3: Search Products with Specifications

```csharp
var query = new SearchProductsQuery
{
    Keyword = "macbook",
    CategoryId = electronicsCategory.Id,
    MinPrice = 500,
    MaxPrice = 2000,
    IsPublished = true,
    InStockOnly = true,
    PageNumber = 1,
    PageSize = 20
};

var result = await _mediator.Send(query);

// Returns: PaginatedResult<ProductListDto>
// - Optimized query (only selects needed fields)
// - DTOs instead of full domain entities
// - Includes total count for pagination
```

---

## 3. Architecture Overview

### 3.1: Domain Model

**Value Objects:**
- `Money` - Monetary value with currency (immutable, type-safe)
- `SKU` - Stock Keeping Unit (validated format)
- `ProductImage` - Image with alt text and ordering

**Aggregate Roots:**
- `Product` - Rich domain model with business logic
- `Category` - Hierarchical structure with parent/children

**Domain Events:**
- `ProductPriceChangedEvent` - Price updated
- `ProductLowStockEvent` - Stock below threshold
- `ProductOutOfStockEvent` - Stock depleted
- `ProductPublishedEvent` - Product made visible

**Enums:**
- `ProductStatus` - InStock, OutOfStock, Discontinued

👉 **Chi tiết:** [BUILD_28_Domain_Layer.md](BUILD_28_Domain_Layer.md)

---

### 3.2: Application Layer (CQRS)

**Commands (Write):**
- `CreateProductCommand` - Create new product
- `UpdateProductCommand` - Update existing product
- `DeleteProductCommand` - Delete product
- `CreateCategoryCommand` - Create new category

**Queries (Read):**
- `GetProductQuery` - Get single product with details
- `SearchProductsQuery` - Search with filters & pagination
- `SearchCategoriesQuery` - Get categories hierarchy

**DTOs:**
- `ProductDto` - Detailed product info
- `ProductListDto` - Simplified for lists
- `CategoryDto` - Category with parent info

**Validators:**
- `CreateProductCommandValidator` - FluentValidation rules
- `UpdateProductCommandValidator`
- `CreateCategoryCommandValidator`

**Specifications:**
- `ProductByIdSpec` - Get product by ID
- `ProductSearchSpec` - Complex search with filters
- `CategorySearchSpec` - Category filtering

👉 **Chi tiết:** [BUILD_28_Application_Layer.md](BUILD_28_Application_Layer.md)

---

### 3.3: Infrastructure & Controllers

**EF Core Configurations:**
- `ProductConfiguration` - Value object mappings
- `CategoryConfiguration` - Hierarchical structure

**Migrations:**
- `AddCatalogModule` - Tables, indexes, relationships

**REST APIs:**
```
POST   /api/catalog/products/search    - Search products
GET    /api/catalog/products/{id}- Get product by ID
POST   /api/catalog/products   - Create product
PUT    /api/catalog/products/{id}      - Update product
DELETE /api/catalog/products/{id}      - Delete product

POST   /api/catalog/categories/search  - Search categories
GET    /api/catalog/categories/{id}    - Get category
POST   /api/catalog/categories  - Create category
PUT    /api/catalog/categories/{id}    - Update category
DELETE /api/catalog/categories/{id}    - Delete category
```

👉 **Chi tiết:** [BUILD_28_Infrastructure_Controllers.md](BUILD_28_Infrastructure_Controllers.md)

---

## 4. Key Features

### 4.1: Rich Domain Model (DDD)

**Encapsulation:**
```csharp
// ❌ Anemic model (bad)
public class Product
{
    public decimal Price { get; set; }  // Anyone can set
    public int Stock { get; set; }      // No validation
}

// ✅ Rich model (good)
public class Product
{
    public Money Price { get; private set; }  // Private setter
    public int Stock { get; private set; }
    
    // Business logic methods
    public void UpdatePrice(Money newPrice) 
    {
        if (newPrice.Amount <= 0) throw new ArgumentException();
        Price = newPrice;
        DomainEvents.Add(ProductPriceChangedEvent.Create(...));
    }
    
    public void DecreaseStock(int quantity)
    {
        if (Stock < quantity) throw new InvalidOperationException();
        Stock -= quantity;
        
        if (Stock <= LowStockThreshold)
       DomainEvents.Add(ProductLowStockEvent.Create(...));
    }
}
```

---

### 4.2: Type-Safe Value Objects

**Money Example:**
```csharp
// ❌ Primitive obsession (bad)
decimal price = 999.99m;  // No currency!
decimal vndPrice = 23500000m;
var total = price + vndPrice;  // Wrong! Different currencies

// ✅ Value Object (good)
Money usdPrice = Money.Of(999.99m, "USD");
Money vndPrice = Money.Of(23500000m, "VND");
var total = usdPrice.Add(vndPrice);  // Exception! Cannot add different currencies
```

**SKU Example:**
```csharp
// ❌ String (bad)
string sku = "prod-123";  // Can be anything!
string anotherSku = "PROD-123";  // Same SKU, different format

// ✅ SKU Value Object (good)
SKU sku = SKU.Of("prod-123");  // Automatically uppercase: "PROD-123"
SKU invalid = SKU.Of("prod@123");  // Exception! Invalid format
```

---

### 4.3: Domain Events

**Automatic Events (BUILD_11 decorator):**
- `EntityCreatedEvent<Product>` - Auto-added when product created
- `EntityUpdatedEvent<Product>` - Auto-added when product updated
- `EntityDeletedEvent<Product>` - Auto-added when product deleted

**Business Events (manual):**
- `ProductPriceChangedEvent` - When price changes
- `ProductLowStockEvent` - When stock <= threshold
- `ProductOutOfStockEvent` - When stock == 0
- `ProductPublishedEvent` - When product published

**Event Handlers:**
```csharp
// Email notification
public class ProductLowStockEventHandler : INotificationHandler<ProductLowStockEvent>
{
    public async Task Handle(ProductLowStockEvent notification, CancellationToken ct)
    {
      await _mailService.SendAsync(
          to: "inventory@company.com",
            subject: $"Low Stock: {notification.ProductName}",
     body: $"Current: {notification.CurrentStock}, Threshold: {notification.LowStockThreshold}"
        );
  }
}

// Cache invalidation
public class ProductUpdatedCacheHandler : INotificationHandler<EntityUpdatedEvent<Product>>
{
    public async Task Handle(EntityUpdatedEvent<Product> notification, CancellationToken ct)
    {
        await _cacheService.RemoveAsync($"product:{notification.Entity.Id}");
    }
}
```

---

### 4.4: CQRS Pattern

**Separation:**
```csharp
// Commands - Use domain entities
await _mediator.Send(new CreateProductCommand { ... });
// → Domain: Product.Create() → Business rules → Events → Save

// Queries - Use DTOs directly
var products = await _mediator.Send(new SearchProductsQuery { ... });
// → Specification → Optimized query → Map to DTOs → Return
```

**Benefits:**
- Commands enforce business rules
- Queries optimized for read performance
- Can scale read/write independently

---

## 5. Comparison: Workspace vs Theoretical Design

| Aspect | Workspace Code | BUILD_28 Theoretical |
|--------|----------------|---------------------|
| Price Type | `double` | `Money` (Value Object) |
| SKU Type | `string` | `SKU` (Value Object) |
| Images | `string? ImageUrls` | `List<ProductImage>` |
| Encapsulation | Public setters | Private setters + methods |
| Validation | Minimal | Comprehensive business rules |
| Events | Generic lifecycle only | Business-specific events |
| Currency | No support | Multi-currency with validation |
| Type Safety | Primitive types | Strongly-typed value objects |
| CQRS | No separation | Separate Commands/Queries |
| Testability | Medium | High (pure domain logic) |

---

## 6. File Structure

```
ECO.WebApi/
├── Domain/Catalog/
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   ├── SKU.cs
│   │   └── ProductImage.cs
│   ├── Events/
│   │   └── ProductEvents.cs
│   ├── Product.cs (Aggregate Root)
│   ├── Category.cs (Aggregate Root)
│   └── ProductStatus.cs (Enum)
│
├── Application/Catalog/
│   ├── Products/
│   │   ├── ProductDto.cs
│   │   ├── CreateProductCommand.cs
│   │   ├── UpdateProductCommand.cs
│ │   ├── DeleteProductCommand.cs
│   │   ├── GetProductQuery.cs
│   │   ├── SearchProductsQuery.cs
│   │   └── ProductSpecifications.cs
│   └── Categories/
│       ├── CategoryDto.cs
│       ├── CreateCategoryCommand.cs
│       ├── SearchCategoriesQuery.cs
│       └── CategorySpecifications.cs
│
├── Infrastructure/Persistence/Configurations/
│   ├── ProductConfiguration.cs
│   └── CategoryConfiguration.cs
│
└── Host/Controllers/Catalog/
    ├── ProductsController.cs
    └── CategoriesController.cs
```

---

## 7. Summary

### ✅ BUILD_28 Complete (Theoretical Design):

**Domain Layer:**
- ✅ Value Objects (Money, SKU, ProductImage)
- ✅ Rich Domain Models (Product, Category)
- ✅ Domain Events (business-specific)
- ✅ Aggregate Roots with business logic

**Application Layer:**
- ✅ CQRS (Commands + Queries)
- ✅ DTOs (ProductDto, CategoryDto)
- ✅ Validators (FluentValidation)
- ✅ Specifications (complex queries)

**Infrastructure Layer:**
- ✅ EF Core Configurations
- ✅ Value Object Converters
- ✅ Database Migrations

**Controllers:**
- ✅ REST APIs (Products, Categories)
- ✅ Swagger Documentation

### 📊 Benefits của Theoretical Design:

**1. Type Safety:**
- Cannot mix Money with regular decimals
- Cannot use invalid SKU formats
- Compile-time errors prevent bugs

**2. Business Rules Enforcement:**
- Domain methods validate all changes
- Impossible to create invalid state
- Business logic in one place (domain)

**3. Event-Driven:**
- Side effects decoupled
- Easy to add new features
- Audit trail automatic

**4. Testability:**
- Pure domain logic
- Easy to mock dependencies
- Unit tests for business rules

**5. Maintainability:**
- Clear separation of concerns
- Self-documenting code
- Easy to understand intent

---

## 8. Sub-Documentation Index

**Full documentation split into 3 parts:**

### 📄 Part 1: Domain Layer
**File:** [BUILD_28_Domain_Layer.md](BUILD_28_Domain_Layer.md)

**Contents:**
- Analysis of workspace code
- Value Objects (Money, SKU, ProductImage)
- Domain Events (ProductPriceChanged, ProductLowStock, etc.)
- Product Aggregate Root (rich domain model)
- Category Aggregate Root (hierarchical)
- Domain Enums (ProductStatus)

**When to read:** Understanding domain design, DDD patterns, value objects

---

### 📄 Part 2: Application Layer
**File:** [BUILD_28_Application_Layer.md](BUILD_28_Application_Layer.md)

**Contents:**
- DTOs (ProductDto, CategoryDto, ProductListDto)
- Commands (Create, Update, Delete) with validators
- Queries (Get, Search) with pagination
- Specifications (ProductSearchSpec, CategorySearchSpec)
- CQRS pattern explanation

**When to read:** Implementing use cases, CQRS, validation

---

### 📄 Part 3: Infrastructure & Controllers
**File:** [BUILD_28_Infrastructure_Controllers.md](BUILD_28_Infrastructure_Controllers.md)

**Contents:**
- EF Core Configurations (ProductConfiguration, CategoryConfiguration)
- Value Object Converters
- Database Migrations
- REST API Controllers (ProductsController, CategoriesController)
- Swagger examples

**When to read:** Database setup, API implementation, testing

---

## 9. Next Steps

**Tiếp theo:** [BUILD_29 - Notifications](BUILD_29_Notifications.md)

Trong bước tiếp theo, chúng ta sẽ:
- ✅ SignalR hub setup
- ✅ Real-time push notifications
- ✅ Notification entity
- ✅ In-app notification center

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)

---

**Document Version:** 2.0 (Theoretical Design - Complete with 3 Sub-Docs)  
**Last Updated:** 2026-01-30  
**Note:** This is **theoretical design** (research-based DDD), not workspace code documentation.
