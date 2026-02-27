namespace NightMarket.WebApi.Application.Identity.Users;

/// <summary>
/// Request để toggle user active status
/// </summary>
public class ToggleUserStatusRequest
{
    /// <summary>
    /// User ID to toggle
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// True = activate, False = deactivate
    /// </summary>
    public bool ActivateUser { get; set; }
}
