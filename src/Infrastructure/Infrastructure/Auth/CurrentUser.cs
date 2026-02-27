using System.Security.Claims;
using NightMarket.WebApi.Application.Common.Interfaces;

namespace NightMarket.WebApi.Infrastructure.Auth;

/// <summary>
/// Implementation của ICurrentUser và ICurrentUserInitializer
/// Scoped per request - mỗi HTTP request có instance riêng
/// </summary>
public class CurrentUser : ICurrentUser, ICurrentUserInitializer
{
    private ClaimsPrincipal? _user;
    private Guid _userId = Guid.Empty;

    /// <summary>
    /// User name từ Identity.Name
    /// </summary>
    public string? Name => _user?.Identity?.Name;

    /// <summary>
    /// Lấy User ID từ NameIdentifier claim
    /// </summary>
    public Guid GetUserId() =>
        IsAuthenticated()
            ? Guid.Parse(_user?.GetUserId() ?? Guid.Empty.ToString())
            : _userId;

    /// <summary>
    /// Lấy User Email từ Email claim
    /// </summary>
    public string? GetUserEmail() =>
        IsAuthenticated()
            ? _user!.GetEmail()
            : string.Empty;

    /// <summary>
    /// Check user đã authenticate chưa
    /// </summary>
    public bool IsAuthenticated() =>
        _user?.Identity?.IsAuthenticated is true;

    /// <summary>
    /// Check user có role không
    /// </summary>
    public bool IsInRole(string role) =>
        _user?.IsInRole(role) is true;

    /// <summary>
    /// Lấy tất cả claims
    /// </summary>
    public IEnumerable<Claim>? GetUserClaims() =>
        _user?.Claims;

    /// <summary>
    /// Set current user từ ClaimsPrincipal
    /// Chỉ được gọi một lần per request (từ middleware)
    /// </summary>
    public void SetCurrentUser(ClaimsPrincipal user)
    {
        if (_user != null)
        {
            throw new Exception("Method reserved for in-scope initialization");
        }

        _user = user;
    }

    /// <summary>
    /// Set current user ID manually (cho background jobs)
    /// </summary>
    public void SetCurrentUserId(string userId)
    {
        if (_userId != Guid.Empty)
        {
            throw new Exception("Method reserved for in-scope initialization");
        }

        if (!string.IsNullOrEmpty(userId))
        {
            _userId = Guid.Parse(userId);
        }
    }
}
