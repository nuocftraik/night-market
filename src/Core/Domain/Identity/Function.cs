namespace NightMarket.WebApi.Domain.Identity;

/// <summary>
/// Represents a system function/module (Users, Products, Dashboard, etc.).
/// </summary>
public class Function
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ParentId { get; set; }
    public int SortOrder { get; set; }
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}
