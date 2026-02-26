# JWT Authentication - Token-based Authentication System

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 14 (Validation Behavior) đã hoàn thành

Tài liệu này hướng dẫn xây dựng JWT Authentication - Hệ thống xác thực dựa trên token với refresh token support.

---

## 1. Overview

**Làm gì:** Xây dựng JWT (JSON Web Token) authentication system với access token và refresh token.

**Tại sao cần:**
- **Stateless Authentication:** Server không cần lưu session, dễ scale horizontally
- **Security:** Token có thời gian sống ngắn, refresh token để renew access token
- **Cross-Platform:** JWT hoạt động trên mọi platform (Web, Mobile, Desktop)
- **Claims-Based:** Chứa user info và permissions trong token payload
- **Performance:** Không cần query database mỗi request để verify authentication

**Trong bước này chúng ta sẽ:**
- ✅ Tạo JwtSettings configuration

- ✅ Tạo SecuritySettings configuration
- ✅ Tạo Token DTOs (TokenRequest, TokenResponse, RefreshTokenRequest)
- ✅ Tạo ITokenService interface
- ✅ Implement TokenService (generate tokens, refresh tokens)
- ✅ Setup JWT authentication middleware
- ✅ Tạo TokenController để expose APIs
- ✅ Configure JwtBearer options

**Real-world example:**
```csharp
// Client login
var response = await httpClient.PostAsync("/api/tokens/get", new 
{
    Email = "admin@root.com",
    Password = "123Pa$$word!"
});

// Response
{
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "CfDJ8OxM...",
  "refreshTokenExpiryTime": "2024-02-28T10:00:00Z"
}

// Subsequent requests
httpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", accessToken);

// When token expires, refresh
var newTokens = await httpClient.PostAsync("/api/tokens/refresh", new
{
    Token = oldAccessToken,
    RefreshToken = refreshToken
});
```

---

## 2. Add Required Packages

### Bước 2.1: Add JWT Packages to Infrastructure

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- JWT Authentication -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.1.2" />
</ItemGroup>
```

**Giải thích packages:**
- `Microsoft.AspNetCore.Authentication.JwtBearer`: JWT authentication middleware cho ASP.NET Core
- `System.IdentityModel.Tokens.Jwt`: JWT token generation và validation

**⚠️ Lưu ý:**
- Packages này đã có sẵn trong Infrastructure project vì chúng ta đã add từ BUILD_05
- Nếu chưa có, chạy: `dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0`

---

## 3. Configuration Models

### Bước 3.1: JwtSettings Configuration

**Làm gì:** Tạo model để bind JWT settings từ configuration file.

**Tại sao:** Type-safe configuration với validation, dễ inject vào services.

**File:** `src/Infrastructure/Infrastructure/Auth/Jwt/JwtSettings.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace ECO.WebApi.Infrastructure.Auth.Jwt;

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
```

**Giải thích:**
- **Key:** Secret key để sign JWT token, phải >= 32 ký tự (HMAC-SHA256 requirement)
- **TokenExpirationInMinutes:** Thời gian sống của access token (thường 15-60 phút, hoặc 7 ngày cho development)
- **RefreshTokenExpirationInDays:** Thời gian sống của refresh token (thường 7-30 ngày)
- **IValidatableObject:** Validate configuration khi app start, fail fast nếu config sai

**Tại sao validation:**
- Catch configuration errors sớm (at startup)
- Prevent runtime errors khi generate tokens
- Better developer experience

---

### Bước 3.2: SecuritySettings Configuration

**Làm gì:** Tạo model cho security settings (provider, email confirmation).

**Tại sao:** Quản lý authentication provider và security policies.

**File:** `src/Infrastructure/Infrastructure/Auth/SecuritySettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Auth;

