# Specification Builder Extensions - Chi ti?t Implementation

> ?? [Quay l?i BUILD_11](BUILD_11_Repository_Pattern.md)

Document này ch?a FULL CODE implementation c?a Specification Builder Extensions.  
?ây là ph?n ph?c t?p nh?t c?a Repository Pattern v?i Expression Trees, Reflection, và Generic types.

---

## 1. Overview

**File này implement:**
- `SearchBy()` - Combine keyword search + advanced search + advanced filter
- `PaginateBy()` - Apply pagination + sorting
- `SearchByKeyword()` - Simple keyword search
- `AdvancedSearch()` - Search v?i fields c? th?
- `AdvancedFilter()` - Complex filtering v?i operators và logic
- `OrderBy()` - Multiple field sorting

**Dependencies:**
- Ardalis.Specification
- System.Linq.Expressions (Expression Trees)
- System.Reflection
- System.Text.Json

---

## 2. Filter Constants

### B??c 2.1: FilterOperator Constants

**File:** `src/Core/Application/Common/Models/Filter.cs` (update)

```csharp
namespace ECO.WebApi.Application.Common.Models;

// ... existing Filter class ...

/// <summary>
/// Filter operators constants
/// </summary>
public static class FilterOperator
{
    /// <summary>Equal: ==</summary>
    public const string EQ = "eq";
    
    /// <summary>Not Equal: !=</summary>
    public const string NEQ = "neq";
    
    /// <summary>Less Than: &lt;</summary>
    public const string LT = "lt";
    
    /// <summary>Less Than or Equal: &lt;=</summary>
    public const string LTE = "lte";
    
    /// <summary>Greater Than: &gt;</summary>
    public const string GT = "gt";
    
/// <summary>Greater Than or Equal: &gt;=</summary>
    public const string GTE = "gte";
    
    /// <summary>String Contains</summary>
    public const string CONTAINS = "contains";
    
    /// <summary>String Starts With</summary>
    public const string STARTSWITH = "startswith";
  
    /// <summary>String Ends With</summary>
    public const string ENDSWITH = "endswith";
}

/// <summary>
/// Filter logic operators constants
/// </summary>
public static class FilterLogic
{
    /// <summary>AND logic: &amp;&amp;</summary>
 public const string AND = "and";
    
    /// <summary>OR logic: ||</summary>
    public const string OR = "or";
    
    /// <summary>XOR logic: ^</summary>
    public const string XOR = "xor";
}
```

---

## 3. SpecificationBuilderExtensions - Full Implementation

### B??c 3.1: Core Extensions (SearchBy, PaginateBy)

**File:** `src/Core/Application/Common/Specification/SpecificationBuilderExtensions.cs`

