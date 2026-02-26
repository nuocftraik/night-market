# Property Expressions (Expression Trees) — Giải thích chi tiết “cách code từng hàm”

Tài liệu này đi sâu vào **cách viết code** để build `PropertyExpression` và dùng nó cho **search/filter/order** (đặc biệt nested field như `Category.Name`).
Expression: biểu thức.
ParameterExpression: đại diện cho tham số lambda, ví dụ x trong x => x.Price.
MemberExpression: ví dụ x.Price hoặc x.Category.Name. Đây là loại expression bạn nhận được sau khi nối các property/field.
ConstantExpression: Giá trị hằng, ví dụ số 1000 hay chuỗi "iphone".
LambdaExpression / Expression<Func<T, …>>: Toàn bộ lambda, ví dụ x => x.Price > 1000. EF dùng để dịch sang SQL.
Hình dung nhanh:
Bắt đầu: ParameterExpression x   = biến đầu vào.
Truy cập: MemberExpression x.Price   = thuộc tính/field trên biến đó.
So sánh: BinaryExpression x.Price > 1000
Bọc lại: LambdaExpression x => x.Price > 1000       cả công thức hoàn chỉnh.
---

## 1) PropertyExpression là gì?
- Là biểu thức đại diện cho việc truy cập property của một object trong Expression Tree.
- Ví dụ đơn giản: với parameter `x`, property `Name` → expression tương đương `x => x.Name`.

### Nested property
- Nếu property có dạng `Category.Name`, ta cần đi qua từng bước:
  1. Bắt đầu từ parameter `x`.
  2. Lấy `x.Category`.
  3. Lấy tiếp `.Name` → `x.Category.Name`.
- Kết quả: `MemberExpression` trỏ tới property cuối cùng.

---

## 2) Tại sao phải build PropertyExpression?
- Để tạo các biểu thức filter/search động (dynamic) mà không cần viết code cứng.
- EF Core sẽ chuyển Expression Tree thành SQL tương ứng (có include nested property).
- Hỗ trợ filter/search nhiều field, kể cả nested, mà payload chỉ cần gửi `"Category.Name"`.

---

## 3) Hàm “core” để build nested property: `GetPropertyExpression`

Đây là hàm đúng kiểu bạn đang dùng trong project (`SpecificationBuilderExtensions.cs`). Mình viết lại y chang + comment từng dòng.

```csharp
/// <summary>
/// Build MemberExpression từ string field name (support nested: "Category.Name").
/// </summary>
private static MemberExpression GetPropertyExpression(
    string propertyName,
    MemberExpression parameter)
{
    // Bước 1: bắt đầu từ parameter "x"
    Expression propertyExpression = parameter;
 
    // Bước 2: split theo dấu '.' để đi theo từng level
    // "Category.Name" => ["Category", "Name"]
    foreach (string member in propertyName.Split('.'))
    {
        // Bước 3: mỗi vòng lặp, build tiếp expression:
        // x => x.Category (vòng 1)
        // x => x.Category.Name (vòng 2)
        propertyExpression = Expression.PropertyOrField(propertyExpression, member);
    }

    // Bước 4: kết quả cuối cùng luôn là MemberExpression (Property/Field)
    return (MemberExpression)propertyExpression;
}
```

### Debug tip
- Nếu field sai tên (ví dụ `"Categry.Name"`), `Expression.PropertyOrField` sẽ throw exception ngay.

---

## 4) Từ PropertyExpression → tạo Selector (phục vụ Search)

Trong Ardalis.Specification, search criteria cần **selector** dạng `Expression<Func<T, string>>`.

### 4.1) Vì sao phải selector về string?
- Search dùng `%keyword%` (LIKE) nên cần string.
- Nếu property không phải string (int, decimal, Guid...), code sẽ gọi `.ToString()` để biến thành string.

### 4.2) Hàm build selector “string hóa + toLower”

Đây là ý tưởng y hệt `AddSearchPropertyByKeyword` trong project.

