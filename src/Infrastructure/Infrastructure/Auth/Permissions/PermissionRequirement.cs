using Microsoft.AspNetCore.Authorization;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Yêu cầu quyền (implements IAuthorizationRequirement)
/// Đại diện cho một quyền cần được kiểm tra
/// </summary>
internal class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Chuỗi permission (định dạng: "Permissions.Function.Action")
    /// Ví dụ: "Permissions.User.View"
    /// </summary>
    public string Permission { get; private set; }

    public PermissionRequirement(string permission)
    {
         Permission = permission;
    }
}
