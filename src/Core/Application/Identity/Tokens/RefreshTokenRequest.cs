namespace NightMarket.WebApi.Application.Identity.Tokens;

/// <summary>
/// Request để refresh access token
/// </summary>
/// <param name="Token">Access token cũ (đã expired)</param>
/// <param name="RefreshToken">Refresh token còn hạn</param>
public record RefreshTokenRequest(string Token, string RefreshToken);