/// <summary>
/// Security configuration settings
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Authentication provider (e.g., "Jwt", "AzureAd")
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Require email confirmation before login
    /// </summary>
    public bool RequireConfirmedAccount { get; set; }
}
```

**Giải thích:**
- **Provider:** Loại authentication provider ("Jwt", "AzureAd", etc.)
- **RequireConfirmedAccount:** Yêu cầu user confirm email trước khi login (security best practice)

**Tại sao tách riêng:**
- JwtSettings: JWT-specific settings
- SecuritySettings: General security policies
- Easier to add other auth providers later (AzureAd, OAuth2, etc.)

---

### Bước 3.3: Configuration File

**Làm gì:** Tạo file cấu hình cho JWT và Security settings.

**Tại sao:** Separation of concerns, dễ quản lý settings theo environment.

**File:** `src/Host/Host/Configurations/security.json`

```json
{
  "SecuritySettings": {
    "Provider": "Jwt",
    "RequireConfirmedAccount": false,
    "JwtSettings": {
      "key": "S0M3RAN0MS3CR3T!1!MAG1C!1!AMAZIN",
      "tokenExpirationInMinutes": 10080,
      "refreshTokenExpirationInDays": 30
    }
  }
}
```

**Giải thích:**
- **Provider:** "Jwt" = sử dụng JWT authentication
- **RequireConfirmedAccount:** `false` cho development (set `true` cho production)
- **key:** Secret key để sign tokens (⚠️ PHẢI thay đổi trong production)
- **tokenExpirationInMinutes:** 10080 minutes = 7 days (cho development, production nên dùng 15-60 phút)
- **refreshTokenExpirationInDays:** 30 days

**⚠️ Production Security:**
```json
{
  "SecuritySettings": {
    "Provider": "Jwt",
    "RequireConfirmedAccount": true,
    "JwtSettings": {
      "key": "<GENERATE_STRONG_SECRET_KEY_FROM_SECURE_SOURCE>",
      "tokenExpirationInMinutes": 30,
      "refreshTokenExpirationInDays": 7
    }
  }
}
```

**Generate secure key:**
```bash
# PowerShell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

# Linux/Mac
openssl rand -base64 32
```

---

## 4. Token DTOs

### Bước 4.1: TokenRequest DTO

**Làm gì:** DTO cho login request (email + password).

**Tại sao:** Type-safe request model với FluentValidation.

**File:** `src/Core/Application/Identity/Tokens/TokenRequest.cs`

```csharp
using FluentValidation;

namespace ECO.WebApi.Application.Identity.Tokens;

/// <summary>
/// Request để lấy access token (login)
/// </summary>
public record TokenRequest(string Email, string Password);

