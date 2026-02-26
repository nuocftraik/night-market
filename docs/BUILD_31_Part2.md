# BUILD_31 (Part 2): Attributes, Tags, Reviews & Configurations

> 📚 **Part 1:** [BUILD_31_Database_Design_Catalog_Module.md](BUILD_31_Database_Design_Catalog_Module.md) (Products, Variants, Categories)  
> 📚 **Part 2:** This file - Attributes, Tags, Reviews, Configurations, Examples, Seed Data

---

## 4. Attribute System (EAV Pattern)

### 4.1. EAV Pattern Overview

**Entity-Attribute-Value (EAV)** pattern cho phép định nghĩa attributes động cho products mà không cần alter database schema.

**Use Case:**
```
T-Shirt Product:
├─ Attribute: "Size" (Dropdown)
│  ├─ Value: "Small"
│  ├─ Value: "Medium"
│  └─ Value: "Large"
├─ Attribute: "Color" (Radio)
│  ├─ Value: "Red"
│  ├─ Value: "Blue"
│  └─ Value: "Green"
└─ Attribute: "Material" (Checkbox)
   ├─ Value: "Cotton"
   ├─ Value: "Polyester"
   └─ Value: "Blend"
```

---

### 4.2. AttributeType Enum

**File:** `src/Core/Domain/Enum/AttributeType.cs`

```csharp
namespace ECO.WebApi.Domain.Enum;

/// <summary>
/// Loại hiển thị của attribute
/// </summary>
public enum AttributeType
{
    /// <summary>
    /// Dropdown list (single selection)
    /// </summary>
    Dropdown = 1,
    
    /// <summary>
    /// Radio buttons (single selection)
    /// </summary>
    Radio = 2,
    
    /// <summary>
    /// Checkboxes (multiple selection)
 /// </summary>
    Checkbox = 3
}
```

---

### 4.3. Attribute Entity

**File:** `src/Core/Domain/Catalog/Attributes/Attribute.cs`

```csharp
using ECO.WebApi.Domain.Enum;

namespace ECO.WebApi.Domain.Catalog.Attributes;

/// <summary>
/// Product attribute (e.g., Size, Color, Material)
/// </summary>
public class Attribute : BaseEntity
{
    public string Name { get; private set; }
    public AttributeType AttributeType { get; private set; }
    public Guid ProductId { get; private set; }
    
 // Navigation
    public virtual Product Product { get; private set; } = default!;
    public virtual List<AttributeValue> AttributeValues { get; private set; } = new();
    
  private Attribute() { }
    
    public Attribute(string name, AttributeType attributeType, Guid productId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Attribute name cannot be empty", nameof(name));
   
        Name = name;
        AttributeType = attributeType;
        ProductId = productId;
    }
    
    public void AddValue(string value)
    {
    if (string.IsNullOrWhiteSpace(value))
  throw new ArgumentException("Attribute value cannot be empty", nameof(value));
   
        if (AttributeValues.Any(av => av.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
       return; // Value already exists
        
        AttributeValues.Add(new AttributeValue(value, Id));
    }
    
    public void RemoveValue(Guid attributeValueId)
    {
   var value = AttributeValues.FirstOrDefault(av => av.Id == attributeValueId);
        if (value != null)
            AttributeValues.Remove(value);
    }
}
```

---

### 4.4. AttributeValue Entity

**File:** `src/Core/Domain/Catalog/Attributes/AttributeValue.cs`

```csharp
namespace ECO.WebApi.Domain.Catalog.Attributes;

/// <summary>
/// Value for an attribute (e.g., "Small", "Red", "Cotton")
/// </summary>
public class AttributeValue : BaseEntity
{
    public string Value { get; private set; }
    public Guid AttributeId { get; private set; }
    
    // Navigation
    public virtual Attribute Attribute { get; private set; } = default!;
    public virtual List<VariantAttributeValue> VariantAttributeValues { get; private set; } = new();
    
    private AttributeValue() { }
    
    public AttributeValue(string value, Guid attributeId)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty", nameof(value));
        
 Value = value;
        AttributeId = attributeId;
    }
}
```

---

### 4.5. VariantAttributeValue Entity (Junction)

