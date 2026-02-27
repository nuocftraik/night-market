using NightMarket.WebApi.Application.Common.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// UserService - Email/Phone Confirmation Operations (Partial Class)
/// </summary>
internal partial class UserService
{
    /// <summary>
    /// Confirm email với verification code
    /// TODO: Full implementation trong BUILD_23 (Email Service)
    /// </summary>
    public async Task<string> ConfirmEmailAsync(
        string userId, 
        string code, 
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Decode code từ query string
        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

        // Confirm email với Identity
        var result = await _userManager.ConfirmEmailAsync(user, code);

        if (result.Succeeded)
        {
            return "Email confirmed successfully!";
        }

        throw new InternalServerException("An error occurred while confirming email.");
    }

    /// <summary>
    /// Confirm phone number với verification code
    /// TODO: Full implementation trong BUILD_23 (Email Service)
    /// </summary>
    public async Task<string> ConfirmPhoneNumberAsync(string userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Confirm phone với Identity
        var result = await _userManager.ChangePhoneNumberAsync(user, user.PhoneNumber!, code);

        if (result.Succeeded)
        {
            return "Phone number confirmed successfully!";
        }

        throw new InternalServerException("An error occurred while confirming phone number.");
    }
}
