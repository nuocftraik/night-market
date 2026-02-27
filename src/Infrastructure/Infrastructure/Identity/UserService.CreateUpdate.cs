using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Identity.Users;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.Shared.Authorization;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Create & Update Operations (Partial Class)
/// </summary>
internal partial class UserService
{
    /// <summary>
    /// Create new user (admin hoặc self-register)
    /// </summary>
    public async Task<string> CreateAsync(CreateUserRequest request, string origin)
    {
        // Create ApplicationUser entity
        var user = new ApplicationUser
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserName = request.UserName,
            PhoneNumber = request.PhoneNumber,
            IsActive = true
        };

        // Create user với password (ASP.NET Core Identity)
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InternalServerException(
                "Validation Errors Occurred.", 
                result.Errors.Select(e => e.Description).ToList());
        }

        // Assign "Basic" role by default
        await _userManager.AddToRoleAsync(user, AppRoles.Basic);

        var message = $"User {user.UserName} Registered.";

        // TODO: Email confirmation sẽ implement trong BUILD_23 (Email Service)
        return message;
    }

    /// <summary>
    /// Update user profile (basic info only - no image upload yet)
    /// </summary>
    public async Task UpdateAsync(UpdateUserRequest request, string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Update basic info
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.Email = request.Email;

        // Update phone number nếu changed
        string? phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        if (request.PhoneNumber != phoneNumber)
        {
            await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
        }

        // Update user trong database
        var result = await _userManager.UpdateAsync(user);

        // Refresh sign in (update claims)
        await _signInManager.RefreshSignInAsync(user);

        if (!result.Succeeded)
        {
            throw new InternalServerException("Update profile failed", result.Errors.Select(e => e.Description).ToList());
        }
    }
}
