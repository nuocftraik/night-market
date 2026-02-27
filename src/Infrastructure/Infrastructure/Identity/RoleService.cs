using NightMarket.WebApi.Application.Common.Events;
using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Application.Identity.Functions;
using NightMarket.WebApi.Application.Identity.Roles;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using NightMarket.Shared.Authorization;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// Service xử lý role management operations
/// </summary>
internal class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IEventPublisher _events;
    // private readonly IFunctionService _functionService;

    public RoleService(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ICurrentUser currentUser,
        IEventPublisher events)
        // IFunctionService functionService)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _db = db;
        _currentUser = currentUser;
        _events = events;
        // _functionService = functionService;
    }

    /// <summary>
    /// Get list tất cả roles
    /// </summary>
    public async Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return (await _roleManager.Roles.ToListAsync(cancellationToken))
            .Adapt<List<RoleDto>>();
    }

    /// <summary>
    /// Get total role count
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return await _roleManager.Roles.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Check role name đã tồn tại chưa (exclude excludeId nếu update)
    /// </summary>
    public async Task<bool> ExistsAsync(string roleName, string? excludeId)
    {
        return await _roleManager.FindByNameAsync(roleName)
               is ApplicationRole existingRole
               && existingRole.Id != excludeId;
    }

    /// <summary>
    /// Get role details by ID
    /// </summary>
    public async Task<RoleDto> GetByIdAsync(string id)
    {
        return await _db.Roles.SingleOrDefaultAsync(x => x.Id == id) is { } role
            ? role.Adapt<RoleDto>()
            : throw new NotFoundException("Role Not Found");
    }

    /// <summary>
    /// Get role details với permissions (Functions + Actions)
    /// Returns list of Functions with Actions marked as Selected or not
    /// </summary>
    public async Task<List<FunctionDto>> GetByIdWithPermissionsAsync(
        string roleId, 
        CancellationToken cancellationToken)
    {
        // Get all functions với actions
        var functions = await _db.Functions
            .Include(f => f.ActionInFunctions)
            .ThenInclude(x => x.Action)
            .ToListAsync(cancellationToken);

        // Get permissions cho role này (từ Permission table)
        var permissions = await _db.Permissions
            .Where(p => p.RoleId == roleId)
            .ToListAsync(cancellationToken);

        // Build FunctionDto list với Selected flags
        var functionDtos = new List<FunctionDto>();

        foreach (var function in functions)
        {
            var functionDto = new FunctionDto
            {
                Id = function.Id,
                Name = function.Name,
                ActionDtos = function.ActionInFunctions.Select(aif => new ActionDto
                {
                    Id = aif.Action.Id,
                    Name = aif.Action.Name,
                    // Check nếu permission exists trong Permission table
                    Selected = permissions.Any(p => 
                        p.FunctionId == function.Id && 
                        p.ActionId == aif.Action.Id)
                }).ToList()
            };
            functionDtos.Add(functionDto);
        }

        return functionDtos;
    }

    /// <summary>
    /// Create hoặc update role
    /// </summary>
    public async Task<string> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            // Create new role
            var role = new ApplicationRole(request.Name, request.Description);
            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                throw new InternalServerException(
                    "Register role failed", 
                    result.Errors.Select(e => e.Description).ToList());
            }

            return $"Role {request.Name} Created.";
        }
        else
        {
            // Update existing role
            var role = await _roleManager.FindByIdAsync(request.Id);

            _ = role ?? throw new NotFoundException("Role Not Found");

            // Cannot update default roles
            if (AppRoles.IsDefault(role.Name!))
            {
                throw new ConflictException($"Not allowed to modify {role.Name} Role.");
            }

            role.Name = request.Name;
            role.NormalizedName = request.Name.ToUpperInvariant();
            role.Description = request.Description;

            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                throw new InternalServerException(
                    "Update role failed", 
                    result.Errors.Select(e => e.Description).ToList());
            }

            return $"Role {role.Name} Updated.";
        }
    }

    /// <summary>
    /// Update permissions của role (table-based approach)
    /// Replaces all current permissions with new ones
    /// </summary>
    public async Task<string> UpdatePermissionsAsync(
        UpdateRolePermissionsRequest request, 
        CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(request.RoleId);
        _ = role ?? throw new NotFoundException("Role Not Found");

        // Cannot update Admin role permissions
        if (role.Name == AppRoles.Admin)
        {
            throw new ConflictException("Not allowed to modify Permissions for this Role.");
        }

        // Remove all current permissions
        var currentPermissions = await _db.Permissions
            .Where(p => p.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        _db.Permissions.RemoveRange(currentPermissions);
        await _db.SaveChangesAsync(cancellationToken);

        // Add new permissions từ request
        foreach (var permissionRequest in request.Permissions)
        {
            if (!string.IsNullOrEmpty(permissionRequest.FunctionId) && 
                !string.IsNullOrEmpty(permissionRequest.ActionId))
            {
                _db.Permissions.Add(new Permission(
                    role.Id, 
                    permissionRequest.FunctionId, 
                    permissionRequest.ActionId));
            }
        }
        
        await _db.SaveChangesAsync(cancellationToken);
        return "Permissions Updated.";
    }

    /// <summary>
    /// Delete role với validation
    /// </summary>
    public async Task<string> DeleteAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);

        _ = role ?? throw new NotFoundException("Role Not Found");

        // Cannot delete default roles
        if (AppRoles.IsDefault(role.Name!))
        {
            throw new ConflictException($"Not allowed to delete {role.Name} Role.");
        }

        // Cannot delete role đang được users sử dụng
        if ((await _userManager.GetUsersInRoleAsync(role.Name!)).Count > 0)
        {
            throw new ConflictException($"Not allowed to delete {role.Name} Role as it is being used.");
        }

        await _roleManager.DeleteAsync(role);

        return $"Role {role.Name} Deleted.";
    }
}
