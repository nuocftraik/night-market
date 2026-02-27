using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Application.Identity.Tokens;

namespace NightMarket.WebApi.Application.Identity.O2Auth;

/// <summary>
/// Authentication service cho OAuth2 social login
/// </summary>
public interface IAuthenticationService : ITransientService
{
    /// <summary>
    /// Đăng nhập bằng Google ID Token
    /// </summary>
    Task<TokenResponse> GoogleSignIn(string idToken, string ipAddress);

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code
    /// </summary>
    Task<TokenResponse> GoogleSignIn2(string authorizedCode, string ipAddress);

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token
    /// </summary>
    Task<TokenResponse> FacebookSignIn(string idToken, string ipAddress);
}
