namespace NightMarket.WebApi.Application.Common.Models;

/// <summary>
/// Pagination filter kế thừa BaseFilter, thêm pagination và sorting
/// </summary>
public class PaginationFilter : BaseFilter
{
    /// <summary>
    /// Page number (bắt đầu từ 1)
    /// </summary>
    public int PageNumber { get; set; } = 1;
    
    /// <summary>
    /// Page size (số items mỗi page)
    /// </summary>
    public int PageSize { get; set; } = 10;
    
    /// <summary>
    /// OrderBy fields: ["Name", "Price Desc", "Category.Name"]
    /// </summary>
    public string[]? OrderBy { get; set; }
}

public static class PaginationFilterExtensions
{
    public static bool HasOrderBy(this PaginationFilter filter) =>
        filter.OrderBy?.Any() is true;
}
