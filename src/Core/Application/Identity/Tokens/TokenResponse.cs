namespace NightMarket.WebApi.Application.Identity.Tokens;

/// <summary>
/// Response chứa access token và refresh token
/// </summary>
/// <param name="accessToken">JWT access token (short-lived)</param>
/// <param name="refreshToken">Refresh token (long-lived)</param>
/// <param name="RefreshTokenExpiryTime">Thời điểm refresh token hết hạn</param>
public record TokenResponse(
    string accessToken, 
    string refreshToken, 
    DateTime RefreshTokenExpiryTime);