```csharp
using Ardalis.Specification;
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Common.Models;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace ECO.WebApi.Application.Common.Specification;

public static class SpecificationBuilderExtensions
{
    #region SearchBy & PaginateBy (Entry Points)

    /// <summary>
    /// Extension method ?? apply t?t c? search và filter t? BaseFilter vào specification.
    /// </summary>
 public static ISpecificationBuilder<T> SearchBy<T>(this ISpecificationBuilder<T> query, BaseFilter filter) =>
     query
        .SearchByKeyword(filter.Keyword)
      .AdvancedSearch(filter.AdvancedSearch)
   .AdvancedFilter(filter.AdvancedFilter);

    /// <summary>
    /// Extension method ?? apply pagination và ordering vào specification.
    /// </summary>
    public static ISpecificationBuilder<T> PaginateBy<T>(this ISpecificationBuilder<T> query, PaginationFilter filter)
    {
        // Validate và set defaults
        if (filter.PageNumber <= 0)
        {
  filter.PageNumber = 1;
        }

        if (filter.PageSize <= 0)
        {
            filter.PageSize = 10;
        }

        // Calculate skip
        if (filter.PageNumber > 1)
        {
            query = query.Skip((filter.PageNumber - 1) * filter.PageSize);
        }

        return query
   .Take(filter.PageSize)
            .OrderBy(filter.OrderBy);
    }

    #endregion

    #region Search Methods

    /// <summary>
    /// Extension method ?? search ??n gi?n v?i keyword trong t?t c? fields.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> SearchByKeyword<T>(
        this ISpecificationBuilder<T> specificationBuilder,
        string? keyword) =>
 specificationBuilder.AdvancedSearch(new Search { Keyword = keyword });

    /// <summary>
    /// Extension method ?? search nâng cao v?i keyword trong các fields c? th? ho?c t?t c? fields.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> AdvancedSearch<T>(
        this ISpecificationBuilder<T> specificationBuilder,
        Search? search)
    {
        if (!string.IsNullOrEmpty(search?.Keyword))
        {
      if (search.Fields?.Any() is true)
  {
                // Search trong các fields ???c ch? ??nh
            foreach (string field in search.Fields)
      {
    var paramExpr = Expression.Parameter(typeof(T));
   MemberExpression propertyExpr = GetPropertyExpression(field, paramExpr);
     specificationBuilder.AddSearchPropertyByKeyword(propertyExpr, paramExpr, search.Keyword);
 }
            }
            else
     {
     // Search trong T?T C? primitive fields
         foreach (var property in typeof(T).GetProperties()
          .Where(prop =>
                (Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType) is { } propertyType
      && !propertyType.IsEnum
&& Type.GetTypeCode(propertyType) != TypeCode.Object))
         {
     var paramExpr = Expression.Parameter(typeof(T));
      var propertyExpr = Expression.Property(paramExpr, property);
     specificationBuilder.AddSearchPropertyByKeyword(propertyExpr, paramExpr, search.Keyword);
    }
      }
    }

        return new OrderedSpecificationBuilder<T>(specificationBuilder.Specification);
    }

    /// <summary>
    /// Private helper method ?? thêm search criteria cho m?t property c? th?.
    /// </summary>
    private static void AddSearchPropertyByKeyword<T>(
  this ISpecificationBuilder<T> specificationBuilder,
        Expression propertyExpr,
        ParameterExpression paramExpr,
        string keyword,
        string operatorSearch = FilterOperator.CONTAINS)
    {
     if (propertyExpr is not MemberExpression memberExpr || memberExpr.Member is not PropertyInfo property)
      {
   throw new ArgumentException("propertyExpr must be a property expression.", nameof(propertyExpr));
    }

        // T?o search pattern
        string searchTerm = operatorSearch switch
        {
        FilterOperator.STARTSWITH => $"{keyword.ToLower()}%",
       FilterOperator.ENDSWITH => $"%{keyword.ToLower()}",
      FilterOperator.CONTAINS => $"%{keyword.ToLower()}%",
        _ => throw new ArgumentException("operatorSearch is not valid.", nameof(operatorSearch))
};

        // Build selector expression
        Expression selectorExpr =
 property.PropertyType == typeof(string)
? propertyExpr
     : Expression.Condition(
Expression.Equal(
      Expression.Convert(propertyExpr, typeof(object)),
     Expression.Constant(null, typeof(object))),
              Expression.Constant(null, typeof(string)),
  Expression.Call(propertyExpr, "ToString", null, null));

        // Convert to lowercase
        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
        Expression callToLowerMethod = Expression.Call(selectorExpr, toLowerMethod!);
   var selector = Expression.Lambda<Func<T, string>>(callToLowerMethod, paramExpr);

        // Add to SearchCriterias
   ((List<SearchExpressionInfo<T>>)specificationBuilder.Specification.SearchCriterias)
            .Add(new SearchExpressionInfo<T>(selector, searchTerm, 1));
    }

    #endregion

    #region Filter Methods

    /// <summary>
    /// Extension method ?? apply advanced filter v?i operators và logic.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> AdvancedFilter<T>(
 this ISpecificationBuilder<T> specificationBuilder,
        Filter? filter)
    {
  if (filter is not null)
    {
          var parameter = Expression.Parameter(typeof(T));
            Expression binaryExpresioFilter;

        if (!string.IsNullOrEmpty(filter.Logic))
      {
 if (filter.Filters is null)
          throw new CustomException("The Filters attribute is required when declaring a logic");

           binaryExpresioFilter = CreateFilterExpression(filter.Logic, filter.Filters, parameter);
         }
            else
            {
var filterValid = GetValidFilter(filter);
                binaryExpresioFilter = CreateFilterExpression(
      filterValid.Field!,
  filterValid.Operator!,
        filterValid.Value,
     parameter);
      }

   ((List<WhereExpressionInfo<T>>)specificationBuilder.Specification.WhereExpressions)
           .Add(new WhereExpressionInfo<T>(
            Expression.Lambda<Func<T, bool>>(binaryExpresioFilter, parameter)));
  }

        return new OrderedSpecificationBuilder<T>(specificationBuilder.Specification);
    }

    /// <summary>
    /// Build filter expression t? Logic và Filters (recursive).
    /// </summary>
    private static Expression CreateFilterExpression(
        string logic,
  IEnumerable<Filter> filters,
    ParameterExpression parameter)
    {
        Expression filterExpression = default!;

        foreach (var filter in filters)
        {
Expression bExpresionFilter;

            if (!string.IsNullOrEmpty(filter.Logic))
            {
     if (filter.Filters is null)
             throw new CustomException("The Filters attribute is required when declaring a logic");

        bExpresionFilter = CreateFilterExpression(filter.Logic, filter.Filters, parameter);
            }
            else
     {
        var filterValid = GetValidFilter(filter);
  bExpresionFilter = CreateFilterExpression(
            filterValid.Field!,
        filterValid.Operator!,
        filterValid.Value,
  parameter);
       }

          filterExpression = filterExpression is null
 ? bExpresionFilter
     : CombineFilter(logic, filterExpression, bExpresionFilter);
        }

        return filterExpression;
    }

    /// <summary>
    /// Build filter expression t? Field, Operator, Value.
    /// </summary>
    private static Expression CreateFilterExpression(
        string field,
 string filterOperator,
 object? value,
        ParameterExpression parameter)
    {
        var propertyExpresion = GetPropertyExpression(field, parameter);
        var valueExpresion = GeValuetExpression(field, value, propertyExpresion.Type);
        return CreateFilterExpression(propertyExpresion, valueExpresion, filterOperator);
    }

    /// <summary>
    /// Build binary expression t? member expression và constant expression.
    /// </summary>
    private static Expression CreateFilterExpression(
   Expression memberExpression,
Expression constantExpression,
        string filterOperator)
    {
        // Case-insensitive string comparison
        if (memberExpression.Type == typeof(string))
      {
       constantExpression = Expression.Call(constantExpression, "ToLower", null);
            memberExpression = Expression.Call(memberExpression, "ToLower", null);
        }

        return filterOperator switch
        {
  FilterOperator.EQ => Expression.Equal(memberExpression, constantExpression),
  FilterOperator.NEQ => Expression.NotEqual(memberExpression, constantExpression),
         FilterOperator.LT => Expression.LessThan(memberExpression, constantExpression),
      FilterOperator.LTE => Expression.LessThanOrEqual(memberExpression, constantExpression),
     FilterOperator.GT => Expression.GreaterThan(memberExpression, constantExpression),
            FilterOperator.GTE => Expression.GreaterThanOrEqual(memberExpression, constantExpression),
       FilterOperator.CONTAINS => Expression.Call(memberExpression, "Contains", null, constantExpression),
         FilterOperator.STARTSWITH => Expression.Call(memberExpression, "StartsWith", null, constantExpression),
            FilterOperator.ENDSWITH => Expression.Call(memberExpression, "EndsWith", null, constantExpression),
 _ => throw new CustomException("Filter Operator is not valid."),
      };
    }

    /// <summary>
    /// K?t h?p hai expressions v?i logic operator.
    /// </summary>
    private static Expression CombineFilter(
      string filterOperator,
   Expression bExpresionBase,
        Expression bExpresion) => filterOperator switch
        {
      FilterLogic.AND => Expression.And(bExpresionBase, bExpresion),
    FilterLogic.OR => Expression.Or(bExpresionBase, bExpresion),
  FilterLogic.XOR => Expression.ExclusiveOr(bExpresionBase, bExpresion),
     _ => throw new ArgumentException("FilterLogic is not valid."),
 };

    #endregion

    #region OrderBy Methods

    /// <summary>
    /// Extension method ?? apply ordering vào specification.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> OrderBy<T>(
     this ISpecificationBuilder<T> specificationBuilder,
        string[]? orderByFields)
    {
        if (orderByFields is not null)
        {
      foreach (var field in ParseOrderBy(orderByFields))
       {
             var paramExpr = Expression.Parameter(typeof(T));

    Expression propertyExpr = paramExpr;
   foreach (string member in field.Key.Split('.'))
        {
  propertyExpr = Expression.PropertyOrField(propertyExpr, member);
     }

       var keySelector = Expression.Lambda<Func<T, object?>>(
         Expression.Convert(propertyExpr, typeof(object)),
 paramExpr);

    ((List<OrderExpressionInfo<T>>)specificationBuilder.Specification.OrderExpressions)
               .Add(new OrderExpressionInfo<T>(keySelector, field.Value));
       }
        }

        return new OrderedSpecificationBuilder<T>(specificationBuilder.Specification);
    }

 /// <summary>
    /// Parse orderByFields array thành Dictionary.
    /// </summary>
    private static Dictionary<string, OrderTypeEnum> ParseOrderBy(string[] orderByFields) =>
        new(orderByFields.Select((orderByfield, index) =>
        {
      string[] fieldParts = orderByfield.Split(' ');
    string field = fieldParts[0];
       bool descending = fieldParts.Length > 1 &&
    fieldParts[1].StartsWith("Desc", StringComparison.OrdinalIgnoreCase);

        var orderBy = index == 0
      ? descending ? OrderTypeEnum.OrderByDescending : OrderTypeEnum.OrderBy
  : descending ? OrderTypeEnum.ThenByDescending : OrderTypeEnum.ThenBy;

       return new KeyValuePair<string, OrderTypeEnum>(field, orderBy);
      }));

    #endregion

    #region Helper Methods

    /// <summary>
    /// Build property expression t? property name (support nested properties).
    /// </summary>
    private static MemberExpression GetPropertyExpression(
      string propertyName,
        ParameterExpression parameter)
    {
   Expression propertyExpression = parameter;

    foreach (string member in propertyName.Split('.'))
        {
    propertyExpression = Expression.PropertyOrField(propertyExpression, member);
        }

return (MemberExpression)propertyExpression;
    }

    /// <summary>
    /// Extract string t? JsonElement.
    /// </summary>
    private static string GetStringFromJsonElement(object value)
        => ((JsonElement)value).GetString()!;

    /// <summary>
  /// Convert value thành ConstantExpression v?i type phù h?p.
    /// </summary>
    private static ConstantExpression GeValuetExpression(
   string field,
        object? value,
        Type propertyType)
    {
        if (value == null)
            return Expression.Constant(null, propertyType);

        // Handle Enum
 if (propertyType.IsEnum)
    {
            string? stringEnum = GetStringFromJsonElement(value);
        if (!Enum.TryParse(propertyType, stringEnum, true, out object? valueparsed))
    throw new CustomException($"Value {value} is not valid for {field}");
      return Expression.Constant(valueparsed, propertyType);
        }

        // Handle Guid
     if (propertyType == typeof(Guid))
        {
    string? stringGuid = GetStringFromJsonElement(value);
  if (!Guid.TryParse(stringGuid, out Guid valueparsed))
                throw new CustomException($"Value {value} is not valid for {field}");
            return Expression.Constant(valueparsed, propertyType);
        }

        // Handle String
     if (propertyType == typeof(string))
        {
    string? text = GetStringFromJsonElement(value);
         return Expression.Constant(text, propertyType);
     }

     // Handle DateTime
        if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
    {
     string? text = GetStringFromJsonElement(value);
   return Expression.Constant(ChangeType(text, propertyType), propertyType);
        }

        // Handle other types
     return Expression.Constant(
         ChangeType(((JsonElement)value).GetRawText(), propertyType),
        propertyType);
    }

    /// <summary>
    /// Convert value sang type khác (handle Nullable types).
    /// </summary>
    public static dynamic? ChangeType(object value, Type conversion)
    {
        var t = conversion;

        if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
        {
            if (value == null)
        {
         return null;
     }

            t = Nullable.GetUnderlyingType(t);
        }

        return Convert.ChangeType(value, t!);
    }

    /// <summary>
    /// Validate Filter object.
    /// </summary>
    private static Filter GetValidFilter(Filter filter)
    {
     if (string.IsNullOrEmpty(filter.Field))
         throw new CustomException("The field attribute is required when declaring a filter");

        if (string.IsNullOrEmpty(filter.Operator))
       throw new CustomException("The Operator attribute is required when declaring a filter");

        return filter;
    }

    #endregion
}
```

