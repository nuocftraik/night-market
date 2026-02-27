namespace NightMarket.WebApi.Application.Common.Models;

/// <summary>
/// Advanced search với keyword trong các fields cụ thể
/// </summary>
public class Search
{
    /// <summary>
    /// Keyword để search
    /// </summary>
    public string? Keyword { get; set; }
    
    /// <summary>
    /// Danh sách fields để search (nếu null thì search tất cả fields)
    /// Support nested: "Category.Name"
    /// </summary>
    public string[]? Fields { get; set; }
}
