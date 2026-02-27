using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Identity.O2Auth;
using NightMarket.WebApi.Application.Identity.Tokens;
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Auth.OAuth2;
using NightMarket.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using System.Net.Http.Json;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// Authentication service implementation cho OAuth2 social login
/// </summary>
internal class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GoogleAuthSettings _googleAuthConfig;
    private readonly ITokenService _tokenService;
    private readonly ISerializerService _serializerService;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        IOptions<GoogleAuthSettings> googleAuthConfig,
        ITokenService tokenService,
        ISerializerService serializerService)
    {
        _userManager = userManager;
        _googleAuthConfig = googleAuthConfig.Value;
        _tokenService = tokenService;
        _serializerService = serializerService;
    }

    /// <summary>
    /// Đăng nhập bằng Google ID Token
    /// </summary>
    public async Task<TokenResponse> GoogleSignIn(string token, string ipAddress)
    {
        var payload = await GoogleJsonWebSignature
            .ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleAuthConfig.ClientId }
            });

        var emailLogin = payload.Email;
        var existingUser = await _userManager.FindByEmailAsync(
            emailLogin.Trim().Normalize());

        if (existingUser == null)
        {
            existingUser = new ApplicationUser
            {
                Email = emailLogin,
                FirstName = payload.GivenName,
                LastName = payload.FamilyName,
                UserName = payload.Email,
                EmailConfirmed = true,
                IsActive = true
            };

            await _userManager.CreateAsync(existingUser);
            await _userManager.AddToRoleAsync(existingUser, AppRoles.Basic);
        }

        return await _tokenService.GenerateTokensAndUpdateUser(
            existingUser, 
            ipAddress);
    }

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code
    /// </summary>
    public async Task<TokenResponse> GoogleSignIn2(
        string authorizationCode, 
        string ipAddress)
    {
        var googleAuthorizationCodeFlow = new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _googleAuthConfig.ClientId,
                    ClientSecret = _googleAuthConfig.ClientSecret
                },
                Scopes = new[] 
                { 
                    "https://www.googleapis.com/auth/userinfo.profile", 
                    "https://www.googleapis.com/auth/userinfo.email" 
                }
            });

        var tokenResponse = await googleAuthorizationCodeFlow.ExchangeCodeForTokenAsync(
            userId: "me",
            code: authorizationCode,
            redirectUri: "https://localhost:7001/auth/callback",
            CancellationToken.None);

        using var httpClient = new HttpClient();
        var userInfoResponse = await httpClient.GetStringAsync(
            $"https://www.googleapis.com/oauth2/v2/userinfo?access_token={tokenResponse.AccessToken}");

        var userInfo = _serializerService.Deserialize<GoogleUserInfo>(userInfoResponse);

        var emailLogin = userInfo!.Email;
        var existingUser = await _userManager.FindByEmailAsync(
            emailLogin.Trim().Normalize());

        if (existingUser == null)
        {
            existingUser = new ApplicationUser
            {
                Email = emailLogin,
                FirstName = userInfo.GivenName,
                LastName = userInfo.FamilyName,
                UserName = emailLogin,
                EmailConfirmed = true,
                IsActive = true
            };

            await _userManager.CreateAsync(existingUser);
            await _userManager.AddToRoleAsync(existingUser, AppRoles.Basic);
        }

        return await _tokenService.GenerateTokensAndUpdateUser(
            existingUser, 
            ipAddress);
    }

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token
    /// </summary>
    public async Task<TokenResponse> FacebookSignIn(string accessToken, string ipAddress)
    {
        using var httpClient = new HttpClient();
        var userInfoUrl = $"https://graph.facebook.com/me?" +
            $"fields=id,email,first_name,last_name,picture&" +
            $"access_token={accessToken}";

        var userInfoResponse = await httpClient.GetAsync(userInfoUrl);

        if (!userInfoResponse.IsSuccessStatusCode)
        {
            throw new UnauthorizedException("Invalid Facebook Access Token");
        }

        var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<FacebookUserInfo>();

        if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
        {
            throw new UnauthorizedException("Unable to get user info from Facebook");
        }

        var emailLogin = userInfo.Email;
        var existingUser = await _userManager.FindByEmailAsync(
            emailLogin.Trim().Normalize());

        if (existingUser == null)
        {
            existingUser = new ApplicationUser
            {
                Email = emailLogin,
                FirstName = userInfo.FirstName,
                LastName = userInfo.LastName,
                UserName = emailLogin,
                EmailConfirmed = true,
                IsActive = true,
                ImageUrl = userInfo.Picture?.Data?.Url
            };

            await _userManager.CreateAsync(existingUser);
            await _userManager.AddToRoleAsync(existingUser, AppRoles.Basic);
        }

        return await _tokenService.GenerateTokensAndUpdateUser(
            existingUser, 
            ipAddress);
    }
}

public class GoogleUserInfo
{
    public string Email { get; set; } = default!;
    public string GivenName { get; set; } = default!;
    public string FamilyName { get; set; } = default!;
    public string Picture { get; set; } = default!;
}

public class FacebookUserInfo
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public FacebookPicture? Picture { get; set; }
}

public class FacebookPicture
{
    public FacebookPictureData? Data { get; set; }
}

public class FacebookPictureData
{
    public string Url { get; set; } = default!;
}
