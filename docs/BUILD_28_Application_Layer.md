# BUILD_28: Catalog Module - Application Layer (Theoretical Design)

> 📚 [Quay lại BUILD_28 Main](BUILD_28_Catalog_Module.md)  
> 📋 **Prerequisites:** BUILD_28_Domain_Layer.md hoàn thành

Tài liệu này hướng dẫn xây dựng **Application Layer** cho Catalog Module theo **CQRS pattern với MediatR**.

---

## 1. Overview

**Làm gì:** Xây dựng Application Layer với CQRS (Command Query Responsibility Segregation) pattern.

**Tại sao CQRS:**
- ✅ **Separation of Concerns:** Commands (write) tách biệt Queries (read)
- ✅ **Optimized Queries:** Queries có thể return DTOs trực tiếp
- ✅ **Validation:** FluentValidation cho tất cả commands
- ✅ **Testability:** Dễ test từng handler độc lập
- ✅ **Scalability:** Có thể scale read/write khác nhau

**Trong bước này chúng ta sẽ:**
- ✅ Tạo DTOs (Data Transfer Objects)
- ✅ Tạo Commands (Create, Update, Delete)
- ✅ Tạo Queries (Get, Search, List)
- ✅ Tạo Validators (FluentValidation)
- ✅ Tạo Specifications (query patterns)

---

## 2. Product DTOs

### Bước 2.1: Product DTOs

**File:** `src/Core/Application/Catalog/Products/ProductDto.cs`

```csharp
namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Product DTO for API responses
/// </summary>
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string SKU { get; set; } = default!;
    public string? Description { get; set; }
    
    // Pricing
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal? CompareAtPrice { get; set; }
    public decimal? Cost { get; set; }
    
    // Inventory
    public int Stock { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool IsInStock { get; set; }
    public bool IsLowStock { get; set; }
    
  // Category
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    
    // Additional
    public string? Brand { get; set; }
    public bool IsPublished { get; set; }
    
    // Calculated fields
    public decimal? DiscountPercentage { get; set; }
    public decimal? ProfitMargin { get; set; }
    
    // Images
    public List<ProductImageDto> Images { get; set; } = new();
    
  // Audit
    public DateTime CreatedOn { get; set; }
    public DateTime? LastModifiedOn { get; set; }
}

/// <summary>
/// Product image DTO
/// </summary>
public class ProductImageDto
{
    public string Url { get; set; } = default!;
    public string? AltText { get; set; }
    public int Order { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Simplified product DTO for lists
/// </summary>
public class ProductListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string SKU { get; set; } = default!;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public int Stock { get; set; }
    public bool IsPublished { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public string? CategoryName { get; set; }
}
```

---

## 3. Product Commands

### Bước 3.1: Create Product Command

