using NightMarket.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Thuộc tính MustHavePermission (authorization khai báo)
/// Cách dùng: [MustHavePermission(AppAction.View, AppFunction.User)]
/// Tạo policy: "Permissions.User.View"
/// </summary>
public class MustHavePermissionAttribute : AuthorizeAttribute
{
    public MustHavePermissionAttribute(string action, string function)
    {
        Policy = AppPermission.NameFor(action, function);
    }
}