/// <summary>
/// Validator cho TokenRequest
/// </summary>
public class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(p => p.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid Email Address.");

            RuleFor(p => p.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
```

**Giải thích:**
- **record:** Immutable DTO (C# 9 feature)
- **Email:** User email for login
- **Password:** User password
- **Validator:** Auto-validate bằng ValidationBehavior (BUILD_14)

**Tại sao record:**
- Immutable by default (thread-safe)
- Value-based equality
- Concise syntax
- Good for DTOs

---

### Bước 4.2: TokenResponse DTO

**Làm gì:** DTO cho token response (access token + refresh token).

**Tại sao:** Return multiple values từ token generation.

**File:** `src/Core/Application/Identity/Tokens/TokenResponse.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Tokens;

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
```

**Giải thích:**
- **accessToken:** JWT token chứa user claims (short-lived, 15-60 minutes)
- **refreshToken:** Token để renew access token (long-lived, 7-30 days)
- **RefreshTokenExpiryTime:** UTC timestamp khi refresh token expire

**Why two tokens:**
- **Access Token:** Short-lived, chứa user claims, gửi với mọi request
- **Refresh Token:** Long-lived, chỉ dùng để lấy access token mới, lưu secure

**Security pattern:**
- Access token ngắn hạn → giảm risk nếu bị steal
- Refresh token dài hạn → user không cần login thường xuyên
- Refresh token có thể revoke từ server side

---

### Bước 4.3: RefreshTokenRequest DTO

**Làm gì:** DTO cho refresh token request.

**Tại sao:** Renew access token khi hết hạn.

**File:** `src/Core/Application/Identity/Tokens/RefreshTokenRequest.cs`

```csharp
namespace ECO.WebApi.Application.Identity.Tokens;

/// <summary>
/// Request để refresh access token
/// </summary>
/// <param name="Token">Access token cũ (đã expired)</param>
/// <param name="RefreshToken">Refresh token còn hạn</param>
public record RefreshTokenRequest(string Token, string RefreshToken);
```

**Giải thích:**
- **Token:** Access token cũ (có thể đã expired)
- **RefreshToken:** Refresh token còn hạn

**Why both tokens:**
- **Token:** Verify principal claims từ expired token
- **RefreshToken:** Verify refresh token validity với database

**Flow:**
1. Client access token expires
2. Client gửi expired token + refresh token
3. Server extract claims từ expired token
4. Server verify refresh token từ database
5. Server generate new access token + refresh token
6. Return new tokens

---

## 5. Token Service Interface

### Bước 5.1: ITokenService Interface

**Làm gì:** Define contract cho token operations.

**Tại sao:** Abstraction, dễ test, dễ swap implementations.

**File:** `src/Core/Application/Identity/Tokens/ITokenService.cs`

```csharp
using ECO.WebApi.Domain.Identity;

namespace ECO.WebApi.Application.Identity.Tokens;

/// <summary>
/// Service xử lý JWT token operations
/// </summary>
public interface ITokenService : ITransientService
{
    /// <summary>
    /// Generate tokens khi user login (email + password)
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TokenResponse chứa access token và refresh token</returns>
    Task<TokenResponse> GetTokenAsync(
      TokenRequest request, 
     string ipAddress, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Refresh access token bằng refresh token
    /// </summary>
    /// <param name="request">Expired access token và refresh token</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <returns>TokenResponse chứa tokens mới</returns>
        Task<TokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request, 
        string ipAddress);

    /// <summary>
    /// Generate tokens và update user refresh token trong database
    /// </summary>
    /// <param name="user">ApplicationUser</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <returns>TokenResponse</returns>
    Task<TokenResponse> GenerateTokensAndUpdateUser(
    ApplicationUser user,string ipAddress);
}
```

**Giải thích:**
- **GetTokenAsync:** Login with email/password
- **RefreshTokenAsync:** Renew tokens with refresh token
- **GenerateTokensAndUpdateUser:** Internal method để generate tokens (reusable)
- **ITransientService:** Auto-register as transient (BUILD_08)

**Tại sao ITransientService:**
- Token generation không có state
- Short-lived service per request
- Thread-safe (mỗi request có instance riêng)

---

## 6. Token Service Implementation

### Bước 6.1: TokenService Implementation

**Làm gì:** Implement JWT token generation và validation.

**Tại sao:** Core logic cho authentication system.

**File:** `src/Infrastructure/Infrastructure/Identity/TokenService.cs`

```csharp
using ECO.WebApi.Application.Common.Exceptions;
using ECO.WebApi.Application.Identity.Tokens;
using ECO.WebApi.Domain.Identity;
using ECO.WebApi.Infrastructure.Auth;
using ECO.WebApi.Infrastructure.Auth.Jwt;
using ECO.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ECO.WebApi.Infrastructure.Identity;

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
            new(ECOClaims.Fullname, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(ECOClaims.IpAddress, ipAddress),
            new(ECOClaims.ImageUrl, user.ImageUrl ?? string.Empty),
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
```

**Giải thích chi tiết:**

**GetTokenAsync (Login):**
1. Find user by email
2. Verify password với `UserManager.CheckPasswordAsync`
3. Check user active status
4. Check email confirmation nếu `RequireConfirmedAccount = true`
5. Generate tokens nếu tất cả checks pass

**RefreshTokenAsync (Refresh):**
1. Extract claims từ expired token (không validate lifetime)
2. Find user by email từ claims
3. Verify refresh token match với database
4. Verify refresh token chưa expired
5. Generate new tokens

**GenerateTokensAndUpdateUser:**
1. Generate JWT access token với user claims
2. Generate new refresh token (cryptographically random)
3. Update refresh token và expiry time vào database
4. Return tokens

**Security Features:**
- Password hashing: `UserManager.CheckPasswordAsync` dùng PBKDF2
- Refresh token: Cryptographically random (32 bytes)
- Token signing: HMAC-SHA256
- Claims-based: User info embedded in token
- IP tracking: Log IP address trong claims

**Tại sao lưu refresh token trong database:**
- Server có thể revoke refresh tokens
- Track active sessions
- Logout from all devices
- Security audit trail

---

## 7. JWT Authentication Middleware Setup

### Bước 7.1: ConfigureJwtBearerOptions

**Làm gì:** Configure JWT Bearer authentication options.

**Tại sao:** Customize token validation và error handling.

**File:** `src/Infrastructure/Infrastructure/Auth/Jwt/ConfigureJwtBearerOptions.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace ECO.WebApi.Infrastructure.Auth.Jwt;

/// <summary>
/// Configure JWT Bearer authentication options
/// </summary>
public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSettings _jwtSettings;

    public ConfigureJwtBearerOptions(IOptions<JwtSettings> jwtSettings)
    {
      _jwtSettings = jwtSettings.Value;
    }

    public void Configure(JwtBearerOptions options)
    {
        Configure(string.Empty, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
             return;
        }

        byte[] key = Encoding.ASCII.GetBytes(_jwtSettings.Key);

        options.RequireHttpsMetadata = false; // Allow HTTP trong development
        options.SaveToken = true; // Save token trong AuthenticationProperties
        options.TokenValidationParameters = new TokenValidationParameters
      {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false, // Không validate issuer
            ValidateLifetime = true, // Validate token expiration
            ValidateAudience = false, // Không validate audience
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero // Không có clock skew tolerance
        };
        
        // Custom events
        options.Events = new JwtBearerEvents
        {
             OnChallenge = context => { 
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    throw new UnauthorizedException("Authentication Failed.");
                }
                    return Task.CompletedTask;
            },
            
        OnForbidden = _ => throw new ForbiddenException(
       "You are not authorized to access this resource."),
     
        OnMessageReceived = context =>
        {
        // Support SignalR authentication từ query string
        var accessToken = context.Request.Query["access_token"];

          if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/notifications"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
}
```

**Giải thích:**

**TokenValidationParameters:**
- **ValidateIssuerSigningKey:** Validate token signature
- **IssuerSigningKey:** Secret key để validate signature
- **ValidateIssuer:** `false` (single-tenant app, không cần issuer)
- **ValidateAudience:** `false` (single-tenant app, không cần audience)
- **ValidateLifetime:** `true` (check token expiration)
- **ClockSkew:** `TimeSpan.Zero` (no tolerance, strict expiration)

**Custom Events:**
- **OnChallenge:** 401 Unauthorized response
- **OnForbidden:** 403 Forbidden response
- **OnMessageReceived:** Support SignalR authentication từ query string

**Tại sao ClockSkew = Zero:**
- Default ClockSkew = 5 minutes (Microsoft cho phép token expired 5 phút vẫn valid)
- Production: Set về 0 để strict expiration
- Security best practice

**SignalR Support:**
- SignalR không hỗ trợ custom headers
- Phải gửi token qua query string: `?access_token=xxx`
- OnMessageReceived extract token từ query string

---

### Bước 7.2: JWT Startup Configuration

**Làm gì:** Register JWT authentication services.

**Tại sao:** Modular startup pattern (BUILD_05).

**File:** `src/Infrastructure/Infrastructure/Auth/Jwt/Startup.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECO.WebApi.Infrastructure.Auth.Jwt;

/// <summary>
/// JWT authentication startup configuration
/// </summary>
internal static class Startup
{
    /// <summary>
    /// Add JWT authentication services
    /// </summary>
    internal static IServiceCollection AddJwtAuth(this IServiceCollection services)
    {
    // Bind JwtSettings từ configuration
        services.AddOptions<JwtSettings>()
            .BindConfiguration($"SecuritySettings:{nameof(JwtSettings)}")
            .ValidateDataAnnotations() // Validate với IValidatableObject
            .ValidateOnStart(); // Validate khi app start (fail fast)

        // Register ConfigureJwtBearerOptions
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

    // Add JWT Bearer authentication
        return services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, null!) // Configure bởi ConfigureJwtBearerOptions
            .Services;
    }
}
```

**Giải thích:**
- **BindConfiguration:** Bind từ `SecuritySettings:JwtSettings` trong config
- **ValidateDataAnnotations:** Validate với `IValidatableObject.Validate()`
- **ValidateOnStart:** Fail fast nếu config invalid
- **AddAuthentication:** Set default scheme = JwtBearer
- **AddJwtBearer:** Add JWT Bearer handler (configured bởi ConfigureJwtBearerOptions)

**Tại sao ValidateOnStart:**
- Catch config errors at startup
- Không cần wait đến runtime
- Better developer experience

---

### Bước 7.3: Update Infrastructure Startup

**Làm gì:** Call `AddJwtAuth()` trong Infrastructure startup.

**Tại sao:** Integrate JWT authentication vào modular startup.

**File:** `src/Infrastructure/Infrastructure/Auth/Startup.cs`

```csharp
using ECO.WebApi.Infrastructure.Auth.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Auth;

/// <summary>
/// Auth module startup configuration
/// </summary>
internal static class Startup
{
    internal static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services
            .AddCurrentUser()
            .AddPermissions()
    // JWT Authentication
        .AddJwtAuth();
            
  return services;
    }

    internal static IApplicationBuilder UseAuth(this IApplicationBuilder app)
    {
        return app
         .UseCurrentUser()
     .UseAuthentication()
     .UseAuthorization();
    }
}
```

**Giải thích:**
- **AddJwtAuth():** Register JWT authentication services
- **UseAuthentication():** Add authentication middleware
- **UseAuthorization():** Add authorization middleware

**Middleware Order (QUAN TRỌNG!):**
```
UseRouting()
UseCurrentUser()         // Extract current user từ token
UseAuthentication()      // Validate JWT token
UseAuthorization()     // Check permissions
UseEndpoints()
```

---

## 8. Token Controller

### Bước 8.1: TokensController Implementation

**Làm gì:** Expose token APIs (login, refresh).

**Tại sao:** RESTful API endpoints cho authentication.

**File:** `src/Host/Host/Controllers/Identity/TokensController.cs`

```csharp
using ECO.WebApi.Application.Identity.Tokens;
using NSwag.Annotations;

namespace ECO.WebApi.Host.Controllers.Identity;

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
    [OpenApiOperation("Request an access token using credentials.", "")]
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
  [OpenApiOperation("Request an access token using a refresh token.", "")]
    public Task<TokenResponse> RefreshAsync(RefreshTokenRequest request)
    {
     return _tokenService.RefreshTokenAsync(request, GetIpAddress()!);
    }

    /// <summary>
    /// Get client IP address (support proxy)
    /// </summary>
    private string? GetIpAddress() =>
        Request.Headers.ContainsKey("X-Forwarded-For")
            ? Request.Headers["X-Forwarded-For"]
    : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
}
```

**Giải thích:**

**POST /api/tokens/get:**
- Anonymous access (không cần authentication)
- Login với email + password
- Return access token + refresh token
- Track IP address

**POST /api/tokens/refresh:**
- Anonymous access
- Refresh access token với refresh token
- Return new tokens
- Track IP address

**GetIpAddress():**
- Support proxy với `X-Forwarded-For` header
- Fallback to `RemoteIpAddress`
- Log IP trong token claims (audit trail)

**⚠️ Security Note:**
- `[AllowAnonymous]` là required cho login/refresh endpoints
- Tất cả endpoints khác require authentication by default

---

## 9. Update Shared Layer Claims

### Bước 9.1: ECOClaims Constants

**Làm gì:** Define custom claim types.

**Tại sao:** Type-safe claim names, dễ refactor.

**File:** `src/Core/Shared/Authorization/ECOClaims.cs`

```csharp
namespace ECO.WebApi.Shared.Authorization;

/// <summary>
/// Custom claim types cho ECO.WebApi
/// </summary>
public static class ECOClaims
{
    /// <summary>
    /// Full name claim (FirstName + LastName)
    /// </summary>
    public const string Fullname = "fullName";

    /// <summary>
    /// Permission claim (e.g., "Permissions.Users.View")
  /// </summary>
  public const string Permission = "permission";

    /// <summary>
    /// Image URL claim (avatar)
    /// </summary>
    public const string ImageUrl = "image_url";

    /// <summary>
    /// IP address claim (audit trail)
    /// </summary>
    public const string IpAddress = "ipAddress";

    /// <summary>
    /// Token expiration claim (standard JWT claim)
    /// </summary>
    public const string Expiration = "exp";
}
```

**Giải thích:**
- **Fullname:** Display name trong UI
- **Permission:** Permissions sẽ add sau (BUILD_17)
- **ImageUrl:** Avatar URL
- **IpAddress:** Security audit
- **Expiration:** Standard JWT claim

---

### Bước 9.2: ClaimsPrincipal Extensions

**Làm gì:** Extension methods để extract claims.

**Tại sao:** Reusable, type-safe claim access.

**File:** `src/Core/Shared/Authorization/ClaimsPrincipalExtensions.cs`

```csharp
using System.Security.Claims;

namespace ECO.WebApi.Shared.Authorization;

/// <summary>
/// Extension methods cho ClaimsPrincipal
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Get user email từ claims
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Email);

    /// <summary>
    /// Get user ID từ claims
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Get user full name từ claims
    /// </summary>
    public static string? GetFullName(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ECOClaims.Fullname);

    /// <summary>
    /// Get user first name từ claims
    /// </summary>
    public static string? GetFirstName(this ClaimsPrincipal principal)
      => principal.FindFirstValue(ClaimTypes.Name);

    /// <summary>
    /// Get user surname từ claims
    /// </summary>
    public static string? GetSurname(this ClaimsPrincipal principal)
  => principal.FindFirstValue(ClaimTypes.Surname);

    /// <summary>
    /// Get phone number từ claims
    /// </summary>
  public static string? GetPhoneNumber(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.MobilePhone);

    /// <summary>
  /// Get user image URL từ claims
    /// </summary>
    public static string? GetImageUrl(this ClaimsPrincipal principal)
     => principal.FindFirstValue(ECOClaims.ImageUrl);

 /// <summary>
    /// Get IP address từ claims
    /// </summary>
    public static string? GetIpAddress(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ECOClaims.IpAddress);

    /// <summary>
    /// Find first claim value by type
    /// </summary>
    private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value;
}
```

**Giải thích:**
- Extension methods cho `ClaimsPrincipal`
- Type-safe claim access
- Null-safe (return `string?`)

**Usage Example:**
```csharp
public class MyService
{
    private readonly ICurrentUser _currentUser;
 
