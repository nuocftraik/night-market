using System.ComponentModel.DataAnnotations;

namespace NightMarket.WebApi.Infrastructure.Auth.Jwt;

/// <summary>
/// JWT authentication settings
/// </summary>
public class JwtSettings : IValidatableObject
{
    /// <summary>
    /// Secret key for signing tokens (minimum 32 characters)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Access token expiration time in minutes
    /// </summary>
    public int TokenExpirationInMinutes { get; set; }

    /// <summary>
    /// Refresh token expiration time in days
    /// </summary>
    public int RefreshTokenExpirationInDays { get; set; }

    /// <summary>
    /// Validate JWT settings
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Key))
        {
            yield return new ValidationResult(
                "No Key defined in JwtSettings config", 
                new[] { nameof(Key) });
        }

        if (Key.Length < 32)
        {
            yield return new ValidationResult(
                "JWT Key must be at least 32 characters", 
                new[] { nameof(Key) });
        }
    }
}
