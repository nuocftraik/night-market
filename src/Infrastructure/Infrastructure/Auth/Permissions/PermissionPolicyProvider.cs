using NightMarket.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace NightMarket.WebApi.Infrastructure.Auth.Permissions;

/// <summary>
/// Nhà cung cấp chính sách quyền (tạo policy động)
/// Tạo authorization policies dựa trên chuỗi permission
/// </summary>
internal class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    /// <summary>
    /// Nhà cung cấp policy dự phòng (cho các policy không phải permission)
    /// </summary>
    public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => 
        FallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AppClaims.Permission, StringComparison.OrdinalIgnoreCase))
        {
             var policy = new AuthorizationPolicyBuilder();
            policy.AddRequirements(new PermissionRequirement(policyName));
            return Task.FromResult<AuthorizationPolicy?>(policy.Build());
        }

        return FallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => 
        Task.FromResult<AuthorizationPolicy?>(null);
}