**File:** `src/Core/Application/Catalog/Products/CreateProductCommand.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using ECO.WebApi.Domain.Catalog.ValueObjects;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Command to create a new product
/// </summary>
public class CreateProductCommand : IRequest<Guid>
{
    public string Name { get; set; } = default!;
    public string SKU { get; set; } = default!;
    public string? Description { get; set; }
    
    // Pricing
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal? CompareAtPrice { get; set; }
    public decimal? Cost { get; set; }
    
    // Inventory
    public int Stock { get; set; }
    public int? LowStockThreshold { get; set; }
    
    // Category
    public Guid CategoryId { get; set; }
    
    // Additional
    public string? Brand { get; set; }
    public bool IsPublished { get; set; }
    
    // Images
    public List<CreateProductImageDto>? Images { get; set; }
}

public class CreateProductImageDto
{
    public string Url { get; set; } = default!;
    public string? AltText { get; set; }
    public int Order { get; set; }
  public bool IsPrimary { get; set; }
}

/// <summary>
/// Validator for CreateProductCommand
/// </summary>
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
         .NotEmpty().WithMessage("Product name is required")
.MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

        RuleFor(x => x.SKU)
    .NotEmpty().WithMessage("SKU is required")
            .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU must contain only uppercase letters, numbers, and hyphens");

     RuleFor(x => x.Price)
         .GreaterThan(0).WithMessage("Price must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
         .Length(3).WithMessage("Currency must be 3 characters (ISO 4217)");

    RuleFor(x => x.CompareAtPrice)
            .GreaterThan(x => x.Price)
   .When(x => x.CompareAtPrice.HasValue)
  .WithMessage("Compare-at price must be greater than price");

    RuleFor(x => x.Stock)
         .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative");

  RuleFor(x => x.CategoryId)
      .NotEmpty().WithMessage("Category is required");
    }
}

/// <summary>
/// Handler for CreateProductCommand
/// </summary>
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IRepository<Product> _repository;
    private readonly IRepository<Category> _categoryRepository;

    public CreateProductCommandHandler(
        IRepository<Product> repository,
   IRepository<Category> categoryRepository)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Verify category exists
        var categoryExists = await _categoryRepository.AnyAsync(
        new CategoryByIdSpec(request.CategoryId), 
     cancellationToken);
   
     if (!categoryExists)
          throw new NotFoundException($"Category with ID {request.CategoryId} not found");

        // Create value objects
   var sku = SKU.Of(request.SKU);
        var price = Money.Of(request.Price, request.Currency);
        var compareAtPrice = request.CompareAtPrice.HasValue 
  ? Money.Of(request.CompareAtPrice.Value, request.Currency) 
       : null;
        var cost = request.Cost.HasValue 
   ? Money.Of(request.Cost.Value, request.Currency) 
        : null;

    // Create product using domain factory
   var product = Product.Create(
            name: request.Name,
            sku: sku,
   price: price,
     categoryId: request.CategoryId,
     stock: request.Stock,
       description: request.Description,
 brand: request.Brand
        );

      // Set optional properties
        if (compareAtPrice != null || cost != null)
        {
         product.UpdatePricing(price, compareAtPrice, cost);
 }

        if (request.LowStockThreshold.HasValue)
        {
      product.SetLowStockThreshold(request.LowStockThreshold.Value);
        }

        if (request.IsPublished)
        {
            product.Publish();
        }

        // Set images
        if (request.Images?.Any() == true)
      {
    var images = request.Images.Select(img => 
       ProductImage.Of(img.Url, img.AltText, img.Order, img.IsPrimary));
     product.SetImages(images);
        }

        // Save to repository
   // ⚠️ EntityCreatedEvent will be auto-added by EventAddingRepositoryDecorator
        await _repository.AddAsync(product, cancellationToken);

        return product.Id;
    }
}
```

**Giải thích:**

**1. Command Pattern:**
- `CreateProductCommand` - Input DTO
- `CreateProductCommandValidator` - FluentValidation rules
- `CreateProductCommandHandler` - Business logic

**2. Validation Rules:**
- Name: Required, max 200 chars
- SKU: Required, uppercase alphanumeric + hyphens
- Price: Must be > 0
- CompareAtPrice: Must be > Price
- Category: Must exist

**3. Domain Logic:**
- Uses domain factory method `Product.Create()`
- Creates value objects (Money, SKU, ProductImage)
- Validates category exists
- Domain events auto-added by decorator

---

### Bước 3.2: Update Product Command

**File:** `src/Core/Application/Catalog/Products/UpdateProductCommand.cs`

```csharp
using ECO.WebApi.Domain.Catalog;
using ECO.WebApi.Domain.Catalog.ValueObjects;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Command to update an existing product
/// </summary>
public class UpdateProductCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    
    // Pricing
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal? CompareAtPrice { get; set; }
    public decimal? Cost { get; set; }
    
    // Category
    public Guid CategoryId { get; set; }
    
    // Additional
    public string? Brand { get; set; }
}

/// <summary>
/// Validator for UpdateProductCommand
/// </summary>
public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Product ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
   .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

 RuleFor(x => x.Price)
        .GreaterThan(0).WithMessage("Price must be greater than zero");

      RuleFor(x => x.CompareAtPrice)
  .GreaterThan(x => x.Price)
     .When(x => x.CompareAtPrice.HasValue)
   .WithMessage("Compare-at price must be greater than price");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required");
    }
}

/// <summary>
/// Handler for UpdateProductCommand
/// </summary>
public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Unit>
{
    private readonly IRepository<Product> _repository;
    private readonly IRepository<Category> _categoryRepository;

    public UpdateProductCommandHandler(
        IRepository<Product> repository,
        IRepository<Category> categoryRepository)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
    }

 public async Task<Unit> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        // Get existing product
   var product = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
            throw new NotFoundException($"Product with ID {request.Id} not found");

        // Verify category exists
  var categoryExists = await _categoryRepository.AnyAsync(
            new CategoryByIdSpec(request.CategoryId), 
      cancellationToken);
   
    if (!categoryExists)
            throw new NotFoundException($"Category with ID {request.CategoryId} not found");

        // Create value objects
    var price = Money.Of(request.Price, request.Currency);
        var compareAtPrice = request.CompareAtPrice.HasValue 
            ? Money.Of(request.CompareAtPrice.Value, request.Currency) 
   : null;
        var cost = request.Cost.HasValue 
 ? Money.Of(request.Cost.Value, request.Currency) 
            : null;

     // Update using domain methods
        product.Update(
  name: request.Name,
            price: price,
     categoryId: request.CategoryId,
 description: request.Description,
      brand: request.Brand
 );

        // Update pricing if needed
        if (compareAtPrice != null || cost != null)
        {
   product.UpdatePricing(price, compareAtPrice, cost);
        }

        // Save changes
        // ⚠️ EntityUpdatedEvent will be auto-added by decorator
      // ⚠️ Business events (ProductPriceChangedEvent) already added in domain methods
        await _repository.UpdateAsync(product, cancellationToken);

        return Unit.Value;
    }
}
```

