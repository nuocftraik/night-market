namespace NightMarket.WebApi.Application.Common.Models;

/// <summary>
/// Advanced filter với operators và logic
/// </summary>
public class Filter
{
    /// <summary>
    /// Logic operator: "and", "or", "xor" (dùng khi có nhiều filters)
    /// </summary>
    public string? Logic { get; set; }
    
    /// <summary>
    /// Field name (support nested: "Category.Name")
    /// </summary>
    public string? Field { get; set; }
    
    /// <summary>
    /// Operator: "eq", "neq", "gt", "gte", "lt", "lte", "contains", "startswith", "endswith"
    /// </summary>
    public string? Operator { get; set; }
    
    /// <summary>
    /// Value để compare
    /// </summary>
    public object? Value { get; set; }
    
    /// <summary>
    /// Nested filters (dùng khi có Logic)
    /// </summary>
    public List<Filter>? Filters { get; set; }
}
