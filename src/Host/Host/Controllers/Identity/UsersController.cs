using NightMarket.WebApi.Application.Identity.Users;
using NightMarket.WebApi.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NightMarket.WebApi.Host.Controllers.Identity;

/// <summary>
/// Controller cho user management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Tìm kiếm users với pagination
    /// </summary>
    [HttpGet("search")]
    // [Authorize(Policy = "admin")] // Uncomment when policy is ready
    public async Task<IActionResult> SearchUsers(
        [FromQuery] UserParameterFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _userService.SearchAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get user details by ID
    /// </summary>
    [HttpGet("{id}")]
    // [Authorize(Policy = "admin")] // Uncomment when policy is ready
    public async Task<IActionResult> GetUserById(string id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin tạo user mới
    /// </summary>
    [HttpPost("create")]
    // [Authorize(Policy = "admin")] // Uncomment when policy is ready
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request, 
        CancellationToken cancellationToken)
    {
        var message = await _userService.CreateAsync(request, "localhost");
        return Ok(message);
    }

    /// <summary>
    /// Self-register tài khoản mới
    /// </summary>
    [HttpPost("self-register")]
    public async Task<IActionResult> SelfRegister(
        [FromBody] CreateUserRequest request, 
        CancellationToken cancellationToken)
    {
        var message = await _userService.CreateAsync(request, "localhost");
        return Ok(message);
    }

    /// <summary>
    /// Cập nhật user profile (basic info)
    /// </summary>
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateUser(
        string id, 
        [FromBody] UpdateUserRequest request, 
        CancellationToken cancellationToken)
    {
        await _userService.UpdateAsync(request, id);
        return Ok();
    }

    /// <summary>
    /// Admin kích hoạt/deactivate user
    /// </summary>
    [HttpPost("toggle-status")]
    // [Authorize(Policy = "admin")] // Uncomment when policy is ready
    public async Task<IActionResult> ToggleUserStatus(
        [FromBody] ToggleUserStatusRequest request, 
        CancellationToken cancellationToken)
    {
        await _userService.ToggleStatusAsync(request, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Xác nhận email
    /// </summary>
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _userService.ConfirmEmailAsync(request.UserId, request.Code, cancellationToken);
        return Ok(message);
    }

    /// <summary>
    /// Quên mật khẩu - gửi email xác nhận
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] NightMarket.WebApi.Application.Identity.Users.Password.ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _userService.ForgotPasswordAsync(request, "localhost");
        return Ok(message);
    }
}