---

## 4. Gi?i thích Chi ti?t

### 4.1: Expression Trees

**Expression Trees là gì?**
- C?u trúc data ?? represent code as data
- Có th? build, modify, compile runtime
- Dùng ?? t?o dynamic LINQ queries

**Example:**
```csharp
// Thay vì vi?t:
Func<Product, bool> lambda = p => p.Price > 100;

// Ta build Expression Tree:
var parameter = Expression.Parameter(typeof(Product), "p");
var property = Expression.Property(parameter, "Price");
var constant = Expression.Constant(100m);
var greaterThan = Expression.GreaterThan(property, constant);
var lambda = Expression.Lambda<Func<Product, bool>>(greaterThan, parameter);
// => p => p.Price > 100
```

---

### 4.2: SearchBy Flow

```
SearchBy(filter)
    ?
SearchByKeyword(filter.Keyword)
    ? AdvancedSearch v?i Search { Keyword = keyword }
    ? Loop primitive fields
    ? AddSearchPropertyByKeyword cho m?i field
    ? Build Expression: x => x.Property.ToLower().Contains(keyword)
        ? Add vào SearchCriterias
    ?
AdvancedSearch(filter.AdvancedSearch)
    ? N?u có Fields: Loop fields ch? ??nh
    ? N?u không: Loop t?t c? primitive fields
    ? AddSearchPropertyByKeyword cho m?i field
    ?
AdvancedFilter(filter.AdvancedFilter)
    ? N?u có Logic: CreateFilterExpression (recursive)
    ? N?u không: CreateFilterExpression ??n gi?n
    ? Build Expression: x => x.Field Operator Value
  ? Add vào WhereExpressions
```