---

### Bước 3.3: Delete Product Command

**File:** `src/Core/Application/Catalog/Products/DeleteProductCommand.cs`

```csharp
namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Command to delete a product
/// </summary>
public class DeleteProductCommand : IRequest<Unit>
{
    public Guid Id { get; set; }

    public DeleteProductCommand(Guid id)
    {
        Id = id;
    }
}

/// <summary>
/// Handler for DeleteProductCommand
/// </summary>
public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Unit>
{
    private readonly IRepository<Product> _repository;

    public DeleteProductCommandHandler(IRepository<Product> repository)
    {
      _repository = repository;
    }

    public async Task<Unit> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
  var product = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
            throw new NotFoundException($"Product with ID {request.Id} not found");

    // Soft delete (if ISoftDelete is implemented in BUILD_19)
     // Hard delete otherwise
        await _repository.DeleteAsync(product, cancellationToken);

        return Unit.Value;
    }
}
```

---

## 4. Product Queries

### Bước 4.1: Get Product Query

**File:** `src/Core/Application/Catalog/Products/GetProductQuery.cs`

```csharp
using Mapster;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Query to get a single product by ID
/// </summary>
public class GetProductQuery : IRequest<ProductDto>
{
    public Guid Id { get; set; }

    public GetProductQuery(Guid id)
    {
      Id = id;
    }
}

/// <summary>
/// Handler for GetProductQuery
/// </summary>
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, ProductDto>
{
    private readonly IReadRepository<Product> _repository;

    public GetProductQueryHandler(IReadRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
     var spec = new ProductByIdWithDetailsSpec(request.Id);
     var product = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (product == null)
            throw new NotFoundException($"Product with ID {request.Id} not found");

    // Map to DTO using Mapster
        var dto = product.Adapt<ProductDto>();
        
        // Map calculated properties
   dto.IsInStock = product.IsInStock();
        dto.IsLowStock = product.IsLowStock();
        dto.DiscountPercentage = product.GetDiscountPercentage();
        dto.ProfitMargin = product.GetProfitMargin();

        return dto;
    }
}
```

---

### Bước 4.2: Search Products Query

**File:** `src/Core/Application/Catalog/Products/SearchProductsQuery.cs`

```csharp
using ECO.WebApi.Application.Common.Models;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Query to search products with filters and pagination
/// </summary>
public class SearchProductsQuery : PaginationFilter, IRequest<PaginatedResult<ProductListDto>>
{
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? IsPublished { get; set; }
    public bool? InStockOnly { get; set; }
}

/// <summary>
/// Handler for SearchProductsQuery
/// </summary>
public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, PaginatedResult<ProductListDto>>
{
  private readonly IReadRepository<Product> _repository;

    public SearchProductsQueryHandler(IReadRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedResult<ProductListDto>> Handle(
        SearchProductsQuery request, 
        CancellationToken cancellationToken)
    {
        // Build specification
        var spec = new ProductSearchSpec(request);

      // Get paginated results
        var products = await _repository.ListAsync(spec, cancellationToken);
        var totalCount = await _repository.CountAsync(spec, cancellationToken);

   // Map to DTOs
        var dtos = products.Select(p => new ProductListDto
        {
  Id = p.Id,
    Name = p.Name,
 SKU = p.SKU.Value,
        Price = p.Price.Amount,
            Currency = p.Price.Currency,
            Stock = p.Stock,
  IsPublished = p.IsPublished,
        PrimaryImageUrl = p.Images.FirstOrDefault(img => img.IsPrimary)?.Url,
       CategoryName = p.Category?.Name
        }).ToList();

    return new PaginatedResult<ProductListDto>(
      dtos,
            totalCount,
        request.PageNumber,
   request.PageSize
 );
    }
}
```

---

## 5. Product Specifications

### Bước 5.1: Product Specifications

**File:** `src/Core/Application/Catalog/Products/ProductSpecifications.cs`

