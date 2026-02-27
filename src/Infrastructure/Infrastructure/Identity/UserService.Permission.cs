using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Các Thao tác Quyền (Partial Class)
/// </summary>
internal partial class UserService
{
    /// <summary>
    /// Lấy danh sách quyền của user từ database
    /// Trả về danh sách chuỗi permission (Định dạng: "Function.Action")
    /// Lưu ý: Lưu trong bảng Permission dưới dạng (RoleId, FunctionId, ActionId)
    /// </summary>
    public async Task<List<string>> GetPermissionsAsync(
         string userId, 
         CancellationToken cancellationToken)
    {
        // Tìm user
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            throw new UnauthorizedException("Xác thực thất bại.");
        }

        // Lấy các roles của user (từ bảng UserRoles - Identity)
        var userRoles = await _userManager.GetRolesAsync(user);

        // Truy vấn permissions từ bảng Permission
        // JOIN: Permission -> Role -> Function -> Action
        var permissions = await _db.Permissions
            .Include(p => p.Role)
            .Include(p => p.Function)
            .Include(p => p.Action)
            .Where(p => userRoles.Contains(p.Role.Name!)) // Lọc theo roles của user
            .Select(p => $"{p.Function.Name}.{p.Action.Name}") // Định dạng: "Function.Action"
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions;
    }

    /// <summary>
    /// Kiểm tra xem user có quyền cụ thể hay không
    /// Được sử dụng bởi PermissionAuthorizationHandler
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        string userId, 
        string permission, 
        CancellationToken cancellationToken = default)
    {
        // Lấy danh sách quyền của user
        var permissions = await GetPermissionsAsync(userId, cancellationToken);

        // Kiểm tra xem permission có tồn tại trong danh sách không
        // Định dạng permission: "Permissions.Function.Action" (từ JWT claims)
        // HOẶC "Function.Action" (từ database)
        // Vì vậy cần chuẩn hóa để so sánh
        var normalizedPermission = permission
            .Replace("Permissions.", "", StringComparison.OrdinalIgnoreCase);

        return permissions?.Contains(normalizedPermission) ?? false;
    }
}