**File:** `src/Core/Domain/Catalog/VariantAttributeValue.cs`

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Domain.Catalog;

/// <summary>
/// Junction table: Variant ↔ AttributeValue (Many-to-Many)
/// Uses Composite Primary Key (NO surrogate Id)
/// </summary>
[PrimaryKey(nameof(VariantId), nameof(AttributeValueId))]
public class VariantAttributeValue
{
    public Guid VariantId { get; private set; }
    public Guid AttributeValueId { get; private set; }
    
    // Navigation
    public virtual Variant Variant { get; private set; } = default!;
    public virtual Attributes.AttributeValue AttributeValue { get; private set; } = default!;
    
    private VariantAttributeValue() { }
    
    public VariantAttributeValue(Guid variantId, Guid attributeValueId)
    {
        VariantId = variantId;
        AttributeValueId = attributeValueId;
    }
}
```

---

### 4.6. Attribute Configurations

**File:** `src/Infrastructure/Persistence/Configurations/Catalog/AttributeConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations.Catalog;

public class AttributeConfiguration : IEntityTypeConfiguration<Attribute>
{
    public void Configure(EntityTypeBuilder<Attribute> builder)
    {
   builder.ToTable("Attributes", "Catalog");
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.AttributeType).IsRequired();
        builder.Property(a => a.ProductId).IsRequired();
   
        // Indexes
    builder.HasIndex(a => a.ProductId);
        