---

### 4.3: Advanced Filter Flow

**Simple filter:**
```json
{ "field": "Price", "operator": "gte", "value": 1000 }
```

**Flow:**
```
1. GetValidFilter ? Validate Field và Operator
2. GetPropertyExpression("Price", parameter)
   ? x.Price (MemberExpression)
3. GeValuetExpression("Price", 1000, typeof(decimal))
   ? Expression.Constant(1000m, typeof(decimal))
4. CreateFilterExpression(x.Price, 1000m, "gte")
   ? Expression.GreaterThanOrEqual(x.Price, 1000m)
5. Wrap trong lambda: x => x.Price >= 1000m
6. Add vào WhereExpressions
```

**Complex filter v?i logic:**
```json
{
  "logic": "and",
  "filters": [
    { "field": "Price", "operator": "gte", "value": 1000 },
{ "field": "Stock", "operator": "gt", "value": 0 }
  ]
}
```

**Flow:**
```
1. Detect Logic="and" ? Recursive call
2. Loop filters:
   a) Filter 1: x.Price >= 1000
   b) Filter 2: x.Stock > 0
3. CombineFilter("and", filter1Expression, filter2Expression)
   ? Expression.And(x.Price >= 1000, x.Stock > 0)
4. Result: x => (x.Price >= 1000) && (x.Stock > 0)
```