```csharp
/// <summary>
/// Build selector expression: x => x.Prop.ToLower()
/// hoặc x => (x.Prop == null ? null : x.Prop.ToString()).ToLower()
/// </summary>
private static Expression<Func<T, string>> BuildSearchSelector<T>(
    MemberExpression propertyExpr,
    ParameterExpression paramExpr)
{
    // Nếu property đã là string: dùng trực tiếp x.Prop
    Expression selectorExpr =
        propertyExpr.Type == typeof(string)
            ? propertyExpr
            : Expression.Condition(
                // (object)x.Prop == null ?
                Expression.Equal(
                    Expression.Convert(propertyExpr, typeof(object)),
                    Expression.Constant(null, typeof(object))),
                // null
                Expression.Constant(null, typeof(string)),
                // x.Prop.ToString()
                Expression.Call(propertyExpr, "ToString", null, null));

    // selectorExpr.ToLower()
    var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
    Expression lowerExpr = Expression.Call(selectorExpr, toLowerMethod);

    // x => lowerExpr
    return Expression.Lambda<Func<T, string>>(lowerExpr, paramExpr);
}
```

### 4.3) Tạo pattern `%keyword%`

```csharp
private static string BuildLikePattern(string keyword, string op)
{
    var k = keyword.ToLower();
    return op switch
    {
        "startswith" => $"{k}%",
        "endswith" => $"%{k}",
        "contains" => $"%{k}%",
        _ => throw new ArgumentException("Invalid search operator")
    };
}
```

---

## 5) Từ PropertyExpression → tạo Predicate (phục vụ Filter)

Filter cần predicate dạng `Expression<Func<T, bool>>` (Where).

### 5.1) Convert JSON value → ConstantExpression (đúng kiểu)

Project đang dùng `JsonElement` cho value. Ta cần convert value sang đúng type của property.

```csharp
private static ConstantExpression BuildConstant(
    string field,
    object? value,
    Type propertyType)
{
    if (value is null)
        return Expression.Constant(null, propertyType);

    // value thường là JsonElement
    var json = (JsonElement)value;

    // Enum
    if (propertyType.IsEnum)
    {
        var s = json.GetString();
        if (!Enum.TryParse(propertyType, s, true, out var parsed))
            throw new Exception($"Value {s} invalid for {field}");
        return Expression.Constant(parsed, propertyType);
    }

    // Guid
    if (propertyType == typeof(Guid))
    {
        var s = json.GetString();
        if (!Guid.TryParse(s, out var g))
            throw new Exception($"Value {s} invalid for {field}");
        return Expression.Constant(g, propertyType);
    }

    // string
    if (propertyType == typeof(string))
        return Expression.Constant(json.GetString(), propertyType);

    // DateTime / DateTime?
    if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        return Expression.Constant(Convert.ChangeType(json.GetString()!, Nullable.GetUnderlyingType(propertyType) ?? propertyType), propertyType);

    // số, bool... dùng raw text convert
    return Expression.Constant(Convert.ChangeType(json.GetRawText(), Nullable.GetUnderlyingType(propertyType) ?? propertyType), propertyType);
}
```

### 5.2) Build comparison expression theo operator

```csharp
private static Expression BuildCompareExpression(
    Expression memberExpr,
    Expression constExpr,
    string op)
{
    // Nếu string => lower-case để case-insensitive
    if (memberExpr.Type == typeof(string))
    {
        memberExpr = Expression.Call(memberExpr, "ToLower", null);
        constExpr = Expression.Call(constExpr, "ToLower", null);
    }

    return op switch
    {
        "eq" => Expression.Equal(memberExpr, constExpr),
        "neq" => Expression.NotEqual(memberExpr, constExpr),
        "gt" => Expression.GreaterThan(memberExpr, constExpr),
        "gte" => Expression.GreaterThanOrEqual(memberExpr, constExpr),
        "lt" => Expression.LessThan(memberExpr, constExpr),
        "lte" => Expression.LessThanOrEqual(memberExpr, constExpr),
        "contains" => Expression.Call(memberExpr, "Contains", null, constExpr),
        "startswith" => Expression.Call(memberExpr, "StartsWith", null, constExpr),
        "endswith" => Expression.Call(memberExpr, "EndsWith", null, constExpr),
        _ => throw new Exception("Invalid operator")
    };
}
```

