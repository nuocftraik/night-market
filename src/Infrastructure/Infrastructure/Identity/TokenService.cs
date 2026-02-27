using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Identity.Tokens;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Auth;
using NightMarket.WebApi.Infrastructure.Auth.Jwt;
using NightMarket.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// Service xử lý JWT token generation và validation
/// </summary>
internal class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SecuritySettings _securitySettings;
    private readonly JwtSettings _jwtSettings;

    public TokenService(
        UserManager<ApplicationUser> userManager,
        IOptions<JwtSettings> jwtSettings,
        IOptions<SecuritySettings> securitySettings)
    {
        _userManager = userManager;
        _jwtSettings = jwtSettings.Value;
        _securitySettings = securitySettings.Value;
    }

    /// <summary>
    /// Generate tokens khi user login (email + password)
    /// </summary>
    public async Task<TokenResponse> GetTokenAsync(
        TokenRequest request, 
        string ipAddress, 
        CancellationToken cancellationToken)
    {
        // Find user by email
        var user = await _userManager.FindByEmailAsync(request.Email.Trim().Normalize());
        
        // Validate credentials
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        // Check if user is active
        if (!user.IsActive)
        {
            throw new UnauthorizedException("User Not Active. Please contact the administrator.");
        }

        // Check email confirmation nếu required
        if (_securitySettings.RequireConfirmedAccount && !user.EmailConfirmed)
        {
            throw new UnauthorizedException("E-Mail not confirmed.");
        }

        // Generate tokens
        return await GenerateTokensAndUpdateUser(user, ipAddress);
    }

    /// <summary>
    /// Refresh access token bằng refresh token
    /// </summary>
    public async Task<TokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request, 
        string ipAddress)
    {
        // Extract claims từ expired token (không validate expiration)
        var userPrincipal = GetPrincipalFromExpiredToken(request.Token);
        string? userEmail = userPrincipal.GetEmail();
        
        // Find user
        var user = await _userManager.FindByEmailAsync(userEmail!) 
            ?? throw new UnauthorizedException("Authentication Failed.");

        // Validate refresh token
        if (user.RefreshToken != request.RefreshToken || 
            user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Invalid Refresh Token.");
        }

        // Generate new tokens
        return await GenerateTokensAndUpdateUser(user, ipAddress);
    }

    /// <summary>
    /// Generate tokens và update user refresh token trong database
    /// </summary>
    public async Task<TokenResponse> GenerateTokensAndUpdateUser(
        ApplicationUser user, 
        string ipAddress)
    {
        // Generate JWT access token
        string token = GenerateJwt(user, ipAddress);

        // Generate refresh token (cryptographically random)
        user.RefreshToken = GenerateRefreshToken();
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
            _jwtSettings.RefreshTokenExpirationInDays);

        // Update user trong database
        await _userManager.UpdateAsync(user);

        return new TokenResponse(token, user.RefreshToken, user.RefreshTokenExpiryTime);
    }

    #region Private Methods

    /// <summary>
    /// Generate JWT access token
    /// </summary>
    private string GenerateJwt(ApplicationUser user, string ipAddress) =>
        GenerateEncryptedToken(GetSigningCredentials(), GetClaims(user, ipAddress));

    /// <summary>
    /// Build user claims cho JWT
    /// </summary>
    private IEnumerable<Claim> GetClaims(ApplicationUser user, string ipAddress) =>
        new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(AppClaims.Fullname, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(AppClaims.IpAddress, ipAddress),
            new(AppClaims.ImageUrl, user.ImageUrl ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty)
        };

    /// <summary>
    /// Generate cryptographically secure refresh token
    /// </summary>
    private static string GenerateRefreshToken()
    {
        byte[] randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// Generate JWT token với signing credentials và claims
    /// </summary>
    private string GenerateEncryptedToken(
        SigningCredentials signingCredentials, 
        IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.TokenExpirationInMinutes),
            signingCredentials: signingCredentials);
   
        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Extract claims từ expired token (không validate lifetime)
    /// </summary>
    private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.Key)),
            ValidateIssuer = false,
            ValidateAudience = false,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero,
            ValidateLifetime = false // KHÔNG validate expiration
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(
            token, 
            tokenValidationParameters, 
            out var securityToken);
          
        // Verify algorithm (phải là HMAC-SHA256)
        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase))
        {
            throw new UnauthorizedException("Invalid Token.");
        }

        return principal;
    }

    /// <summary>
    /// Get signing credentials với secret key
    /// </summary>
    private SigningCredentials GetSigningCredentials()
    {
        byte[] secret = Encoding.UTF8.GetBytes(_jwtSettings.Key);
        return new SigningCredentials(
            new SymmetricSecurityKey(secret), 
            SecurityAlgorithms.HmacSha256);
    }

    #endregion
}
