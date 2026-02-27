using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Domain.Identity;

/// <summary>
/// Maps which actions are available for each function.
/// </summary>
[PrimaryKey(nameof(ActionId), nameof(FunctionId))]
public class ActionInFunction
{
    public string ActionId { get; set; } = default!;
    public string FunctionId { get; set; } = default!;

    public virtual Action Action { get; set; } = default!;
    public virtual Function Function { get; set; } = default!;

    public ActionInFunction()
    {
    }

    public ActionInFunction(string actionId, string functionId)
    {
        ActionId = actionId;
        FunctionId = functionId;
    }
}
