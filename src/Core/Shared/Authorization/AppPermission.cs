using System.Reflection;

namespace NightMarket.Shared.Authorization;

/// <summary>
/// Permission record với dynamic generation.
/// Format: "Permissions.{Function}.{Action}".
/// </summary>
public record AppPermission(string Action, string Function)
{
    public string Name => NameFor(Action, Function);

    public static string NameFor(string action, string function) =>
        $"Permissions.{function}.{action}";

    /// <summary>
    /// Generate tất cả permissions cho một function.
    /// </summary>
    public static List<string> GeneratePermissionsForFunction(string function)
    {
        var actions = typeof(AppAction)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .Select(field => field.GetValue(null)?.ToString())
            .Where(value => value != null)
            .Cast<string>()
            .ToList();

        return actions.Select(action => NameFor(action, function)).ToList();
    }

    /// <summary>
    /// Generate permissions với custom actions.
    /// </summary>
    public static List<string> GeneratePermissionsForFunction(string function, List<string> actions)
    {
        if (actions == null || actions.Count == 0)
        {
            throw new ArgumentException("Actions không được null hoặc empty", nameof(actions));
        }

        return actions.Select(action => NameFor(action, function)).ToList();
    }
}