        // Relationships
        builder.HasOne(a => a.Product)
  .WithMany(p => p.Attributes)
            .HasForeignKey(a => a.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(a => a.AttributeValues)
    .WithOne(av => av.Attribute)
   .HasForeignKey(av => av.AttributeId)
     .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**File:** `src/Infrastructure/Persistence/Configurations/Catalog/AttributeValueConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations.Catalog;

public class AttributeValueConfiguration : IEntityTypeConfiguration<AttributeValue>
{
    public void Configure(EntityTypeBuilder<AttributeValue> builder)
    {
        builder.ToTable("AttributeValues", "Catalog");
     builder.HasKey(av => av.Id);
        
        builder.Property(av => av.Value).IsRequired().HasMaxLength(100);
        builder.Property(av => av.AttributeId).IsRequired();
        
     // Indexes
        builder.HasIndex(av => av.AttributeId);
        
        // Relationships
    builder.HasOne(av => av.Attribute)
     .WithMany(a => a.AttributeValues)
         .HasForeignKey(av => av.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);
   
     builder.HasMany(av => av.VariantAttributeValues)
  .WithOne(vav => vav.AttributeValue)
  .HasForeignKey(vav => vav.AttributeValueId)
            .OnDelete(DeleteBehavior.Restrict); // ⚠️ Prevent Multiple Cascade Paths
    }
}
```

---

## 5. Tag System

### 5.1. Tag Entity

**File:** `src/Core/Domain/Catalog/Tag.cs`

```csharp
namespace ECO.WebApi.Domain.Catalog;

/// <summary>
/// Product tag (flat structure, no hierarchy)
/// </summary>
public class Tag : BaseEntity
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    
    // Navigation
    public virtual List<ProductTag> ProductTags { get; private set; } = new();
    
    private Tag() { }
    
    public Tag(string name, string slug)
  {
        if (string.IsNullOrWhiteSpace(name))
 throw new ArgumentException("Tag name cannot be empty", nameof(name));
        
     if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Tag slug cannot be empty", nameof(slug));
        
        Name = name;
        Slug = slug;
    }
    
    public void Update(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }
}
```

---

### 5.2. ProductTag Entity (Junction)

**File:** `src/Core/Domain/Catalog/ProductTag.cs`

```csharp
using Microsoft.EntityFrameworkCore;

namespace ECO.WebApi.Domain.Catalog;

/// <summary>
/// Junction table: Product ↔ Tag (Many-to-Many)
/// Uses Composite Primary Key (NO surrogate Id)
/// </summary>
[PrimaryKey(nameof(ProductId), nameof(TagId))]
public class ProductTag
{
    public Guid ProductId { get; private set; }
    public Guid TagId { get; private set; }
    
    // Navigation
    public virtual Product Product { get; private set; } = default!;
 public virtual Tag Tag { get; private set; } = default!;
    
    private ProductTag() { }
    
    public ProductTag(Guid productId, Guid tagId)
    {
        ProductId = productId;
        TagId = tagId;
    }
}
```

---

### 5.3. Tag Configurations

**File:** `src/Infrastructure/Persistence/Configurations/Catalog/TagConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations.Catalog;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags", "Catalog");
        builder.HasKey(t => t.Id);
      
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(300);
        
        // Unique constraint
        builder.HasIndex(t => t.Name).IsUnique();
   builder.HasIndex(t => t.Slug).IsUnique();
        
        // Relationships
        builder.HasMany(t => t.ProductTags)
       .WithOne(pt => pt.Tag)
            .HasForeignKey(pt => pt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

## 6. UserReview System

### 6.1. UserReview Entity

**File:** `src/Core/Domain/Catalog/UserReview.cs`

```csharp
namespace ECO.WebApi.Domain.Catalog;

/// <summary>
/// Product review by user
/// Reviews are tied to VARIANT (not Product)
/// </summary>
public class UserReview : BaseEntity
{
    public Guid VariantId { get; private set; }
    public Guid UserId { get; private set; }
    public int Rating { get; private set; } // 1-5 stars
  public string? Title { get; private set; }
    public string? Content { get; private set; }
    public DateTime CreatedOn { get; private set; }
    
    // Navigation
    public virtual Variant Variant { get; private set; } = default!;
    
    private UserReview() { }
    
  public UserReview(Guid variantId, Guid userId, int rating, string? title, string? content)
    {
      if (rating < 1 || rating > 5)
  throw new ArgumentException("Rating must be between 1 and 5", nameof(rating));
        
        VariantId = variantId;
        UserId = userId;
      Rating = rating;
        Title = title;
        Content = content;
        CreatedOn = DateTime.UtcNow;
    }
    
    public void Update(int rating, string? title, string? content)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5", nameof(rating));
        
        Rating = rating;
        Title = title;
Content = content;
    }
}
```

---

### 6.2. UserReview Configuration

**File:** `src/Infrastructure/Persistence/Configurations/Catalog/UserReviewConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations.Catalog;

public class UserReviewConfiguration : IEntityTypeConfiguration<UserReview>
{
 public void Configure(EntityTypeBuilder<UserReview> builder)
{
        builder.ToTable("UserReviews", "Catalog");
        builder.HasKey(ur => ur.Id);
        
        builder.Property(ur => ur.VariantId).IsRequired();
    builder.Property(ur => ur.UserId).IsRequired();
        builder.Property(ur => ur.Rating).IsRequired();
        builder.Property(ur => ur.Title).HasMaxLength(500);
        builder.Property(ur => ur.Content).HasColumnType("nvarchar(max)");
  builder.Property(ur => ur.CreatedOn).IsRequired();
        
  // Indexes
        builder.HasIndex(ur => ur.VariantId);
        builder.HasIndex(ur => ur.UserId);
    builder.HasIndex(ur => ur.Rating);
        builder.HasIndex(ur => ur.CreatedOn);
   
        // Composite index for common queries
 builder.HasIndex(ur => new { ur.VariantId, ur.Rating });
   
        // Relationships
        builder.HasOne(ur => ur.Variant)
            .WithMany(v => v.UserReviews)
            .HasForeignKey(ur => ur.VariantId)
     .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

## 7. Complete EF Core Configurations Summary

### 7.1. All Configuration Files

```
src/Infrastructure/Persistence/Configurations/Catalog/
├── ProductConfiguration.cs ✅
├── VariantConfiguration.cs ✅
├── CategoryConfiguration.cs ✅
├── ProductCategoryConfiguration.cs ✅
├── AttributeConfiguration.cs ✅
├── AttributeValueConfiguration.cs ✅
├── VariantAttributeValueConfiguration.cs ✅
├── TagConfiguration.cs ✅
├── ProductTagConfiguration.cs ✅
└── UserReviewConfiguration.cs ✅
```

---

### 7.2. ProductCategoryConfiguration

**File:** `src/Infrastructure/Persistence/Configurations/Catalog/ProductCategoryConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations.Catalog;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
  builder.ToTable("ProductCategories", "Catalog");
        
        // ⭐ Composite Primary Key (configured via [PrimaryKey] attribute)
        
        builder.Property(pc => pc.ProductId).IsRequired();
        builder.Property(pc => pc.CategoryId).IsRequired();
        
        // Indexes
        builder.HasIndex(pc => pc.ProductId);
        builder.HasIndex(pc => pc.CategoryId);
        
  // Relationships
        builder.HasOne(pc => pc.Product)
            .WithMany(p => p.ProductCategories)
   .HasForeignKey(pc => pc.ProductId)
        .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(pc => pc.Category)
     .WithMany(c => c.ProductCategories)
      .HasForeignKey(pc => pc.CategoryId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 7.3. VariantAttributeValueConfiguration (Multiple Cascade Paths Fix)

**File:** `src/Infrastructure/Persistence/Configurations/Catalog/VariantAttributeValueConfiguration.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECO.WebApi.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>
/// EF Core configuration for VariantAttributeValue junction table
/// Uses COMPOSITE PRIMARY KEY - NO surrogate Id
/// Fixes Multiple Cascade Paths issue with DeleteBehavior.Restrict
/// </summary>
public class VariantAttributeValueConfiguration : IEntityTypeConfiguration<VariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<VariantAttributeValue> builder)
    {
     builder.ToTable("VariantAttributeValues", "Catalog");
        