---

### 4.4: OrderBy Flow

**Input:**
```csharp
orderBy = ["Name", "Price Desc", "Category.Name"]
```

**ParseOrderBy:**
```
"Name"     ? (Name, OrderBy)          // First field, ascending
"Price Desc" ? (Price, ThenByDescending) // Second field, descending
"Category.Name" ? (Category.Name, ThenBy)     // Third field, ascending
```

**Build Expressions:**
```csharp
// 1. Name
x => (object)x.Name  // OrderBy

// 2. Price Desc
x => (object)x.Price // ThenByDescending

// 3. Category.Name
x => (object)x.Category.Name // ThenBy (nested property)
```

**SQL Result:**
```sql
ORDER BY Name ASC, Price DESC, CategoryName ASC
```

---

## 5. Usage Examples

### Example 1: Simple keyword search
```csharp
var spec = new Specification<Product>();
spec.Query.SearchByKeyword("iphone");

// SQL: WHERE Name LIKE '%iphone%' OR Description LIKE '%iphone%' OR ...
```

### Example 2: Advanced search v?i fields
```csharp
var search = new Search
{
    Keyword = "pro",
    Fields = new[] { "Name", "Description", "Brand.Name" }
};

var spec = new Specification<Product>();
spec.Query.AdvancedSearch(search);

// SQL: WHERE Name LIKE '%pro%' OR Description LIKE '%pro%' OR BrandName LIKE '%pro%'
```

