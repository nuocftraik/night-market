using System.Security.Claims;
using NightMarket.WebApi.Application.Identity.Users;
using Microsoft.AspNetCore.Authorization;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Trình xử lý authorization cho yêu cầu quyền
/// Kiểm tra xem user có quyền yêu cầu trong JWT claims không
/// </summary>
internal class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserService _userService;

    public PermissionAuthorizationHandler(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Xử lý yêu cầu quyền
    /// Kiểm tra xem user có quyền yêu cầu không
    /// </summary>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        PermissionRequirement requirement)
    {
        if (context.User?.GetUserId() is { } userId &&
            await _userService.HasPermissionAsync(userId, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
