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

    public virtual ICollection<ActionInFunction> ActionInFunctions { get; set; } = new List<ActionInFunction>();

    public Function()
    {
    }

    public void AddAction(string actionId)
    {
        ActionInFunctions.Add(new ActionInFunction(actionId, Id));
    }

    public void UpdateActions(List<string>? newActionIds)
    {
        if (newActionIds == null || newActionIds.Count == 0)
        {
            ActionInFunctions.Clear();
            return;
        }

        var toRemove = ActionInFunctions.Where(aif => !newActionIds.Contains(aif.ActionId)).ToList();
        foreach (var item in toRemove)
        {
            ActionInFunctions.Remove(item);
        }

        var existingActionIds = ActionInFunctions.Select(aif => aif.ActionId).ToHashSet();
        foreach (var actionId in newActionIds)
        {
            if (!existingActionIds.Contains(actionId))
            {
                ActionInFunctions.Add(new ActionInFunction(actionId, Id));
            }
        }
    }
}