    public MyService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }
    
    public void DoSomething()
    {
        var userId = User.GetUserId();
    var email = User.GetEmail();
        var fullName = User.GetFullName();
    }
}
```

---

## 10. Update Domain Entities

### Bước 10.1: Update ApplicationUser (Already exists)

**Làm gì:** Verify ApplicationUser có refresh token fields.

**Tại sao:** Lưu refresh token trong database.

**File:** `src/Core/Domain/Identity/ApplicationUser.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace ECO.WebApi.Domain.Identity;

/// <summary>
/// Application user entity (extends IdentityUser)
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime Dob { get; set; }
    public bool IsActive { get; set; }
 
    // Refresh token fields
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }
    
    public string? ObjectId { get; set; }
}
```

**Giải thích:**
- **RefreshToken:** Lưu refresh token string (Base64)
- **RefreshTokenExpiryTime:** UTC timestamp khi token expire
- Fields này đã có sẵn từ BUILD_03

**⚠️ Security:**
- Refresh token lưu plain text (không hash)
- Có thể hash nếu muốn extra security
- Trade-off: Performance vs Security

---

## 11. Testing JWT Authentication

### Bước 11.1: Test Login API

**API Call:**
```bash
curl -X POST https://localhost:7001/api/tokens/get \
-H "Content-Type: application/json" \
  -d '{
    "email": "admin@root.com",
    "password": "123Pa$$word!"
  }'
