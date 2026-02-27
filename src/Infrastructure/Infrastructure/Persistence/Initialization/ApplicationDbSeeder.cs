using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NightMarket.Shared.Authorization;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using Action = NightMarket.WebApi.Domain.Identity.Action;

namespace NightMarket.WebApi.Infrastructure.Persistence.Initialization;

internal class ApplicationDbSeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CustomSeederRunner _seederRunner;
    private readonly ILogger<ApplicationDbSeeder> _logger;

    public ApplicationDbSeeder(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        CustomSeederRunner seederRunner,
        ILogger<ApplicationDbSeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _seederRunner = seederRunner;
        _logger = logger;
    }

    public async Task SeedDatabaseAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Seed theo thứ tự phụ thuộc
        await SeedActionsAndFunctionsAsync(dbContext);
        await SeedRolesAsync(dbContext);
        await SeedAdminUserAsync();
        await _seederRunner.RunSeedersAsync(cancellationToken);
    }

    private async Task SeedActionsAndFunctionsAsync(ApplicationDbContext dbContext)
    {
        // 1. Seed Actions
        var actions = typeof(AppAction)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
            .ToList();

        foreach (var action in actions)
        {
            if (!await dbContext.Actions.AnyAsync(x => x.Name == action))
            {
                dbContext.Actions.Add(new Action { Id = Guid.NewGuid().ToString(), Name = action! });
            }
        }
        await dbContext.SaveChangesAsync();

        // 2. Seed Functions
        var functions = typeof(AppFunction)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
            .ToList();

        foreach (var functionName in functions)
        {
            if (!await dbContext.Functions.AnyAsync(f => f.Name == functionName))
            {
                dbContext.Functions.Add(new Function { Id = Guid.NewGuid().ToString(), Name = functionName! });
            }
        }
        await dbContext.SaveChangesAsync();

        // 3. Link Actions with Functions
        foreach (var functionName in functions)
        {
            var function = await dbContext.Functions.FirstAsync(f => f.Name == functionName);
            foreach (var actionName in actions)
            {
                var action = await dbContext.Actions.FirstAsync(a => a.Name == actionName);
                if (!await dbContext.ActionInFunctions.AnyAsync(
                        aif => aif.FunctionId == function.Id && aif.ActionId == action.Id))
                {
                    dbContext.ActionInFunctions.Add(new ActionInFunction { ActionId = action.Id, FunctionId = function.Id });
                }
            }
        }
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedRolesAsync(ApplicationDbContext dbContext)
    {
        foreach (string roleName in AppRoles.DefaultRoles)
        {
            // Tạo role nếu chưa có
            if (await _roleManager.FindByNameAsync(roleName) is not ApplicationRole role)
            {
                _logger.LogInformation("Seeding {role} Role.", roleName);
                role = new ApplicationRole(roleName, $"{roleName} Role");
                await _roleManager.CreateAsync(role);
            }

            // Assign permissions
            if (roleName == AppRoles.Admin)
            {
                await AssignAllPermissionsAsync(dbContext, role);
            }
            else if (roleName == AppRoles.Basic)
            {
                await AssignBasicPermissionsAsync(dbContext, role);
            }
        }
    }

    private async Task AssignAllPermissionsAsync(ApplicationDbContext dbContext, ApplicationRole role)
    {
        var functions = await dbContext.Functions.ToListAsync();
        foreach (var function in functions)
        {
            var actions = await dbContext.ActionInFunctions
                .Where(aif => aif.FunctionId == function.Id)
                .ToListAsync();

            foreach (var actionInFunction in actions)
            {
                if (!await dbContext.Permissions.AnyAsync(p =>
                        p.RoleId == role.Id &&
                        p.FunctionId == function.Id &&
                        p.ActionId == actionInFunction.ActionId))
                {
                    dbContext.Permissions.Add(new Permission { RoleId = role.Id, FunctionId = function.Id, ActionId = actionInFunction.ActionId });
                }
            }
        }
        await dbContext.SaveChangesAsync();
    }

    private async Task AssignBasicPermissionsAsync(ApplicationDbContext dbContext, ApplicationRole role)
    {
        // Basic role chỉ có View và Search permissions
        var basicActions = new[] { AppAction.View, AppAction.Search };
        var functions = await dbContext.Functions.ToListAsync();

        foreach (var function in functions)
        {
            var actions = await dbContext.ActionInFunctions
                .Include(x => x.Action)
                .Where(aif => aif.FunctionId == function.Id && basicActions.Contains(aif.Action.Name))
                .ToListAsync();

            foreach (var actionInFunction in actions)
            {
                if (!await dbContext.Permissions.AnyAsync(p =>
                        p.RoleId == role.Id &&
                        p.FunctionId == function.Id &&
                        p.ActionId == actionInFunction.ActionId))
                {
                    dbContext.Permissions.Add(new Permission { RoleId = role.Id, FunctionId = function.Id, ActionId = actionInFunction.ActionId });
                }
            }
        }
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedAdminUserAsync()
    {
        const string adminEmail = "admin@gmail.com";
        const string adminPassword = "Abcd@1234";

        if (await _userManager.FindByEmailAsync(adminEmail) is not ApplicationUser adminUser)
        {
            _logger.LogInformation("Seeding default admin user.");

            adminUser = new ApplicationUser
            {
                FirstName = "System",
                LastName = "Admin",
                Email = adminEmail,
                UserName = "system.admin",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true
            };

            await _userManager.CreateAsync(adminUser, adminPassword);
        }

        // Assign Admin role
        if (!await _userManager.IsInRoleAsync(adminUser, AppRoles.Admin))
        {
            await _userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
        }
    }
}
