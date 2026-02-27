using NightMarket.WebApi.Application.Identity.Users;
using NightMarket.WebApi.Application.Identity.Users.Password;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NightMarket.WebApi.Host.Controllers.Personal;

/// <summary>
/// Personal Controller - Current user profile management
/// Endpoints: Profile, Change password, Get permissions
/// </summary>
public class PersonalController : BaseApiController
{
    private readonly IUserService _userService;

    public PersonalController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lấy profile của current logged-in user
    /// </summary>
    [HttpGet("profile")]
    public async Task<ActionResult<UserDetailDto>> GetProfileAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var profile = await _userService.GetAsync(userId, cancellationToken);
        return Ok(profile);
    }

    /// <summary>
    /// Cập nhật profile của current logged-in user
    /// </summary>
    [HttpPut("profile")]
    public async Task<ActionResult> UpdateProfileAsync(UpdateUserRequest request)
    {
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        await _userService.UpdateAsync(request, userId);
        return Ok(new { message = "Profile updated successfully" });
    }

    /// <summary>
    /// Đổi password của current logged-in user
    /// </summary>
    [HttpPut("change-password")]
    public async Task<ActionResult> ChangePasswordAsync(ChangePasswordRequest model)
    {
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        await _userService.ChangePasswordAsync(model, userId);
        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>
    /// Lấy danh sách permissions của current logged-in user
    /// </summary>
    [HttpGet("permissions")]
    public async Task<ActionResult<List<string>>> GetPermissionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var permissions = await _userService.GetPermissionsAsync(userId, cancellationToken);
        return Ok(permissions);
    }
}
