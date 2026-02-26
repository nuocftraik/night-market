# BUILD_28: Catalog Module - Infrastructure & Controllers

> 📚 [Quay lại BUILD_28 Main](BUILD_28_Catalog_Module.md)  
> 📋 **Prerequisites:** BUILD_28_Application_Layer.md hoàn thành

Tài liệu này hướng dẫn xây dựng **Infrastructure Layer** và **Controllers** cho Catalog Module.

---

## 1. EF Core Configurations

### Bước 1.1: Product Entity Configuration

**File:** `src/Infrastructure/Persistence/Configurations/ProductConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using ECO.WebApi.Domain.Catalog.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Product entity
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "Catalog");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

  builder.Property(p => p.Description)
       .HasMaxLength(2000);

        builder.Property(p => p.Brand)
 .HasMaxLength(100);

        builder.Property(p => p.IsPublished)
            .IsRequired()
  .HasDefaultValue(false);

    builder.Property(p => p.Stock)
      .IsRequired()
.HasDefaultValue(0);

        builder.Property(p => p.LowStockThreshold)
       .IsRequired(false);

        // ==================== Value Objects Configuration ====================

        // SKU Value Object
        builder.OwnsOne(p => p.SKU, sku =>
  {
   sku.Property(s => s.Value)
        .IsRequired()
     .HasMaxLength(50)
       .HasColumnName("SKU");

   // Unique index
     sku.HasIndex(s => s.Value)
  .IsUnique()
   .HasDatabaseName("IX_Products_SKU");
        });

        // Money Value Object (Price)
  builder.OwnsOne(p => p.Price, price =>
    {
     price.Property(m => m.Amount)
        .IsRequired()
                .HasPrecision(18, 2)
        .HasColumnName("Price");

 price.Property(m => m.Currency)
       .IsRequired()
   .HasMaxLength(3)
 .HasColumnName("Currency")
    .HasDefaultValue("VND");
        });

        // Money Value Object (CompareAtPrice)
   builder.OwnsOne(p => p.CompareAtPrice, comparePrice =>
   {
         comparePrice.Property(m => m.Amount)
   .HasPrecision(18, 2)
         .HasColumnName("CompareAtPrice");

      comparePrice.Property(m => m.Currency)
.HasMaxLength(3)
         .HasColumnName("CompareAtPriceCurrency");
   });

      // Money Value Object (Cost)
        builder.OwnsOne(p => p.Cost, cost =>
        {
  cost.Property(m => m.Amount)
         .HasPrecision(18, 2)
       .HasColumnName("Cost");

    cost.Property(m => m.Currency)
     .HasMaxLength(3)
  .HasColumnName("CostCurrency");
 });

        // ProductImage Collection (owned entity)
builder.OwnsMany(p => p.Images, images =>
        {
      images.ToTable("ProductImages", "Catalog");
       images.WithOwner().HasForeignKey("ProductId");
            images.Property<int>("Id");
            images.HasKey("Id");

       images.Property(img => img.Url)
     .IsRequired()
         .HasMaxLength(500);

        images.Property(img => img.AltText)
     .HasMaxLength(200);

            images.Property(img => img.Order)
    .IsRequired();

       images.Property(img => img.IsPrimary)
  .IsRequired();
        });

   // ==================== Relationships ====================

    // Category relationship
 builder.HasOne(p => p.Category)
     .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
  .OnDelete(DeleteBehavior.Restrict);

// ==================== Indexes ====================

  builder.HasIndex(p => p.Name);
   builder.HasIndex(p => p.CategoryId);
 builder.HasIndex(p => p.IsPublished);
 builder.HasIndex(p => p.CreatedOn);
  }
}
```

**Giải thích:**

**1. Value Objects Mapping:**
- `OwnsOne()` for single value objects (SKU, Money)
- `OwnsMany()` for collections (ProductImage)
- Custom column names to avoid EF Core conventions

**2. Money Precision:**
- `HasPrecision(18, 2)` - 18 digits total, 2 decimal places
- Standard for financial calculations