### 5.3) Ghép lại: build predicate từ field/operator/value

```csharp
public static Expression<Func<T, bool>> BuildPredicate<T>(
    string field,
    string op,
    object? value)
{
    // x
    var param = Expression.Parameter(typeof(T), "x");

    // x.Category.Name (hoặc x.Price)
    var member = GetPropertyExpression(field, param);

    // constant đúng kiểu (string/enum/guid/number...)
    var constant = BuildConstant(field, value, member.Type);

    // x.Price >= 1000 (hoặc x.Category.Name.Contains("phone"))
    var body = BuildCompareExpression(member, constant, op);

    // x => body
    return Expression.Lambda<Func<T, bool>>(body, param);
}
```

---

## 6) Nested filter mẫu (giống project)

Payload:
```json
{
  "advancedFilter": {
    "logic": "and",
    "filters": [
      { "field": "Category.Name", "operator": "contains", "value": "phone" },
      { "field": "Brand.Name", "operator": "eq", "value": "apple" }
    ]
  }
}
```

Tương đương predicate:
- `x => x.Category.Name.Contains("phone") && x.Brand.Name.ToLower() == "apple"`

---

## 7) Nối nhiều điều kiện bằng AND/OR (giống AdvancedFilter)

```csharp
private static Expression Combine(Expression left, Expression right, string logic) =>
    logic switch
    {
        "and" => Expression.And(left, right),
        "or" => Expression.Or(left, right),
        "xor" => Expression.ExclusiveOr(left, right),
        _ => throw new Exception("Invalid logic")
    };
```

**Lưu ý:** `Expression.And/Or` là bitwise AND/OR (EF vẫn dịch được). Nếu muốn short-circuit thì dùng `AndAlso/OrElse`.

---

## 5) Xử lý type & null
- Khi build selector cho search:
  - Nếu property là string: dùng trực tiếp.
  - Nếu không phải string: ToString() và xử lý null: `x.Prop == null ? null : x.Prop.ToString()`.
  - Convert về lower-case để search không phân biệt hoa/thường.
- Khi filter:
  - Nếu property là string: chuyển cả property và value về lower-case để so sánh không phân biệt hoa/thường.
  - Enum/Guid/DateTime: parse từ payload (JsonElement) sang type tương ứng; sai định dạng → throw CustomException.

---

## 6) Lợi ích
- Viết một lần, dùng cho mọi entity, kể cả nested properties.
- Payload chỉ cần gửi tên field dạng chuỗi, code sẽ tự build expression.
- Giảm code lặp, tăng độ linh hoạt của search/filter/pagination.

---

## 7) Khi debug
- Kiểm tra chuỗi field: `"Category.Name"` có đúng tên property không.
- Với enum/guid/datetime: đảm bảo payload gửi đúng format (nếu không sẽ bị CustomException).
- Kiểm tra operator: `eq, neq, gt, gte, lt, lte, contains, startswith, endswith`.

---

## 8) Liên hệ trực tiếp với file trong project

Trong `src/Core/Application/Common/Specification/SpecificationBuilderExtensions.cs`, các hàm liên quan property expression là:
- `GetPropertyExpression` → build `x.Category.Name`.
- `AddSearchPropertyByKeyword` → dùng property expression để tạo `SearchExpressionInfo` (search).
- `CreateFilterExpression(string field, ...)` → dùng property expression + constant expression để tạo predicate (filter).
- `OrderBy` → dùng property expression + keySelector để tạo order expressions.

---

*Nếu bạn muốn, mình có thể tạo thêm 1 file “mini-demo” (pseudo code) mô phỏng chạy 1 request payload qua các bước Search → Filter → OrderBy để bạn debug dễ hơn.*
