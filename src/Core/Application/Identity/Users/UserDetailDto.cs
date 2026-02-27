namespace NightMarket.WebApi.Application.Identity.Users;

/// <summary>
/// User detail DTO (dùng cho responses)
/// </summary>
public class UserDetailDto
{
    /// <summary>
    /// User ID (Guid)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Username (unique)
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Is user active (can login)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Has email been confirmed
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Avatar image URL
    /// </summary>
    public string? ImageUrl { get; set; }
}