**3. Indexes:**
- Unique index on SKU
- Indexes on commonly queried fields (Name, CategoryId, IsPublished)

---

### Bước 1.2: Category Entity Configuration

**File:** `src/Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Category entity
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "Catalog");

        // Primary key
   builder.HasKey(c => c.Id);

        // Properties
  builder.Property(c => c.Name)
    .IsRequired()
      .HasMaxLength(100);

     builder.Property(c => c.Code)
       .IsRequired()
   .HasMaxLength(50);

        builder.Property(c => c.Description)
    .HasMaxLength(500);

   builder.Property(c => c.ImageUrl)
   .HasMaxLength(500);

      builder.Property(c => c.DisplayOrder)
    .IsRequired()
      .HasDefaultValue(0);

        builder.Property(c => c.IsActive)
         .IsRequired()
            .HasDefaultValue(true);

   // ==================== Relationships ====================

// Self-referencing relationship (hierarchical)
        builder.HasOne(c => c.Parent)
       .WithMany(c => c.Children)
   .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ==================== Indexes ====================

  builder.HasIndex(c => c.Code)
            .IsUnique()
   .HasDatabaseName("IX_Categories_Code");

  builder.HasIndex(c => c.ParentId);
        builder.HasIndex(c => c.DisplayOrder);
        builder.HasIndex(c => c.IsActive);
    }
}
```

---

## 2. Database Migrations

### Bước 2.1: Create Migration

**Command:**
```bash
# Navigate to Migrators project
cd src/Migrators/Migrators.MSSQL

# Create migration
dotnet ef migrations add AddCatalogModule --startup-project ../../Host/Host

# Update database
dotnet ef database update --startup-project ../../Host/Host
```

**Expected Migration:**
```csharp
public partial class AddCatalogModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
  // Create Categories table
 migrationBuilder.CreateTable(
    name: "Categories",
    schema: "Catalog",
      columns: table => new
{
    Id = table.Column<Guid>(nullable: false),
     Name = table.Column<string>(maxLength: 100, nullable: false),
              Code = table.Column<string>(maxLength: 50, nullable: false),
       Description = table.Column<string>(maxLength: 500, nullable: true),
         ParentId = table.Column<Guid>(nullable: true),
    DisplayOrder = table.Column<int>(nullable: false, defaultValue: 0),
  IsActive = table.Column<bool>(nullable: false, defaultValue: true),
            ImageUrl = table.Column<string>(maxLength: 500, nullable: true),
  CreatedBy = table.Column<Guid>(nullable: false),
         CreatedOn = table.Column<DateTime>(nullable: false),
     LastModifiedBy = table.Column<Guid>(nullable: false),
      LastModifiedOn = table.Column<DateTime>(nullable: true)
     },
   constraints: table =>
            {
     table.PrimaryKey("PK_Categories", x => x.Id);
table.ForeignKey("FK_Categories_Parent", x => x.ParentId, "Categories", "Id");
     });

        // Create Products table
      migrationBuilder.CreateTable(
            name: "Products",
            schema: "Catalog",
    columns: table => new
       {
          Id = table.Column<Guid>(nullable: false),
     Name = table.Column<string>(maxLength: 200, nullable: false),
  SKU = table.Column<string>(maxLength: 50, nullable: false),
    Description = table.Column<string>(maxLength: 2000, nullable: true),
     Price = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
  Currency = table.Column<string>(maxLength: 3, nullable: false, defaultValue: "VND"),
           CompareAtPrice = table.Column<decimal>(precision: 18, scale: 2, nullable: true),
  CompareAtPriceCurrency = table.Column<string>(maxLength: 3, nullable: true),
             Cost = table.Column<decimal>(precision: 18, scale: 2, nullable: true),
       CostCurrency = table.Column<string>(maxLength: 3, nullable: true),
           Stock = table.Column<int>(nullable: false, defaultValue: 0),
     LowStockThreshold = table.Column<int>(nullable: true),
     IsPublished = table.Column<bool>(nullable: false, defaultValue: false),
   CategoryId = table.Column<Guid>(nullable: false),
         Brand = table.Column<string>(maxLength: 100, nullable: true),
 CreatedBy = table.Column<Guid>(nullable: false),
         CreatedOn = table.Column<DateTime>(nullable: false),
     LastModifiedBy = table.Column<Guid>(nullable: false),
         LastModifiedOn = table.Column<DateTime>(nullable: true)
     },
         constraints: table =>
    {
       table.PrimaryKey("PK_Products", x => x.Id);
     table.ForeignKey("FK_Products_Categories", x => x.CategoryId, "Categories", "Id");
 });

        // Create ProductImages table
 migrationBuilder.CreateTable(
       name: "ProductImages",
        schema: "Catalog",
          columns: table => new
        {
  Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
       ProductId = table.Column<Guid>(nullable: false),
  Url = table.Column<string>(maxLength: 500, nullable: false),
       AltText = table.Column<string>(maxLength: 200, nullable: true),
          Order = table.Column<int>(nullable: false),
                IsPrimary = table.Column<bool>(nullable: false)
      },
    constraints: table =>
   {
          table.PrimaryKey("PK_ProductImages", x => x.Id);
      table.ForeignKey("FK_ProductImages_Products", x => x.ProductId, "Products", "Id", onDelete: ReferentialAction.Cascade);
         });

    // Create indexes
   migrationBuilder.CreateIndex("IX_Categories_Code", "Categories", "Code", unique: true, schema: "Catalog");
    migrationBuilder.CreateIndex("IX_Products_SKU", "Products", "SKU", unique: true, schema: "Catalog");
    migrationBuilder.CreateIndex("IX_Products_CategoryId", "Products", "CategoryId", schema: "Catalog");
    }
}
```

