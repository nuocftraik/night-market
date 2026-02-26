namespace NightMarket.WebApi.Domain.Identity;

/// <summary>
/// Maps which actions are available for each function.
/// </summary>
public class ActionInFunction
{
    public string ActionId { get; set; } = default!;
    public string FunctionId { get; set; } = default!;

    public virtual Action Action { get; set; } = default!;
    public virtual Function Function { get; set; } = default!;
}