```

**Expected Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjNmYTg1ZjY0LTU3MTctNGViMi1iMjVmLTU4NjE2YWEyZmZjYyIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2VtYWlsYWRkcmVzcyI6ImFkbWluQHJvb3QuY29tIiwiZnVsbE5hbWUiOiJBZG1pbiBVc2VyIiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvbmFtZSI6IkFkbWluIiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvc3VybmFtZSI6IlVzZXIiLCJpcEFkZHJlc3MiOiI6OjEiLCJpbWFnZV91cmwiOiIiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9tb2JpbGVwaG9uZSI6IiIsImV4cCI6MTczODMwNDUwMH0.Kqw5HJZ3pQ9X5Y7W9Z8V7X6Y5W4Z3Q2W1X0Y9Z8V7X6",
  "refreshToken": "CfDJ8OxM3L2N1K0J9I8H7G6F5E4D3C2B1A0Z9Y8X7W6V5U4T3S2R1Q0P",
  "refreshTokenExpiryTime": "2024-02-28T10:30:00.000Z"
}
```

**Decode JWT (jwt.io):**
```json
{
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "3fa85f64-5717-4eb2-b25f-58616aa2ffcc",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "admin@root.com",
  "fullName": "Admin User",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "Admin",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname": "User",
  "ipAddress": "::1",
  "image_url": "",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/mobilephone": "",
  "exp": 1738304500
}
```