---

## 3. REST API Controllers

### Bước 3.1: Products Controller

**File:** `src/Host/Controllers/Catalog/ProductsController.cs`

```csharp
using ECO.WebApi.Application.Catalog.Products;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers.Catalog;

/// <summary>
/// Products API endpoints
/// </summary>
[Route("api/catalog/products")]
public class ProductsController : BaseApiController
{
    /// <summary>
 /// Search products with filters and pagination
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(PaginatedResult<ProductListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] SearchProductsQuery request)
    {
    var result = await Mediator.Send(request);
 return Ok(result);
    }

    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await Mediator.Send(new GetProductQuery(id));
        return Ok(result);
    }

  /// <summary>
    /// Create a new product
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var id = await Mediator.Send(command);
     return CreatedAtAction(nameof(Get), new { id }, id);
    }

    /// <summary>
    /// Update an existing product
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
 public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command)
    {
        if (id != command.Id)
     return BadRequest("ID mismatch");

      await Mediator.Send(command);
        return NoContent();
}

    /// <summary>
    /// Delete a product
  /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
  await Mediator.Send(new DeleteProductCommand(id));
  return NoContent();
    }
}
```

---

### Bước 3.2: Categories Controller

**File:** `src/Host/Controllers/Catalog/CategoriesController.cs`

```csharp
using ECO.WebApi.Application.Catalog.Categories;
using Microsoft.AspNetCore.Mvc;

namespace ECO.WebApi.Host.Controllers.Catalog;

/// <summary>
/// Categories API endpoints
/// </summary>
[Route("api/catalog/categories")]
public class CategoriesController : BaseApiController
{
    /// <summary>
    /// Search categories
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] SearchCategoriesQuery request)
    {
        var result = await Mediator.Send(request);
    return Ok(result);
    }

    /// <summary>
    /// Get category by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await Mediator.Send(new GetCategoryQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Create a new category
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    /// <summary>
    /// Update an existing category
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryCommand command)
    {
   if (id != command.Id)
         return BadRequest("ID mismatch");

        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete a category
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCategoryCommand(id));
        return NoContent();
    }
}
```

---

## 4. Testing với Swagger

