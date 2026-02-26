namespace NightMarket.WebApi.Domain.Identity;

/// <summary>
/// Permission entity - maps Role + Function + Action.
/// </summary>
public class Permission
{
    public string RoleId { get; set; } = default!;
    public string FunctionId { get; set; } = default!;
    public string ActionId { get; set; } = default!;

    public virtual ApplicationRole Role { get; set; } = default!;
    public virtual Function Function { get; set; } = default!;
    public virtual Action Action { get; set; } = default!;
}
