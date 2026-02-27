using NightMarket.WebApi.Application.Identity.Tokens;
using NightMarket.WebApi.Host.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NightMarket.WebApi.Host.Controllers.Identity;

/// <summary>
/// Token management APIs
/// </summary>
public sealed class TokensController : BaseApiController
{ 
    private readonly ITokenService _tokenService;

    public TokensController(ITokenService tokenService) => _tokenService = tokenService;

    /// <summary>
    /// Login và lấy access token
    /// </summary>
    /// <param name="request">Email và Password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TokenResponse chứa access token và refresh token</returns>
    [HttpPost("get")]
    [AllowAnonymous]
    public Task<TokenResponse> GetTokenAsync(
        TokenRequest request, 
        CancellationToken cancellationToken)
    {
        return _tokenService.GetTokenAsync(request, GetIpAddress()!, cancellationToken);
    }

    /// <summary>
    /// Refresh access token bằng refresh token
    /// </summary>
    /// <param name="request">Expired access token và refresh token</param>
    /// <returns>TokenResponse chứa tokens mới</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public Task<TokenResponse> RefreshAsync(RefreshTokenRequest request)
    {
        return _tokenService.RefreshTokenAsync(request, GetIpAddress()!);
    }

    /// <summary>
    /// Get client IP address (support proxy)
    /// </summary>
    private string? GetIpAddress() =>
        Request.Headers.ContainsKey("X-Forwarded-For")
            ? Request.Headers["X-Forwarded-For"].ToString()
            : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
}
