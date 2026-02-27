using NightMarket.WebApi.Application.Common.Events;
using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Common.Models;
using NightMarket.WebApi.Application.Common.Specification;
using NightMarket.WebApi.Application.Identity.Users;
using NightMarket.WebApi.Application.Identity.Users.Password;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Auth;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using NightMarket.Shared.Authorization;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ardalis.Specification.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// Service xử lý user management operations
/// (Partial class - implementation chia thành nhiều files)
/// </summary>
internal partial class UserService : IUserService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly SecuritySettings _securitySettings;
    private readonly IEventPublisher _events;

    public UserService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        IOptions<SecuritySettings> securitySettings,
        IEventPublisher events)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _securitySettings = securitySettings.Value;
        _events = events;
    }

    #region Default Operations

    /// <summary>
    /// Search users với pagination và filters
    /// </summary>
    public async Task<PaginationResponse<UserDetailDto>> SearchAsync(
        UserParameterFilter filter, 
        CancellationToken cancellationToken)
    {
        var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(filter);

        var users = await _userManager.Users
            .WithSpecification(spec)
            .ProjectToType<UserDetailDto>()
            .ToListAsync(cancellationToken);

        int count = await _userManager.Users.CountAsync(cancellationToken);

        return new PaginationResponse<UserDetailDto>(
            users, 
            count, 
            filter.PageNumber, 
            filter.PageSize);
    }

    /// <summary>
    /// Check username đã tồn tại chưa
    /// </summary>
    public async Task<bool> ExistsWithNameAsync(string name)
    {
        return await _userManager.FindByNameAsync(name) is not null;
    }

    /// <summary>
    /// Check email đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
    public async Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null)
    {
        return await _userManager.FindByEmailAsync(email.Normalize()) is ApplicationUser user 
            && user.Id != exceptId;
    }

    /// <summary>
    /// Check phone number đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
    public async Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null)
    {
        return await _userManager.Users
            .FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber) is ApplicationUser user 
            && user.Id != exceptId;
    }

    /// <summary>
    /// Get full name của user
    /// </summary>
    public async Task<string> GetFullName(Guid userId)
    {
        var user = await GetAsync(userId.ToString(), CancellationToken.None);
        return string.Join(" ", user.FirstName, user.LastName);
    }

    /// <summary>
    /// Get list tất cả users (không pagination)
    /// </summary>
    public async Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken) =>
        (await _userManager.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Adapt<List<UserDetailDto>>();

    /// <summary>
    /// Get total user count
    /// </summary>
    public Task<int> GetCountAsync(CancellationToken cancellationToken) =>
        _userManager.Users.AsNoTracking().CountAsync(cancellationToken);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    public async Task<UserDetailDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        return user.Adapt<UserDetailDto>();
    }

    /// <summary>
    /// Toggle user active status (admin only)
    /// </summary>
    public async Task ToggleStatusAsync(
        ToggleUserStatusRequest request, 
        CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .Where(u => u.Id == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Không cho phép deactivate admin
        bool isAdmin = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
        if (isAdmin)
        {
            throw new ConflictException("Administrators Profile's Status cannot be toggled");
        }

        user.IsActive = request.ActivateUser;

        await _userManager.UpdateAsync(user);
    }

    #endregion
    
    // Placeholder implementations for Role and Permission Operations

    public Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
        
    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, string origin)
        => throw new NotImplementedException();

    public Task<string> ResetPasswordAsync(ResetPasswordRequest request)
        => throw new NotImplementedException();

    public Task<ChangePasswordRequest> ChangePasswordAsync(ChangePasswordRequest request, string userId)
        => throw new NotImplementedException();
        
    Task IUserService.ChangePasswordAsync(ChangePasswordRequest request, string userId)
    {
        throw new NotImplementedException();
    }
}