---

### Bước 11.2: Test Authenticated Request

**API Call:**
```bash
curl -X GET https://localhost:7001/api/users/me \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Expected Response:**
```json
{
  "id": "3fa85f64-5717-4eb2-b25f-58616aa2ffcc",
  "email": "admin@root.com",
  "firstName": "Admin",
  "lastName": "User",
  "fullName": "Admin User"
}
```

---

### Bước 11.3: Test Refresh Token

**API Call:**
```bash
curl -X POST https://localhost:7001/api/tokens/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "CfDJ8OxM3L2N1K0J9I8H7G6F5E4D3C2B1A0Z9Y8X7W6V5U4T3S2R1Q0P"
  }'
```

**Expected Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...(new token)",
  "refreshToken": "CfDJ8NEW...(new refresh token)",
  "refreshTokenExpiryTime": "2024-03-30T10:30:00.000Z"
}
```

---

### Bước 11.4: Test Error Cases

**Case 1: Invalid Credentials**
```bash
curl -X POST https://localhost:7001/api/tokens/get \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@root.com",
  "password": "wrong-password"
  }'
```

**Response:**
```json
{
  "statusCode": 401,
  "message": "Authentication Failed."
}
```

---

**Case 2: Inactive User**
```json
{
  "statusCode": 401,
  "message": "User Not Active. Please contact the administrator."
}
```

