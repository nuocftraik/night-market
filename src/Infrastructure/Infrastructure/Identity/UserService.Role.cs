using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Identity.Users;
using NightMarket.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Role Operations (Partial Class)
/// </summary>
internal partial class UserService
{
    /// <summary>
    /// Get user's assigned roles
    /// </summary>
    public async Task<List<UserRoleDto>> GetRolesAsync(
        string userId, 
        CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Get user's roles
        var userRoles = await _userManager.GetRolesAsync(user);

        // Get all available roles
        var allRoles = await _roleManager.Roles.ToListAsync(cancellationToken);

        var roleDtos = allRoles.Select(role => new UserRoleDto
        {
            RoleId = role.Id,
            RoleName = role.Name!,
            Description = role.Description,
            Enabled = userRoles.Contains(role.Name!) // Check if user has this role
        }).ToList();

        return roleDtos;
    }

    /// <summary>
    /// Assign roles to user
    /// Replaces all current roles with new ones
    /// </summary>
    public async Task<string> AssignRolesAsync(
        string userId, 
        UserRolesRequest request, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var user = await _userManager.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Check if Admin role is being assigned/removed for current user
        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin)
            && (request.UserRoles.FirstOrDefault(r => r.RoleName == AppRoles.Admin) is not { Enabled: true }))
        {
            throw new ConflictException("Admin users cannot remove their own Admin role.");
        }

        // Remove all current roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in currentRoles)
        {
            await _userManager.RemoveFromRoleAsync(user, role);
        }

        // Add new roles từ request (where Enabled = true)
        foreach (var roleRequest in request.UserRoles.Where(r => r.Enabled))
        {
            var role = await _roleManager.FindByNameAsync(roleRequest.RoleName);
            if (role != null)
            {
                await _userManager.AddToRoleAsync(user, role.Name!);
            }
        }

        return "User Roles Updated Successfully.";
    }
}