   // ⭐ Composite Primary Key (configured via [PrimaryKey] attribute in entity)
      
    builder.Property(vav => vav.VariantId).IsRequired();
      builder.Property(vav => vav.AttributeValueId).IsRequired();
        
  // Indexes for FK columns
        builder.HasIndex(vav => vav.VariantId);
      builder.HasIndex(vav => vav.AttributeValueId);
   
        // ⭐ Relationships with CASCADE vs RESTRICT to avoid Multiple Cascade Paths
        
 // Path 1: Variant → VariantAttributeValues (CASCADE)
      builder.HasOne(vav => vav.Variant)
     .WithMany(v => v.VariantAttributeValues)
          .HasForeignKey(vav => vav.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
   
        // Path 2: AttributeValue → VariantAttributeValues (RESTRICT)
        builder.HasOne(vav => vav.AttributeValue)
            .WithMany(av => av.VariantAttributeValues)
        .HasForeignKey(vav => vav.AttributeValueId)
            .OnDelete(DeleteBehavior.Restrict); // ⚠️ Prevent Multiple Cascade Paths
    }
}
```

**Why Restrict on AttributeValue?**
- ✅ Prevents SQL Server error: "may cause cycles or multiple cascade paths"
- ✅ Business logic: AttributeValues can be reused across variants
- ✅ Must manually clean up VariantAttributeValues before deleting AttributeValue

---

## 8. Usage Examples

### 8.1. Create Configurable Product with Attributes

```csharp
// 1. Create product
var tshirt = Product.CreateConfigurableProduct(
  name: "Basic T-Shirt",
    slug: "basic-tshirt",
    description: "Comfortable cotton t-shirt",
    mainImage: "https://example.com/tshirt.jpg"
);

// 2. Add attributes
tshirt.AddAttribute("Size", AttributeType.Dropdown, new List<string> { "Small", "Medium", "Large" });
tshirt.AddAttribute("Color", AttributeType.Radio, new List<string> { "Red", "Blue", "Green" });

await dbContext.Products.AddAsync(tshirt);
await dbContext.SaveChangesAsync();

// 3. Get attribute value IDs
var sizeSmallId = await dbContext.AttributeValues
    .Where(av => av.Attribute.ProductId == tshirt.Id && 
        av.Attribute.Name == "Size" && 
                 av.Value == "Small")
    .Select(av => av.Id)
    .FirstAsync();

var colorRedId = await dbContext.AttributeValues
    .Where(av => av.Attribute.ProductId == tshirt.Id && 
     av.Attribute.Name == "Color" && 
        av.Value == "Red")
    .Select(av => av.Id)
    .FirstAsync();

// 4. Create variant with attributes
var variant = new Variant(
    sku: "TSHIRT-SM-RED",
  price: 19.99m,
    quantity: 50,
    isDefault: true
);

variant.AddAttributeValue(sizeSmallId);
variant.AddAttributeValue(colorRedId);

tshirt.AddVariant(variant);
await dbContext.SaveChangesAsync();
```

---

### 8.2. Add Tags to Product

```csharp
// Create tags
var newTag = new Tag("New Arrival", "new-arrival");
var summerTag = new Tag("Summer Collection", "summer-collection");

await dbContext.Tags.AddRangeAsync(newTag, summerTag);
await dbContext.SaveChangesAsync();

// Add to product
var product = await dbContext.Products.FindAsync(productId);
product.ProductTags.Add(new ProductTag(product.Id, newTag.Id));
product.ProductTags.Add(new ProductTag(product.Id, summerTag.Id));

await dbContext.SaveChangesAsync();
```

---

### 8.3. Add Product to Categories

```csharp
// Get categories
var electronics = await dbContext.Categories.FirstAsync(c => c.Slug == "electronics");
var computers = await dbContext.Categories.FirstAsync(c => c.Slug == "computers");

// Add product to multiple categories
var product = await dbContext.Products.FindAsync(productId);
product.AddCategory(electronics.Id);
product.AddCategory(computers.Id);

await dbContext.SaveChangesAsync();
```

---

### 8.4. Add Review to Variant

```csharp
var review = new UserReview(
    variantId: variantId,
    userId: userId,
    rating: 5,
    title: "Excellent product!",
    content: "Great quality, fits perfectly. Highly recommend!"
);

await dbContext.UserReviews.AddAsync(review);
await dbContext.SaveChangesAsync();
```

---

### 8.5. Query Product with All Relationships

```csharp
var product = await dbContext.Products
    .Include(p => p.Variants.Where(v => v.IsDefault))
        .ThenInclude(v => v.VariantAttributeValues)
 .ThenInclude(vav => vav.AttributeValue)
         .ThenInclude(av => av.Attribute)
    .Include(p => p.ProductCategories)
        .ThenInclude(pc => pc.Category)
    .Include(p => p.ProductTags)
        .ThenInclude(pt => pt.Tag)
    .Include(p => p.Variants)
   .ThenInclude(v => v.UserReviews)
 .FirstAsync(p => p.Slug == "basic-tshirt");

// Access data
var defaultVariant = product.Variants.First(v => v.IsDefault);
var price = defaultVariant.Price;
var categories = product.ProductCategories.Select(pc => pc.Category.Name);
var tags = product.ProductTags.Select(pt => pt.Tag.Name);
var avgRating = product.Variants.SelectMany(v => v.UserReviews).Average(r => r.Rating);
```

---

## 9. Seed Data Examples

### 9.1. Seed Categories with Hierarchy

```csharp
private async Task SeedCategoriesAsync()
{
    if (await _context.Categories.AnyAsync())
     return;
  
 _logger.LogInformation("Seeding categories...");
    
    // Root categories
    var electronics = Category.CreateRoot("Electronics", "electronics");
    var fashion = Category.CreateRoot("Fashion", "fashion");
    
    await _context.Categories.AddRangeAsync(electronics, fashion);
    await _context.SaveChangesAsync();
    
  // 1st level children
    var computers = Category.CreateChild("Computers", "computers", electronics);
 var phones = Category.CreateChild("Phones", "phones", electronics);
    var mens = Category.CreateChild("Men's Clothing", "mens-clothing", fashion);
    
    electronics.Children.Add(computers);
    electronics.Children.Add(phones);
    fashion.Children.Add(mens);
    
    await _context.SaveChangesAsync();
    
    // 2nd level children
    var laptops = Category.CreateChild("Laptops", "laptops", computers);
    var desktops = Category.CreateChild("Desktops", "desktops", computers);
    
    computers.Children.Add(laptops);
    computers.Children.Add(desktops);
    
    await _context.SaveChangesAsync();
    
    _logger.LogInformation("Seeded categories successfully");
}
```

---

### 9.2. Seed Complete Product with Everything

```csharp
private async Task SeedCompleteProductAsync()
{
    if (await _context.Products.AnyAsync(p => p.Slug == "premium-tshirt"))
        return;
    
    _logger.LogInformation("Seeding complete product...");
    
    // 1. Create product
    var tshirt = Product.CreateConfigurableProduct(
        name: "Premium T-Shirt",
        slug: "premium-tshirt",
        description: "High-quality cotton t-shirt with multiple options",
        mainImage: "https://example.com/premium-tshirt.jpg"
    );
  
    // 2. Add attributes
    tshirt.AddAttribute("Size", AttributeType.Dropdown, 
        new List<string> { "Small", "Medium", "Large", "XL" });
    tshirt.AddAttribute("Color", AttributeType.Radio, 
   new List<string> { "Black", "White", "Gray", "Navy" });
    
    await _context.Products.AddAsync(tshirt);
    await _context.SaveChangesAsync();
    
    // 3. Get attribute values
    var sizeValues = await _context.AttributeValues
        .Where(av => av.Attribute.ProductId == tshirt.Id && av.Attribute.Name == "Size")
        .ToDictionaryAsync(av => av.Value, av => av.Id);
    
    var colorValues = await _context.AttributeValues
        .Where(av => av.Attribute.ProductId == tshirt.Id && av.Attribute.Name == "Color")
        .ToDictionaryAsync(av => av.Value, av => av.Id);
    
  // 4. Create variants
    var variants = new List<Variant>
{
     CreateVariantWithAttributes("TSHIRT-SM-BLK", 29.99m, 100, true, 
            sizeValues["Small"], colorValues["Black"]),
        CreateVariantWithAttributes("TSHIRT-MD-WHT", 29.99m, 150, false, 
      sizeValues["Medium"], colorValues["White"]),
  CreateVariantWithAttributes("TSHIRT-LG-GRY", 32.99m, 80, false, 
            sizeValues["Large"], colorValues["Gray"])
    };
    
    foreach (var variant in variants)
  tshirt.AddVariant(variant);
    
    // 5. Add to categories
    var fashion = await _context.Categories.FirstAsync(c => c.Slug == "fashion");
    var mens = await _context.Categories.FirstAsync(c => c.Slug == "mens-clothing");
    tshirt.AddCategory(fashion.Id);
    tshirt.AddCategory(mens.Id);
    
    // 6. Add tags
    var newTag = await _context.Tags.FirstOrDefaultAsync(t => t.Slug == "new-arrival");
    if (newTag != null)
 tshirt.ProductTags.Add(new ProductTag(tshirt.Id, newTag.Id));
    
    await _context.SaveChangesAsync();
    
    _logger.LogInformation("Seeded complete product successfully");
}

private Variant CreateVariantWithAttributes(string sku, decimal price, int quantity, 
    bool isDefault, Guid sizeValueId, Guid colorValueId)
{
    var variant = new Variant(sku, price, quantity, isDefault);
    variant.AddAttributeValue(sizeValueId);
    variant.AddAttributeValue(colorValueId);
    return variant;
}
```

---

## 10. Summary

### ✅ BUILD_31 Complete Checklist:

**Core Entities:**
- ✅ Product (Variant-First, NO Price/Quantity)
- ✅ Variant (WITH Price/Quantity/SKU/IsDefault)
- ✅ Category (Materialized Path for hierarchy)
- ✅ Attribute (EAV pattern)
- ✅ AttributeValue
- ✅ VariantAttributeValue (Composite PK)
- ✅ Tag (Flat structure)
- ✅ ProductTag (Composite PK)
- ✅ ProductCategory (Composite PK)
- ✅ UserReview

**EF Core Configurations:**
- ✅ All 10 configuration files
- ✅ Composite Primary Keys for junction tables
- ✅ Multiple Cascade Paths fix (DeleteBehavior.Restrict)
- ✅ Strategic indexes for performance

**Best Practices:**
- ✅ Variant-First Design (Shopify/Magento pattern)
- ✅ Materialized Path for categories (O(1) queries)
- ✅ EAV Pattern for flexible attributes
- ✅ Composite PKs for junction tables (no surrogate Id)
- ✅ Research-based architecture

---

**Quay lại:** [Part 1](BUILD_31_Database_Design_Catalog_Module.md) | [Mục lục](BUILD_INDEX.md)

---

**Document Version:** 2.0 (Variant-First Design)  
**Last Updated:** 2025-02-01  
**Author:** ECO.WebApi Development Team  
**Status:** ✅ Production-Ready