---

**Case 3: Email Not Confirmed (if RequireConfirmedAccount = true)**
```json
{
  "statusCode": 401,
  "message": "E-Mail not confirmed."
}
```

---

**Case 4: Invalid Refresh Token**
```bash
curl -X POST https://localhost:7001/api/tokens/refresh \
  -H "Content-Type: application/json" \
  -d '{
 "token": "valid-token",
    "refreshToken": "invalid-refresh-token"
  }'
```

**Response:**
```json
{
  "statusCode": 401,
  "message": "Invalid Refresh Token."
}
```

---

**Case 5: Expired Refresh Token**
```json
{
  "statusCode": 401,
  "message": "Invalid Refresh Token."
}
```

---

## 12. Summary

### ✅ Đã hoàn thành trong bước này:

**Configuration:**
- ✅ JwtSettings với validation
- ✅ SecuritySettings
- ✅ Configuration file (security.json)

**Token DTOs:**
- ✅ TokenRequest với FluentValidation
- ✅ TokenResponse
- ✅ RefreshTokenRequest

**Token Service:**
- ✅ ITokenService interface
- ✅ TokenService implementation
  - Generate JWT với user claims
  - Generate refresh token (cryptographically secure)
  - Validate credentials
  - Refresh token flow

**JWT Middleware:**
- ✅ ConfigureJwtBearerOptions
- ✅ JWT Startup configuration
- ✅ Integration với Infrastructure startup

