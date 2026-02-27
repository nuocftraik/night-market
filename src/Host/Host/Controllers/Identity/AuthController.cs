using NightMarket.WebApi.Application.Identity.O2Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NightMarket.WebApi.Host.Controllers.Identity;

/// <summary>
/// Authentication controller cho OAuth2 social login
/// </summary>
public class AuthController : BaseApiController
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Đăng nhập bằng Google ID Token.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] OAuthRequest request)
    {
        var response = await _authenticationService.GoogleSignIn(
            request.IdToken, 
            GetIpAddress()!);

        return Ok(response);
    }

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code.
    /// </summary>
    [HttpPost("google2")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin2([FromBody] string authorizedCode)
    {
        var response = await _authenticationService.GoogleSignIn2(
            authorizedCode, 
            GetIpAddress()!);

        return Ok(response);
    }

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token.
    /// </summary>
    [HttpPost("facebook")]
    [AllowAnonymous]
    public async Task<IActionResult> FacebookLogin([FromBody] OAuthRequest request)
    {
        var response = await _authenticationService.FacebookSignIn(
            request.IdToken, 
            GetIpAddress()!);

        return Ok(response);
    }

    private string? GetIpAddress() =>
        Request.Headers.ContainsKey("X-Forwarded-For")
            ? Request.Headers["X-Forwarded-For"].ToString()
            : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
}

/// <summary>
/// OAuth request model.
/// </summary>
public class OAuthRequest
{
    public string IdToken { get; set; } = default!;
}
