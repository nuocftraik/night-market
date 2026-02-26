namespace NightMarket.WebApi.Domain.Identity;

/// <summary>
/// Represents a system action (View, Create, Update, Delete, etc.).
/// </summary>
public class Action
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