```csharp
using Ardalis.Specification;

namespace ECO.WebApi.Application.Catalog.Products;

/// <summary>
/// Specification to get product by ID
/// </summary>
public class ProductByIdSpec : Specification<Product>, ISingleResultSpecification<Product>
{
    public ProductByIdSpec(Guid id)
    {
    Query.Where(p => p.Id == id);
    }
}

/// <summary>
/// Specification to get product by ID with related data
/// </summary>
public class ProductByIdWithDetailsSpec : Specification<Product>, ISingleResultSpecification<Product>
{
    public ProductByIdWithDetailsSpec(Guid id)
    {
        Query
      .Where(p => p.Id == id)
            .Include(p => p.Category);
    }
}

/// <summary>
/// Specification to search products
/// </summary>
public class ProductSearchSpec : Specification<Product>
{
    public ProductSearchSpec(SearchProductsQuery filter)
    {
        // Keyword search
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
          Query.Where(p => 
         p.Name.Contains(filter.Keyword) || 
                p.Description!.Contains(filter.Keyword) ||
  p.SKU.Value.Contains(filter.Keyword)
   );
        }

        // Category filter
      if (filter.CategoryId.HasValue)
  {
      Query.Where(p => p.CategoryId == filter.CategoryId.Value);
      }

      // Price range filter
if (filter.MinPrice.HasValue)
        {
         Query.Where(p => p.Price.Amount >= filter.MinPrice.Value);
     }

   if (filter.MaxPrice.HasValue)
        {
      Query.Where(p => p.Price.Amount <= filter.MaxPrice.Value);
     }

        // Published filter
        if (filter.IsPublished.HasValue)
        {
          Query.Where(p => p.IsPublished == filter.IsPublished.Value);
        }

  // In stock filter
      if (filter.InStockOnly == true)
        {
            Query.Where(p => p.Stock > 0);
        }

        // Include related data
        Query.Include(p => p.Category);

     // Pagination
      Query
   .Skip((filter.PageNumber - 1) * filter.PageSize)
   .Take(filter.PageSize);

   // Default ordering
        Query.OrderBy(p => p.Name);
    }
}

/// <summary>
/// Specification to check if product with SKU exists
/// </summary>
public class ProductBySKUSpec : Specification<Product>, ISingleResultSpecification<Product>
{
    public ProductBySKUSpec(string sku)
    {
        Query.Where(p => p.SKU.Value == sku.ToUpperInvariant());
    }
}
```

---

## 6. Category Commands & Queries

### Bước 6.1: Create Category Command

**File:** `src/Core/Application/Catalog/Categories/CreateCategoryCommand.cs`

```csharp
namespace ECO.WebApi.Application.Catalog.Categories;

/// <summary>
/// Command to create a new category
/// </summary>
public class CreateCategoryCommand : IRequest<Guid>
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int DisplayOrder { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Validator for CreateCategoryCommand
/// </summary>
public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required")
            .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters");

RuleFor(x => x.Code)
 .NotEmpty().WithMessage("Category code is required")
       .MaximumLength(50).WithMessage("Category code cannot exceed 50 characters")
   .Matches(@"^[a-z0-9\-]+$").WithMessage("Category code must be lowercase alphanumeric with hyphens");
    }
}

/// <summary>
/// Handler for CreateCategoryCommand
/// </summary>
public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IRepository<Category> _repository;

 public CreateCategoryCommandHandler(IRepository<Category> repository)
    {
   _repository = repository;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
// Check if code already exists
  var existingCategory = await _repository.FirstOrDefaultAsync(
  new CategoryByCodeSpec(request.Code), 
   cancellationToken);

     if (existingCategory != null)
 throw new ConflictException($"Category with code '{request.Code}' already exists");

        // Verify parent exists if specified
        if (request.ParentId.HasValue)
        {
     var parentExists = await _repository.AnyAsync(
new CategoryByIdSpec(request.ParentId.Value), 
  cancellationToken);

            if (!parentExists)
                throw new NotFoundException($"Parent category with ID {request.ParentId} not found");
        }

        // Create category using domain factory
        var category = Category.Create(
            name: request.Name,
     code: request.Code,
description: request.Description,
     parentId: request.ParentId,
displayOrder: request.DisplayOrder,
  imageUrl: request.ImageUrl
        );

        await _repository.AddAsync(category, cancellationToken);

        return category.Id;
    }
}
```

---

### Bước 6.2: Search Categories Query

**File:** `src/Core/Application/Catalog/Categories/SearchCategoriesQuery.cs`

