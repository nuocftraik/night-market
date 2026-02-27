namespace NightMarket.WebApi.Application.Common.Models;

/// <summary>
/// Base filter cho mọi search requests
/// </summary>
public class BaseFilter
{
    /// <summary>
    /// Simple keyword search (search trong tất cả fields)
    /// </summary>
    public string? Keyword { get; set; }
    
    /// <summary>
    /// Advanced search với fields cụ thể
    /// </summary>
    public Search? AdvancedSearch { get; set; }
    
    /// <summary>
    /// Advanced filter với operators và logic
    /// </summary>
    public Filter? AdvancedFilter { get; set; }
}