### Example 3: Complex filter
```csharp
var filter = new Filter
{
    Logic = "and",
    Filters = new List<Filter>
    {
        new() { Field = "Price", Operator = "gte", Value = 1000 },
  new() { Field = "Price", Operator = "lte", Value = 5000 },
      new()
        {
            Logic = "or",
   Filters = new List<Filter>
{
    new() { Field = "Brand.Name", Operator = "eq", Value = "Apple" },
    new() { Field = "Brand.Name", Operator = "eq", Value = "Samsung" }
 }
        }
    }
};

var spec = new Specification<Product>();
spec.Query.AdvancedFilter(filter);

// SQL: WHERE (Price >= 1000 AND Price <= 5000) 
//        AND (BrandName = 'Apple' OR BrandName = 'Samsung')
```

### Example 4: Complete with pagination
```csharp
var request = new PaginationFilter
{
    PageNumber = 2,
    PageSize = 10,
    Keyword = "phone",
  OrderBy = new[] { "Name", "Price Desc" },
    AdvancedFilter = new Filter
    {
   Field = "IsActive",
    Operator = "eq",
        Value = true
    }
};

var spec = new Specification<Product>();
spec.Query
    .SearchBy(request)
    .PaginateBy(request);

// SQL: WHERE (Name LIKE '%phone%' OR ...) AND IsActive = 1
//      ORDER BY Name ASC, Price DESC
//      OFFSET 10 ROWS FETCH NEXT 10 ROWS ONLY
```

---

## 6. Common Pitfalls

### Issue 1: Nested property không t?n t?i
```json
{
  "field": "Category.Name",  // Category có th? null!
  "operator": "eq",
  "value": "Electronics"
}
```

**Solution:** Include Category tr??c khi filter:
```csharp
Query.Include(p => p.Category)
     .AdvancedFilter(filter);
```