```csharp
using ECO.WebApi.Application.Common.Models;

namespace ECO.WebApi.Application.Catalog.Categories;

/// <summary>
/// Category DTO
/// </summary>
public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public int ProductCount { get; set; }
    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// Query to search categories
/// </summary>
public class SearchCategoriesQuery : BaseFilter, IRequest<List<CategoryDto>>
{
    public string? Keyword { get; set; }
    public Guid? ParentId { get; set; }
    public bool? IsActive { get; set; }
    public bool RootOnly { get; set; }  // Only root categories (ParentId == null)
}

/// <summary>
/// Handler for SearchCategoriesQuery
/// </summary>
public class SearchCategoriesQueryHandler : IRequestHandler<SearchCategoriesQuery, List<CategoryDto>>
{
    private readonly IReadRepository<Category> _repository;

    public SearchCategoriesQueryHandler(IReadRepository<Category> repository)
    {
      _repository = repository;
    }

    public async Task<List<CategoryDto>> Handle(SearchCategoriesQuery request, CancellationToken cancellationToken)
    {
        var spec = new CategorySearchSpec(request);
        var categories = await _repository.ListAsync(spec, cancellationToken);

    var dtos = categories.Select(c => new CategoryDto
        {
       Id = c.Id,
   Name = c.Name,
 Code = c.Code,
     Description = c.Description,
   ParentId = c.ParentId,
            ParentName = c.Parent?.Name,
            DisplayOrder = c.DisplayOrder,
       IsActive = c.IsActive,
            ImageUrl = c.ImageUrl,
          ProductCount = c.Products?.Count ?? 0,
            CreatedOn = c.CreatedOn
  }).ToList();

        return dtos;
    }
}
```

---

## 7. Summary

### ✅ Application Layer Complete (Theoretical Design):

**Commands (Write Operations):**
- ✅ CreateProductCommand (with validation & handler)
- ✅ UpdateProductCommand (with validation & handler)
- ✅ DeleteProductCommand (with handler)
- ✅ CreateCategoryCommand (with validation & handler)

**Queries (Read Operations):**
- ✅ GetProductQuery (single product with details)
- ✅ SearchProductsQuery (paginated search with filters)
- ✅ SearchCategoriesQuery (hierarchical categories)

**DTOs:**
- ✅ ProductDto (detailed product info)
- ✅ ProductListDto (simplified for lists)
- ✅ CategoryDto (category with parent info)

**Specifications:**
- ✅ ProductByIdSpec
- ✅ ProductByIdWithDetailsSpec
- ✅ ProductSearchSpec
- ✅ ProductBySKUSpec
- ✅ CategoryByCodeSpec
- ✅ CategorySearchSpec

**Validators:**
- ✅ CreateProductCommandValidator (FluentValidation)
- ✅ UpdateProductCommandValidator
- ✅ CreateCategoryCommandValidator

### 📊 CQRS Benefits:

**1. Separation of Concerns:**
```csharp
// Commands - Write operations (use domain methods)
await _mediator.Send(new CreateProductCommand { ... });

// Queries - Read operations (optimized DTOs)
var products = await _mediator.Send(new SearchProductsQuery { ... });
```

**2. Optimized Queries:**
```csharp
// Query can return DTOs directly
// No need to load full domain entities for lists
var spec = new ProductSearchSpec(filter);
var products = await _repository.ListAsync(spec);  // Optimized query
```

**3. Validation:**
```csharp
// FluentValidation automatically runs before handler
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    // Validation rules
}
```

### 📁 File Structure:

```
src/Core/Application/Catalog/
├── Products/
│   ├── ProductDto.cs
│   ├── CreateProductCommand.cs
│   ├── UpdateProductCommand.cs
│   ├── DeleteProductCommand.cs
│   ├── GetProductQuery.cs
│   ├── SearchProductsQuery.cs
│   └── ProductSpecifications.cs
└── Categories/
    ├── CategoryDto.cs
    ├── CreateCategoryCommand.cs
    ├── UpdateCategoryCommand.cs
    ├── GetCategoryQuery.cs
    └── SearchCategoriesQuery.cs
```

---

## 8. Next Steps

**Tiếp theo:** [BUILD_28_Infrastructure_Layer.md](BUILD_28_Infrastructure_Layer.md)

Trong doc tiếp theo, chúng ta sẽ:
- ✅ EF Core configurations
- ✅ Value object converters
- ✅ Database migrations
- ✅ Seeding data

---

**Quay lại:** [BUILD_28 - Main](BUILD_28_Catalog_Module.md)

---

**Document Version:** 1.0 (Theoretical Design - CQRS with MediatR)  
**Last Updated:** 2026-01-30  
**Note:** This is **theoretical design** following CQRS best practices.
