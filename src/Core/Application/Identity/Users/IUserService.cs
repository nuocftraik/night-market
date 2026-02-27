using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Application.Identity.Users.Password;
using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Identity.Users;

/// <summary>
/// Service xử lý user management operations
/// </summary>
public interface IUserService : ITransientService
{   
    #region Default Operations
    
    /// <summary>
    /// Search users với pagination và filters
    /// </summary>
    Task<PaginationResponse<UserDetailDto>> SearchAsync(
        UserParameterFilter filter, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Check username đã tồn tại chưa
    /// </summary>
    Task<bool> ExistsWithNameAsync(string name);

    /// <summary>
    /// Check email đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
    Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null);

    /// <summary>
    /// Check phone number đã tồn tại chưa (exclude exceptId nếu có)
    /// </summary>
    Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null);

    /// <summary>
    /// Get full name của user
    /// </summary>
    Task<string> GetFullName(Guid userId);

    /// <summary>
    /// Get list tất cả users (không pagination)
    /// </summary>
    Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get total user count
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get user details by ID
    /// </summary>
    Task<UserDetailDto> GetAsync(string userId, CancellationToken cancellationToken);

    #endregion

    #region Role Operations
    
    /// <summary>
    /// Get user's assigned roles
    /// </summary>
    Task<List<UserRoleDto>> GetRolesAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Assign roles to user
    /// </summary>
    Task<string> AssignRolesAsync(
        string userId, 
        UserRolesRequest request, 
        CancellationToken cancellationToken);

    #endregion

    #region Permission Operations
    
    /// <summary>
    /// Get user's permissions
    /// </summary>
    Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Check if user has specific permission
    /// </summary>
    Task<bool> HasPermissionAsync(
        string userId, 
        string permission, 
        CancellationToken cancellationToken = default);

    #endregion

    #region Create & Update Operations
    
    /// <summary>
    /// Toggle user active status (admin only)
    /// </summary>
    Task ToggleStatusAsync(ToggleUserStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Create new user (admin hoặc self-register)
    /// </summary>
    Task<string> CreateAsync(CreateUserRequest request, string origin);

    /// <summary>
    /// Update user profile
    /// </summary>
    Task UpdateAsync(UpdateUserRequest request, string userId);

    #endregion

    #region Email Confirmation
    
    /// <summary>
    /// Confirm email với verification code
    /// </summary>
    Task<string> ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken);

    /// <summary>
    /// Confirm phone number với verification code
    /// </summary>
    Task<string> ConfirmPhoneNumberAsync(string userId, string code);

    #endregion

    #region Password Operations
    
    /// <summary>
    /// Send forgot password email
    /// </summary>
    Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, string origin);

    /// <summary>
    /// Reset password với reset token
    /// </summary>
    Task<string> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Change password (user đã login)
    /// </summary>
    Task ChangePasswordAsync(ChangePasswordRequest request, string userId);

    #endregion
}