---

### Issue 2: JsonElement type mismatch
```json
{
  "field": "Price",
  "operator": "gte",
  "value": "1000"  // String thay vì number!
}
```

**Solution:** `GeValuetExpression` t? ??ng parse:
```csharp
// "1000" ? Convert.ChangeType("1000", typeof(decimal)) ? 1000m
```

---

### Issue 3: Enum value invalid
```json
{
  "field": "Status",
  "operator": "eq",
  "value": "InvalidStatus"  // Enum không có value này!
}
```

**Error:** `CustomException: Value InvalidStatus is not valid for Status`

**Solution:** Validate enum values tr??c khi g?i request.

---

## 7. Performance Considerations

### 7.1: Search trong t?t c? fields
```csharp
// ? Slow: Search 20 fields
Query.SearchByKeyword("test");

// ? Faster: Ch? ??nh fields c?n search
Query.AdvancedSearch(new Search 
{ 
    Keyword = "test", 
 Fields = new[] { "Name", "Description" } 
});
```

---

### 7.2: Complex nested filters
```csharp
// ? Deep nesting = complex SQL
{
  "logic": "and",
  "filters": [
    { "logic": "or", "filters": [...] },
 { "logic": "or", "filters": [...] },
    { "logic": "or", "filters": [...] }
  ]
}

// ? Flatten n?u có th?
```

---

### 7.3: Include related entities
```csharp
// ?? Filtering nested properties REQUIRES Include
Query
    .Include(p => p.Category)        // Required!
    .AdvancedFilter(new Filter 
    { 
        Field = "Category.Name", 
        Operator = "eq", 
        Value = "Electronics" 
    });
```

---

## 8. Testing

### Unit Test Example
```csharp
[Fact]
public async Task SearchBy_WithKeyword_ShouldFilter()
{
    // Arrange
    var filter = new PaginationFilter
    {
        Keyword = "iphone",
     PageNumber = 1,
      PageSize = 10
    };

    var spec = new Specification<Product>();
    spec.Query.SearchBy(filter).PaginateBy(filter);

    // Act
    var products = await _repository.ListAsync(spec);

    // Assert
    Assert.All(products, p =>
    Assert.Contains("iphone", p.Name, StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void AdvancedFilter_WithLogic_ShouldBuildCorrectExpression()
{
    // Arrange
    var filter = new Filter
    {
        Logic = "and",
        Filters = new List<Filter>
    {
            new() { Field = "Price", Operator = "gte", Value = 1000 },
            new() { Field = "Stock", Operator = "gt", Value = 0 }
        }
    };

    var spec = new Specification<Product>();
 spec.Query.AdvancedFilter(filter);

    // Act
    var expression = spec.WhereExpressions.First();

    // Assert
    // x => (x.Price >= 1000) && (x.Stock > 0)
    Assert.NotNull(expression);
}
```

---

## 9. Summary

### ?? Key Points:

**Expression Trees:**
- Build dynamic LINQ queries runtime
- Type-safe và compile-time checked
- D? debug v?i Expression.ToString()

**SearchBy:**
- Keyword search (simple)
- Advanced search (fields-specific)
- Advanced filter (operators + logic)

**PaginateBy:**
- Skip/Take for pagination
- OrderBy/ThenBy for sorting
- Support nested properties

**Filter Operators:**
- Comparison: eq, neq, lt, lte, gt, gte
- String: contains, startswith, endswith

**Filter Logic:**
- AND, OR, XOR
- Recursive nesting support

### ?? Files Created:

```
src/Core/Application/Common/
??? Models/
?   ??? Filter.cs (updated with FilterOperator & FilterLogic)
??? Specification/
    ??? SpecificationBuilderExtensions.cs (full code)
```

---

**Quay l?i:** [BUILD_11 - Repository Pattern](BUILD_11_Repository_Pattern.md)