### Bước 4.1: Create Product Example

```json
POST /api/catalog/products
{
  "name": "iPhone 15 Pro Max",
  "sku": "IPHONE15-PM-256-BLK",
  "description": "Latest flagship iPhone",
  "price": 1199.99,
  "currency": "USD",
  "compareAtPrice": 1299.99,
  "stock": 50,
  "lowStockThreshold": 10,
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "brand": "Apple",
  "isPublished": true,
  "images": [
    {
    "url": "/images/iphone15-pro-max-black.jpg",
      "altText": "iPhone 15 Pro Max Black",
      "order": 0,
      "isPrimary": true
    }
  ]
}
```

**Response:**
```json
"3fa85f64-5717-4562-b3fc-2c963f66afa6"
```

---

### Bước 4.2: Search Products Example

```json
POST /api/catalog/products/search
{
  "keyword": "iphone",
  "minPrice": 500,
  "maxPrice": 2000,
  "isPublished": true,
  "inStockOnly": true,
  "pageNumber": 1,
  "pageSize": 20
}
```

**Response:**
```json
{
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "iPhone 15 Pro Max",
   "sku": "IPHONE15-PM-256-BLK",
      "price": 1199.99,
      "currency": "USD",
      "stock": 50,
      "isPublished": true,
  "primaryImageUrl": "/images/iphone15-pro-max-black.jpg",
      "categoryName": "Smartphones"
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

---

## 5. Summary

### ✅ Infrastructure & Controllers Complete:

**EF Core Configurations:**
- ✅ ProductConfiguration (with value object mappings)
- ✅ CategoryConfiguration (hierarchical structure)
- ✅ Value object converters (Money, SKU, ProductImage)

**Database:**
- ✅ Migrations (AddCatalogModule)
- ✅ Tables (Products, Categories, ProductImages)
- ✅ Indexes (unique SKU, performance indexes)

**Controllers:**
- ✅ ProductsController (CRUD + Search)
- ✅ CategoriesController (CRUD + Search)
- ✅ RESTful endpoints
- ✅ Swagger documentation

### 📊 Complete Flow:

```
1. User → POST /api/catalog/products
   ↓
2. Controller → CreateProductCommand
   ↓
3. Validator → FluentValidation rules
  ↓
4. Handler → Domain factory (Product.Create)
   ↓
5. Domain → Value objects (Money, SKU, ProductImage)
   ↓
6. Domain → Business rules validation
   ↓
7. Repository → Save to database
   ↓
8. Decorator → Add EntityCreatedEvent
   ↓
9. DbContext → Publish domain events
   ↓
10. Event Handlers → Side effects (email, cache, etc.)
  ↓
11. Response → Product ID returned
```

### 📁 Complete File Structure:

```
ECO.WebApi/
├── Domain/
│   └── Catalog/
│       ├── ValueObjects/ (Money, SKU, ProductImage)
│       ├── Events/ (ProductEvents)
│       ├── Product.cs
│       ├── Category.cs
│       └── ProductStatus.cs
├── Application/
│   └── Catalog/
│       ├── Products/ (Commands, Queries, DTOs, Specs)
│       └── Categories/ (Commands, Queries, DTOs, Specs)
├── Infrastructure/
│└── Persistence/
│       └── Configurations/
│    ├── ProductConfiguration.cs
│           └── CategoryConfiguration.cs
└── Host/
    └── Controllers/
        └── Catalog/
      ├── ProductsController.cs
        └── CategoriesController.cs
```

---

## 6. Next Steps

**Completed:** BUILD_28 - Catalog Module (All 4 sub-docs)

**Tiếp theo:** [BUILD_29 - Notifications](BUILD_29_Notifications.md)

---

**Quay lại:** [BUILD_28 - Main](BUILD_28_Catalog_Module.md)

---

**Document Version:** 1.0 (Infrastructure & Controllers)  
**Last Updated:** 2026-01-30  
**Note:** Complete implementation with EF Core configs and REST APIs.