**Controllers:**
- ✅ TokensController (login, refresh)
- ✅ IP address tracking

**Shared Layer:**
- ✅ ECOClaims constants
- ✅ ClaimsPrincipal extensions

### 📊 Authentication Flow:

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ 1. POST /api/tokens/get
    │    { email, password }
       ▼
┌─────────────┐
│TokenService │
│  - Validate │
│  - Generate │
└──────┬──────┘
       │ 2. Return tokens
    │    { accessToken, refreshToken }
       ▼
┌─────────────┐
│   Client    │
│  Store      │
│  tokens     │
└──────┬──────┘
       │ 3. Authenticated requests
       │    Authorization: Bearer {accessToken}
       ▼
┌─────────────────┐
│ JWT Middleware│
│  - Validate     │
│  - Extract user │
└──────┬──────────┘
│ 4. User.Identity
       ▼
┌─────────────┐
│ Controller  │
│  Process  │
└─────────────┘

When token expires:

┌─────────────┐
│   Client    │
└──────┬──────┘
       │ 1. POST /api/tokens/refresh
       │    { token, refreshToken }
    ▼
┌─────────────┐
│TokenService │
│  - Validate │
│  - Generate │
└──────┬──────┘
     │ 2. Return new tokens
▼
┌─────────────┐
│   Client    │
│  Update     │
│  tokens     │
└─────────────┘
```

### 📌 Key Concepts:

**JWT (JSON Web Token):**
- Self-contained token chứa user claims
- Signed bằng secret key (HMAC-SHA256)
- Stateless (không cần server-side session)
- Structure: `header.payload.signature`

**Access Token:**
- Short-lived (15-60 minutes, hoặc 7 days cho development)
- Gửi với mọi authenticated request
- Chứa user claims (id, email, name, permissions)
- Cannot revoke (until expired)

**Refresh Token:**
- Long-lived (7-30 days)
- Lưu trong database (có thể revoke)
- Dùng để renew access token
- Cryptographically random (32 bytes)

**Claims:**
- User information embedded trong JWT
- Type-safe access với extension methods
- Standard claims: NameIdentifier, Email, Name, Surname
- Custom claims: Fullname, ImageUrl, IpAddress, Permission

**Security Features:**
- Password hashing (PBKDF2)
- Token signing (HMAC-SHA256)
- Refresh token rotation
- IP tracking
- Email confirmation support
- Account activation check

### 📁 File Structure:

```
src/
├── Core/
│   ├── Application/
│   │   └── Identity/
│   │       └── Tokens/
│   │ ├── ITokenService.cs
│   │           ├── TokenRequest.cs
│   │  ├── TokenResponse.cs
│   │ └── RefreshTokenRequest.cs
│   ├── Domain/
│   │   └── Identity/
│   │       └── ApplicationUser.cs (RefreshToken fields)
│   └── Shared/
│       └── Authorization/
│     ├── ECOClaims.cs
│  └── ClaimsPrincipalExtensions.cs
├── Infrastructure/
│   └── Infrastructure/
│       ├── Auth/
│       │   ├── SecuritySettings.cs
│       │   ├── Startup.cs (AddAuth)
│       │   └── Jwt/
││       ├── JwtSettings.cs
│       │       ├── ConfigureJwtBearerOptions.cs
│       │       └── Startup.cs (AddJwtAuth)
│       └── Identity/
│      └── TokenService.cs
└── Host/
    └── Host/
   ├── Controllers/
        │   └── Identity/
        │       └── TokensController.cs
        └── Configurations/
            └── security.json
```

---

## 13. Next Steps

**Tiếp theo:** [BUILD_16A - User Service](BUILD_16A_User_Service.md)

Trong bước tiếp theo, chúng ta sẽ xây dựng User Management Service:
1. ✅ User CRUD operations
2. ✅ User registration với email confirmation
3. ✅ Change password, reset password
4. ✅ User search và pagination
5. ✅ Assign roles to users
6. ✅ User DTOs và specifications
7. ✅ User validation rules

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
