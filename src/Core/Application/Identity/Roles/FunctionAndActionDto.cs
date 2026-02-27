namespace NightMarket.WebApi.Application.Identity.Roles;

/// <summary>
/// Action DTO (represents an operation)
/// </summary>
public class ActionDto
{
    /// <summary>
    /// Action ID
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Is this action selected for current role (checkbox state)
    /// </summary>
    public bool Selected { get; set; }
}

/// <summary>
/// Function DTO (represents a module/feature)
/// </summary>
public class FunctionDto
{
    /// <summary>
    /// Function ID
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Function name (e.g., "Users", "Products", "Orders")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// List of actions available for this function
    /// </summary>
    public List<ActionDto> ActionDtos { get; set; } = default!;
}
