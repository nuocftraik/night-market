# OAuth2 Integration - Social Login (Google & Facebook)

> 📚 [Quay lại Mục lục](BUILD_INDEX.md)  
> 📋 **Prerequisites:** Bước 17 (Permission Authorization) đã hoàn thành

Tài liệu này hướng dẫn xây dựng OAuth2 Integration - Social Login với Google và Facebook.

---

## 1. Overview (Tổng quan)

**Làm gì:** Xây dựng OAuth2 authentication để cho phép users đăng nhập bằng tài khoản Google hoặc Facebook.

**Tại sao cần:**
- **User Convenience (Tiện lợi cho User):** Users không cần tạo tài khoản mới, dùng tài khoản social có sẵn
- **Faster Registration (Đăng ký Nhanh):** Không cần điền form đăng ký dài, lấy thông tin từ social profile
- **Trusted Authentication (Xác thực Tin cậy):** Dùng OAuth2 providers đáng tin cậy (Google, Facebook)
- **Email Verification (Xác minh Email):** Email đã được verify bởi provider, không cần gửi email xác nhận
- **Better UX (Trải nghiệm Tốt hơn):** One-click login, không cần nhớ password

**Trong bước này chúng ta sẽ:**
- ✅ Tạo GoogleAuthSettings configuration
- ✅ Tạo FacebookAuthSettings configuration
- ✅ Setup OAuth2 middleware trong Startup
- ✅ Tạo IAuthenticationService interface
- ✅ Implement AuthenticationService (GoogleSignIn, FacebookSignIn)
- ✅ Tạo AuthController cho OAuth2 endpoints
- ✅ Configuration trong appsettings
- ✅ Testing với Google/Facebook login

**Real-world example (Ví dụ thực tế):**
```csharp
// User click "Login with Google" button trên frontend
// Frontend gọi Google OAuth2 và nhận ID Token
// Frontend gửi ID Token đến API

// API Controller
[HttpPost("google")]
[AllowAnonymous]
public async Task<IActionResult> GoogleLogin([FromBody] OAuthRequest request)
{
    // Validate ID Token với Google
    // Tạo hoặc tìm user trong database
    // Generate JWT token cho user
    var response = await _authenticationService.GoogleSignIn(
        request.IdToken, 
        GetIpAddress());
 
    return Ok(response); // Trả về JWT token
}

// OAuth2 Flow:
// 1. User click "Login with Google"
// 2. Redirect đến Google login page
// 3. User đăng nhập và cho phép app access
// 4. Google trả về ID Token
// 5. Frontend gửi ID Token đến API
// 6. API validate token với Google
// 7. API tạo user (nếu chưa có) và generate JWT
// 8. API trả về JWT token cho frontend
```

---

## 2. OAuth2 Authentication Architecture (Kiến trúc OAuth2 Authentication)

### Bước 2.1: OAuth2 Flow Overview (Tổng quan Luồng OAuth2)

**Complete OAuth2 Flow Diagram (Sơ đồ Luồng OAuth2 Hoàn chỉnh):**

```
┌─────────────────────────────────────────────────────────┐
│         OAUTH2 AUTHENTICATION FLOW (GOOGLE)             │
└─────────────────────────────────────────────────────────┘

1. USER CLICK "LOGIN WITH GOOGLE"
┌──────────────┐
│  Frontend    │
│  (React/Vue) │
└──────┬───────┘
       │ User clicks "Login with Google"
       ▼
┌──────────────────────┐
│  Google OAuth2 SDK   │
│  (Frontend)        │
└──────┬───────────────┘
       │ Redirect to Google Login Page
       ▼
┌──────────────────────┐
│  Google Login Page   │
│  accounts.google.com │
└──────┬───────────────┘
       │ User enters Google credentials
       │ User grants permissions
       ▼
┌──────────────────────┐
│  Google OAuth2       │
│  Returns ID Token    │
└──────┬───────────────┘
       │ ID Token (JWT signed by Google)
       ▼
┌──────────────────────┐
│  Frontend            │
│  Receives ID Token   │
└──────┬───────────────┘
       │
       ▼

2. FRONTEND GỬI ID TOKEN ĐẾN API
┌──────────────────────┐
│ POST /api/auth/google│
│ Body: { "idToken": "..." }
└──────┬───────────────┘
       │
       ▼
┌─────────────────────────┐
│  AuthController         │
│  GoogleLogin(request)   │
└──────┬──────────────────┘
       │
       ▼
┌─────────────────────────────────┐
│  AuthenticationService       │
│  GoogleSignIn(idToken, ip)      │
└──────┬──────────────────────────┘
       │ 1. Validate ID Token với Google
       │    GoogleJsonWebSignature.ValidateAsync()
       │ 2. Extract user info (email, name, avatar)
       ▼
┌──────────────────────────┐
│  Check User Exists       │
│  FindByEmailAsync()      │
└──────┬───────────────────┘
       │
       ├─► User không tồn tại
       │   └─> Tạo user mới
       │       - Email từ Google
       │     - FirstName, LastName từ Google
       │    - EmailConfirmed = true
       │       - Assign role "Basic"
       │
       └─► User đã tồn tại
        └─> Dùng user hiện có
    
       ▼
┌──────────────────────────┐
│  TokenService          │
│  GenerateTokensAndUpdate │
└──────┬───────────────────┘
       │ Generate JWT với permissions
       ▼
┌──────────────────────┐
│  TokenResponse       │
│  {                   │
│    token: "...",     │
│    refreshToken "..."│
│  }                   │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────┐
│  Frontend            │
│  Store token         │
│  Redirect to app     │
└──────────────────────┘

✅ User đã đăng nhập thành công!
```

---

### Bước 2.2: Key Components (Các Thành phần Chính)

**1. GoogleAuthSettings / FacebookAuthSettings:**
- Configuration cho OAuth2 providers
- ClientId/AppId, ClientSecret/AppSecret
- Lấy từ Google Cloud Console / Facebook Developers

**2. OAuth2 Startup Configuration:**
- Register Google/Facebook authentication middleware
- Configure authentication options
- Integration với ASP.NET Core Authentication

**3. IAuthenticationService (Interface Dịch vụ Xác thực):**
- `GoogleSignIn(idToken, ipAddress)`: Đăng nhập bằng Google ID Token
- `GoogleSignIn2(authorizationCode, ipAddress)`: Đăng nhập bằng Authorization Code Flow
- `FacebookSignIn(idToken, ipAddress)`: Đăng nhập bằng Facebook

**4. AuthenticationService (Implementation):**
- Validate ID Token với provider
- Extract user information
- Create/find user trong database
- Generate JWT token

**5. AuthController:**
- Endpoints cho OAuth2 login
- `/api/auth/google` - Google login
- `/api/auth/facebook` - Facebook login

---

## 3. OAuth2 Configuration (Cấu hình OAuth2)

### Bước 3.1: GoogleAuthSettings (Cấu hình Google)

**Làm gì:** Configuration class để lưu Google OAuth2 credentials.

**Tại sao:** Centralize configuration, dễ quản lý và thay đổi.

**File:** `src/Infrastructure/Infrastructure/Auth/OAuth2/GoogleAuthSettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Auth.OAuth2;

/// <summary>
/// Google OAuth2 authentication settings
/// Lấy từ appsettings.json section "Authentication:Google"
/// </summary>
public class GoogleAuthSettings
{
    /// <summary>
    /// Section name trong appsettings.json
    /// </summary>
    public const string SectionName = "Authentication:Google";

    /// <summary>
    /// Google OAuth2 Client ID
    /// Lấy từ Google Cloud Console
    /// </summary>
    public string ClientId { get; set; } = default!;

    /// <summary>
 /// Google OAuth2 Client Secret
    /// Lấy từ Google Cloud Console
    /// </summary>
    public string ClientSecret { get; set; } = default!;
}
```

**Giải thích:**

**GoogleAuthSettings Properties:**
- **SectionName:** Tên section trong appsettings.json (`"Authentication:Google"`)
- **ClientId:** OAuth2 Client ID từ Google Cloud Console
- **ClientSecret:** OAuth2 Client Secret từ Google Cloud Console

**Cách lấy Google OAuth2 Credentials:**
1. Truy cập [Google Cloud Console](https://console.cloud.google.com/)
2. Tạo project mới hoặc chọn project có sẵn
3. Enable Google+ API
4. Tạo OAuth2 credentials (Web application)
5. Copy Client ID và Client Secret

---

### Bước 3.2: FacebookAuthSettings (Cấu hình Facebook)

**Làm gì:** Configuration class để lưu Facebook OAuth2 credentials.

**Tại sao:** Centralize configuration cho Facebook authentication.

**File:** `src/Infrastructure/Infrastructure/Auth/OAuth2/FacebookAuthSettings.cs`

```csharp
namespace ECO.WebApi.Infrastructure.Auth.OAuth2;

/// <summary>
/// Facebook OAuth2 authentication settings
/// Lấy từ appsettings.json section "Authentication:Facebook"
/// </summary>
public class FacebookAuthSettings
{
    /// <summary>
    /// Section name trong appsettings.json
    /// </summary>
    public const string SectionName = "Authentication:Facebook";

    /// <summary>
    /// Facebook App ID
    /// Lấy từ Facebook Developers Console
    /// </summary>
    public string AppId { get; set; } = default!;

    /// <summary>
 /// Facebook App Secret
    /// Lấy từ Facebook Developers Console
    /// </summary>
    public string AppSecret { get; set; } = default!;
}
```

**Giải thích:**

**FacebookAuthSettings Properties:**
- **SectionName:** Tên section trong appsettings.json (`"Authentication:Facebook"`)
- **AppId:** Facebook App ID từ Facebook Developers
- **AppSecret:** Facebook App Secret từ Facebook Developers

**Cách lấy Facebook OAuth2 Credentials:**
1. Truy cập [Facebook Developers](https://developers.facebook.com/)
2. Tạo app mới (hoặc chọn app có sẵn)
3. Setup Facebook Login product
4. Copy App ID và App Secret từ Settings > Basic

---

## 4. OAuth2 Middleware Setup (Thiết lập OAuth2 Middleware)

### Bước 4.1: OAuth2 Startup Configuration

**Làm gì:** Register OAuth2 authentication providers trong dependency injection.

**Tại sao:** Configure ASP.NET Core Authentication với Google/Facebook providers.

**File:** `src/Infrastructure/Infrastructure/Auth/OAuth2/Startup.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECO.WebApi.Infrastructure.Auth.OAuth2;

internal static class Startup
{
    /// <summary>
    /// Thêm OAuth2 authentication providers (Google, Facebook)
    /// </summary>
    internal static IServiceCollection AddO2Authentication(
  this IServiceCollection services, 
      IConfiguration configuration)
    {
        // Đăng ký GoogleAuthSettings từ configuration
        services.Configure<GoogleAuthSettings>(
            configuration.GetSection(GoogleAuthSettings.SectionName));

        // Đăng ký FacebookAuthSettings từ configuration
        services.Configure<FacebookAuthSettings>(
        configuration.GetSection(FacebookAuthSettings.SectionName));

        // Thêm authentication schemes
        services.AddAuthentication()
            // Add Google authentication
        .AddGoogle(googleOptions =>
         {
         // Lấy Google settings từ configuration
         var googleAuthSettings = configuration
            .GetSection(GoogleAuthSettings.SectionName)
            .Get<GoogleAuthSettings>();

        // Cấu hình Google OAuth2
         googleOptions.ClientId = googleAuthSettings.ClientId;
                googleOptions.ClientSecret = googleAuthSettings.ClientSecret;
          })
         // Add Facebook authentication
        .AddFacebook(facebookOptions =>
            {
           // Lấy Facebook settings từ configuration
        var facebookSettings = configuration
             .GetSection(FacebookAuthSettings.SectionName)
            .Get<FacebookAuthSettings>();

                // Cấu hình Facebook OAuth2
        facebookOptions.AppId = facebookSettings.AppId;
        facebookOptions.AppSecret = facebookSettings.AppSecret;
      });

      return services;
    }
}
```

**Giải thích:**

**AddO2Authentication Method:**
1. **Configure Settings:** Bind configuration sections vào settings classes
2. **AddAuthentication():** Add authentication services vào DI container
3. **AddGoogle():** Configure Google authentication provider
4. **AddFacebook():** Configure Facebook authentication provider

**Authentication Providers:**
- **Google:** Sử dụng `Microsoft.AspNetCore.Authentication.Google`
- **Facebook:** Sử dụng `Microsoft.AspNetCore.Authentication.Facebook`

**Why Register Settings:**
- Settings có thể inject vào services khác
- Centralized configuration management
- Easy to change without code modification

---

## 5. Add Required Packages (Thêm Packages cần thiết)

### Bước 5.1: Add Google Authentication Package

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Google OAuth2 Authentication -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="8.0.0" />
    
    <!-- Google API để validate ID Token -->
    <PackageReference Include="Google.Apis.Auth" Version="1.68.0" />
    
    <!-- Google Drive API (nếu cần) -->
    <PackageReference Include="Google.Apis.Drive.v3" Version="1.68.0.3574" />
</ItemGroup>
```

**Giải thích packages:**
- `Microsoft.AspNetCore.Authentication.Google`: Google OAuth2 authentication provider cho ASP.NET Core
- `Google.Apis.Auth`: Validate Google ID Token và extract user info
- `Google.Apis.Drive.v3`: (Optional) Google Drive integration

---

### Bước 5.2: Add Facebook Authentication Package

**File:** `src/Infrastructure/Infrastructure/Infrastructure.csproj`

```xml
<ItemGroup>
    <!-- Facebook OAuth2 Authentication -->
  <PackageReference Include="Microsoft.AspNetCore.Authentication.Facebook" Version="8.0.0" />
</ItemGroup>
```

**Giải thích packages:**
- `Microsoft.AspNetCore.Authentication.Facebook`: Facebook OAuth2 authentication provider cho ASP.NET Core

---

## 6. Authentication Service Implementation (Triển khai Dịch vụ Xác thực)

### Bước 6.1: IAuthenticationService Interface

**Làm gì:** Define interface cho authentication service.

**Tại sao:** Abstraction để dễ test và thay đổi implementation.

**File:** `src/Core/Application/Identity/O2Auth/IAuthenticationService.cs`

```csharp
using ECO.WebApi.Application.Identity.Tokens;

namespace ECO.WebApi.Application.Identity.O2Auth;

/// <summary>
/// Authentication service cho OAuth2 social login
/// </summary>
public interface IAuthenticationService : ITransientService
{
    /// <summary>
    /// Đăng nhập bằng Google ID Token
    /// ID Token Flow: Frontend validate với Google và gửi ID Token cho API
    /// </summary>
    /// <param name="idToken">Google ID Token từ frontend</param>
    /// <param name="ipAddress">IP address của user</param>
    /// <returns>JWT token response</returns>
    Task<TokenResponse> GoogleSignIn(string idToken, string ipAddress);

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code
    /// Authorization Code Flow: API trao đổi code để lấy token từ Google
    /// </summary>
    /// <param name="authorizedCode">Authorization code từ Google</param>
    /// <param name="ipAddress">IP address của user</param>
    /// <returns>JWT token response</returns>
    Task<TokenResponse> GoogleSignIn2(string authorizedCode, string ipAddress);

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token
    /// </summary>
    /// <param name="idToken">Facebook Access Token từ frontend</param>
    /// <param name="ipAddress">IP address của user</param>
    /// <returns>JWT token response</returns>
    Task<TokenResponse> FacebookSignIn(string idToken, string ipAddress);
}
```

**Giải thích:**

**Interface Methods:**
- **GoogleSignIn(idToken, ipAddress):**
  - ID Token Flow (Implicit Flow)
  - Frontend validate với Google
  - API chỉ cần validate ID Token signature

- **GoogleSignIn2(authorizedCode, ipAddress):**
  - Authorization Code Flow
  - API trao đổi code với Google để lấy tokens
  - Secure hơn nhưng phức tạp hơn

- **FacebookSignIn(idToken, ipAddress):**
  - Facebook Access Token flow
  - Tương tự Google ID Token flow

**ITransientService:**
- Authentication service là transient (mỗi request tạo instance mới)
- Không có state, safe để dispose sau mỗi request

---

### Bước 6.2: AuthenticationService Implementation (Google ID Token Flow)

**Làm gì:** Implement Google login bằng ID Token.

**Tại sao:** ID Token Flow đơn giản và phổ biến nhất.

**File:** `src/Infrastructure/Infrastructure/Identity/AuthenticationService.cs`

```csharp
using ECO.WebApi.Application.Identity.O2Auth;
using ECO.WebApi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Google.Apis.Auth;
using ECO.WebApi.Infrastructure.Auth.OAuth2;
using Microsoft.Extensions.Options;
using ECO.WebApi.Application.Identity.Tokens;
using ECO.WebApi.Shared.Authorization;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2;
using ECO.WebApi.Application.Common.Interfaces;

namespace ECO.WebApi.Infrastructure.Identity;

/// <summary>
/// Authentication service implementation cho OAuth2 social login
/// </summary>
public class AuthenticationService : IAuthenticationService
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
        // 1. Validate ID Token với Google
    // GoogleJsonWebSignature.ValidateAsync kiểm tra:
        // - Token signature (signed by Google)
        // - Token expiration
        // - Audience (ClientId match)
    var payload = await GoogleJsonWebSignature
        .ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings
    {
   Audience = new[] { _googleAuthConfig.ClientId }
 });

        // 2. Extract user info từ payload
        var emailLogin = payload.Email;

        // 3. Tìm user trong database
      var existingUser = await _userManager.FindByEmailAsync(
            emailLogin.Trim().Normalize());

        // 4. Nếu user chưa tồn tại → Tạo user mới
   if (existingUser == null)
     {
  existingUser = new ApplicationUser
    {
       Email = emailLogin,
           FirstName = payload.GivenName,    // First name từ Google
     LastName = payload.FamilyName,     // Last name từ Google
    UserName = payload.Email,// Username = Email
    EmailConfirmed = true,             // Email đã verified bởi Google
                IsActive = true        // Active ngay
          };

            // Tạo user trong database
        await _userManager.CreateAsync(existingUser);

     // Gán role "Basic" cho user mới
         await _userManager.AddToRoleAsync(existingUser, ECORoles.Basic);
 }

        // Note: Nếu user đã tồn tại nhưng email chưa link với Google account
        // có thể thêm logic check và link accounts ở đây

        // 5. Generate JWT token cho user
        var generateToken = await _tokenService.GenerateTokensAndUpdateUser(
            existingUser, 
   ipAddress);

  return generateToken;
 }

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code
    /// Authorization Code Flow (secure hơn ID Token Flow)
    /// </summary>
    public async Task<TokenResponse> GoogleSignIn2(
        string authorizationCode, 
        string ipAddress)
    {
        // 1. Cấu hình Google Authorization Code Flow
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

      // 2. Trao đổi Authorization Code để lấy Access Token
   var tokenResponse = await googleAuthorizationCodeFlow.ExchangeCodeForTokenAsync(
       userId: "me",
 code: authorizationCode,
   redirectUri: "https://localhost:7001/auth/callback", // Redirect URI phải match với Google Console
   CancellationToken.None
        );

        // 3. Lấy thông tin user từ Google API bằng Access Token
        using var httpClient = new HttpClient();
  var userInfoResponse = await httpClient.GetStringAsync(
     $"https://www.googleapis.com/oauth2/v2/userinfo?access_token={tokenResponse.AccessToken}"
        );

        // 4. Deserialize user info response
     var userInfo = _serializerService.Deserialize<GoogleUserInfo>(userInfoResponse);

        // 5. Kiểm tra hoặc tạo user trong database
        var emailLogin = userInfo.Email;
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
            await _userManager.AddToRoleAsync(existingUser, ECORoles.Basic);
   }

        // 6. Generate JWT token
        var generateToken = await _tokenService.GenerateTokensAndUpdateUser(
      existingUser, 
ipAddress);

   return generateToken;
    }

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token
    /// TODO: Implement Facebook login logic
    /// </summary>
    public Task<TokenResponse> FacebookSignIn(string token, string ipAddress)
    {
        throw new NotImplementedException("Facebook login chưa được implement");
    }
}

/// <summary>
/// Google user info model (từ Google API response)
/// </summary>
public class GoogleUserInfo
{
    public string Email { get; set; } = default!;
    public string GivenName { get; set; } = default!;
    public string FamilyName { get; set; } = default!;
    public string Picture { get; set; } = default!;
}
```

**Giải thích:**

**GoogleSignIn Method Flow:**
1. **Validate ID Token:** `GoogleJsonWebSignature.ValidateAsync()` kiểm tra token
2. **Extract User Info:** Lấy email, name từ payload
3. **Find User:** Tìm user trong database bằng email
4. **Create User (nếu chưa có):** Tạo user mới với info từ Google
5. **Generate JWT:** Tạo JWT token cho user

**Why EmailConfirmed = true:**
- Google đã verify email của user
- Không cần gửi email xác nhận
- User có thể login ngay

**Why AddToRoleAsync(ECORoles.Basic):**
- User mới được gán role "Basic" (không phải Admin)
- Admin phải được tạo thủ công hoặc promote từ Basic

**GoogleSignIn2 (Authorization Code Flow):**
- Secure hơn ID Token Flow
- API trao đổi code với Google backend-to-backend
- Frontend không thấy Access Token
- Phức tạp hơn nhưng recommend cho production

---

## 7. AuthController - OAuth2 Endpoints (Controller cho OAuth2)

### Bước 7.1: AuthController Implementation

**Làm gì:** Tạo controller với OAuth2 login endpoints.

**Tại sao:** Expose REST APIs cho OAuth2 authentication.

**File:** `src/Host/Host/Controllers/Identity/AuthController.cs`

```csharp
using ECO.WebApi.Application.Identity.O2Auth;
using NSwag.Annotations;

namespace ECO.WebApi.Host.Controllers.Identity;

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
    /// Đăng nhập bằng Google ID Token
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [OpenApiOperation("Đăng nhập bằng Google ID Token")]
    public async Task<IActionResult> GoogleLogin([FromBody] OAuthRequest request)
    {
        var response = await _authenticationService.GoogleSignIn(
            request.IdToken, 
   GetIpAddress()!);

   return Ok(response);
    }

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code
    /// </summary>
    [HttpPost("google2")]
    [AllowAnonymous]
    [OpenApiOperation("Đăng nhập bằng Google Authorization Code")]
    public async Task<IActionResult> GoogleLogin2([FromBody] string authorizedCode)
    {
        var response = await _authenticationService.GoogleSignIn2(
            authorizedCode, 
       GetIpAddress()!);

        return Ok(response);
    }

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token
  /// </summary>
[HttpPost("facebook")]
    [AllowAnonymous]
    [OpenApiOperation("Đăng nhập bằng Facebook Access Token")]
    public async Task<IActionResult> FacebookLogin([FromBody] OAuthRequest request)
    {
        var response = await _authenticationService.FacebookSignIn(
     request.IdToken, 
    GetIpAddress()!);

        return Ok(response);
    }

    /// <summary>
    /// Helper method để lấy IP address của client
    /// </summary>
    private string? GetIpAddress() =>
        Request.Headers.ContainsKey("X-Forwarded-For")
  ? Request.Headers["X-Forwarded-For"]
       : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
}

/// <summary>
/// OAuth request model
/// </summary>
public class OAuthRequest
{
    /// <summary>
    /// ID Token từ OAuth provider (Google/Facebook)
    /// </summary>
    public string IdToken { get; set; } = default!;
}
```

**Giải thích:**

**AuthController Endpoints:**
- **POST /api/auth/google:** Google login bằng ID Token
- **POST /api/auth/google2:** Google login bằng Authorization Code
- **POST /api/auth/facebook:** Facebook login bằng Access Token

**AllowAnonymous:**
- OAuth endpoints không cần authentication
- Bất kỳ ai cũng có thể login

**GetIpAddress():**
- Lấy IP address của client
- Check X-Forwarded-For header (nếu đằng sau proxy/load balancer)
- Fallback sang RemoteIpAddress

**OAuthRequest Model:**
- Simple model chứa IdToken
- IdToken = Google ID Token hoặc Facebook Access Token

---

## 8. Configuration Setup (Thiết lập Cấu hình)

### Bước 8.1: appsettings.json Configuration

**Làm gì:** Add OAuth2 configuration vào appsettings.json.

**Tại sao:** Centralize configuration, không hardcode credentials trong code.

**File:** `src/Host/Host/appsettings.json`

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-google-client-secret"
    },
    "Facebook": {
      "AppId": "your-facebook-app-id",
      "AppSecret": "your-facebook-app-secret"
    }
  }
}
```

**Giải thích:**

**Google Configuration:**
- **ClientId:** Google OAuth2 Client ID từ Google Cloud Console
- **ClientSecret:** Google OAuth2 Client Secret

**Facebook Configuration:**
- **AppId:** Facebook App ID từ Facebook Developers
- **AppSecret:** Facebook App Secret

**⚠️ Security Note:**
- **KHÔNG** commit credentials vào git
- Dùng User Secrets cho development
- Dùng Environment Variables cho production

---

### Bước 8.2: User Secrets Setup (Development)

**Làm gì:** Dùng User Secrets để lưu credentials trong development.

**Tại sao:** Không commit sensitive data vào git.

**Commands:**

```bash
# Initialize user secrets
cd src/Host/Host
dotnet user-secrets init

# Set Google credentials
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-client-secret"

# Set Facebook credentials
dotnet user-secrets set "Authentication:Facebook:AppId" "your-app-id"
dotnet user-secrets set "Authentication:Facebook:AppSecret" "your-app-secret"
```

**Giải thích:**
- User Secrets lưu trong `%APPDATA%\Microsoft\UserSecrets\`
- Chỉ available trong development environment
- Không được commit vào git

---

## 9. Testing OAuth2 Integration (Kiểm thử OAuth2)

### Bước 9.1: Setup Google OAuth2 Test

**Prerequisites (Điều kiện tiên quyết):**
1. Google Cloud Console project
2. OAuth2 credentials created
3. Authorized redirect URIs configured

**Frontend Test Code (React example):**

```javascript
import { GoogleLogin } from '@react-oauth/google';

function LoginButton() {
    const handleGoogleSuccess = async (credentialResponse) => {
// credentialResponse.credential = Google ID Token

        // Gửi ID Token đến API
        const response = await fetch('https://localhost:7001/api/auth/google', {
    method: 'POST',
            headers: {
      'Content-Type': 'application/json'
  },
            body: JSON.stringify({
          idToken: credentialResponse.credential
       })
    });

        const data = await response.json();
        
        // Lưu JWT token
        localStorage.setItem('token', data.token);
    localStorage.setItem('refreshToken', data.refreshToken);
        
  // Redirect to app
        window.location.href = '/dashboard';
    };

    return (
        <GoogleLogin
       onSuccess={handleGoogleSuccess}
     onError={() => console.log('Login Failed')}
 />
    );
}
```

---

### Bước 9.2: Test Google Login với Postman

**Step 1: Get Google ID Token (Lấy Google ID Token)**

**Cách 1 - Dùng Google OAuth2 Playground:**
1. Truy cập https://developers.google.com/oauthplayground/
2. Click "Settings" icon → Check "Use your own OAuth credentials"
3. Nhập Client ID và Client Secret
4. Select scope: `https://www.googleapis.com/auth/userinfo.email`
5. Click "Authorize APIs"
6. Login với Google account
7. Click "Exchange authorization code for tokens"
8. Copy `id_token` từ response

**Cách 2 - Dùng Frontend test page:**
```html
<!DOCTYPE html>
<html>
<head>
    <title>Google Login Test</title>
    <script src="https://accounts.google.com/gsi/client" async defer></script>
</head>
<body>
    <div id="g_id_onload"
         data-client_id="YOUR_CLIENT_ID"
         data-callback="handleCredentialResponse">
    </div>
    <div class="g_id_signin" data-type="standard"></div>

    <script>
        function handleCredentialResponse(response) {
         console.log("ID Token:", response.credential);
            // Copy ID Token này để test trong Postman
        }
    </script>
</body>
</html>
```

**Step 2: Call API với Postman**

**API Call:**
```http
POST https://localhost:7001/api/auth/google
Content-Type: application/json

{
    "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjE4MmU0O..."
}
```

**Expected Response (Kết quả mong đợi):**
```json
{
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "CfDJ8O...",
    "refreshTokenExpiryTime": "2024-03-01T10:00:00Z"
}
```

**✅ Success Indicators (Dấu hiệu thành công):**
- API trả về 200 OK
- Response chứa `token` và `refreshToken`
- User mới được tạo trong database (nếu chưa tồn tại)
- User có role "Basic"
- EmailConfirmed = true

---

### Bước 9.3: Verify JWT Token

**Decode JWT Token trên jwt.io:**

```json
{
  "nameid": "user-id-from-database",
  "email": "user@gmail.com",
  "fullName": "John Doe",
  "permission": "Permissions.Dashboard.View",
  "exp": 1706529600,
  "iss": "ECO.WebApi",
  "aud": "ECO.WebApi"
}
```

**✅ Verify (Xác minh):**
- Email match với Google account
- Fullname match với Google profile
- Permissions được gán đúng (theo role Basic)

---

### Bước 9.4: Test Error Cases (Test Trường hợp Lỗi)

**Scenario 1: Invalid ID Token (Token không hợp lệ)**

**API Call:**
```http
POST https://localhost:7001/api/auth/google
Content-Type: application/json

{
    "idToken": "invalid-token"
}
```

**Expected Response:**
```json
{
    "statusCode": 401,
    "message": "Invalid Google ID Token"
}
```

---

**Scenario 2: Expired ID Token (Token hết hạn)**

**API Call:**
```http
POST https://localhost:7001/api/auth/google
Content-Type: application/json

{
 "idToken": "expired-token"
}
```

**Expected Response:**
```json
{
    "statusCode": 401,
  "message": "Google ID Token has expired"
}
```

---

**Scenario 3: ClientId Mismatch (ClientId không khớp)**

**Expected Response:**
```json
{
    "statusCode": 401,
    "message": "Invalid audience in Google ID Token"
}
```

---

## 10. Facebook Login Implementation (Optional - Triển khai Facebook Login)

### Bước 10.1: FacebookSignIn Implementation

**Làm gì:** Implement Facebook login logic (tương tự Google).

**Tại sao:** Support nhiều OAuth providers.

**File:** `src/Infrastructure/Infrastructure/Identity/AuthenticationService.cs` (update existing)

```csharp
using System.Net.Http.Json;

// ... existing code ...

/// <summary>
/// Đăng nhập bằng Facebook Access Token
/// </summary>
public async Task<TokenResponse> FacebookSignIn(string accessToken, string ipAddress)
{
    // 1. Validate Access Token và lấy user info từ Facebook Graph API
    using var httpClient = new HttpClient();
    var userInfoUrl = $"https://graph.facebook.com/me?" +
     $"fields=id,email,first_name,last_name,picture&" +
           $"access_token={accessToken}";

    var userInfoResponse = await httpClient.GetAsync(userInfoUrl);

    if (!userInfoResponse.IsSuccessStatusCode)
    {
        throw new UnauthorizedException("Invalid Facebook Access Token");
    }

 // 2. Parse user info
    var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<FacebookUserInfo>();

    if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
    {
        throw new UnauthorizedException("Unable to get user info from Facebook");
    }

    // 3. Tìm hoặc tạo user trong database
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
       ImageUrl = userInfo.Picture?.Data?.Url // Avatar từ Facebook
    };

    await _userManager.CreateAsync(existingUser);
        await _userManager.AddToRoleAsync(existingUser, ECORoles.Basic);
    }

    // 4. Generate JWT token
    var generateToken = await _tokenService.GenerateTokensAndUpdateUser(
        existingUser, 
        ipAddress);

    return generateToken;
}

/// <summary>
/// Facebook user info model (từ Graph API response)
/// </summary>
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
```

**Giải thích:**

**FacebookSignIn Flow:**
1. **Call Facebook Graph API:** Lấy user info bằng Access Token
2. **Validate Response:** Check response status và email
3. **Find/Create User:** Tìm hoặc tạo user trong database
4. **Generate JWT:** Tạo JWT token cho user

**Facebook Graph API:**
- Endpoint: `https://graph.facebook.com/me`
- Fields: `id, email, first_name, last_name, picture`
- Authentication: `access_token` parameter

---

## 11. Summary (Tổng kết)

### ✅ Đã hoàn thành trong bước này:

**Configuration Classes (Các Lớp Cấu hình):**
- ✅ GoogleAuthSettings (ClientId, ClientSecret)
- ✅ FacebookAuthSettings (AppId, AppSecret)

**OAuth2 Middleware:**
- ✅ OAuth2 Startup configuration
- ✅ Google authentication provider
- ✅ Facebook authentication provider

**Authentication Service:**
- ✅ IAuthenticationService interface
- ✅ GoogleSignIn (ID Token Flow)
- ✅ GoogleSignIn2 (Authorization Code Flow)
- ✅ FacebookSignIn implementation

**Controllers:**
- ✅ AuthController với OAuth2 endpoints
- ✅ /api/auth/google endpoint
- ✅ /api/auth/facebook endpoint

**Configuration:**
- ✅ appsettings.json configuration
- ✅ User Secrets setup
- ✅ Environment variables support

**Testing:**
- ✅ Google login test với Postman
- ✅ JWT token verification
- ✅ Error cases handling

### 📊 Complete OAuth2 Flow (Luồng OAuth2 Hoàn chỉnh):

```
┌─────────────────────────────────────────────────┐
│      COMPLETE OAUTH2 AUTHENTICATION FLOW        │
└─────────────────────────────────────────────────┘

1. USER ACTION
   User clicks "Login with Google" button
   └─> Frontend initiates OAuth2 flow

2. OAUTH2 PROVIDER AUTHENTICATION
   User redirected to Google login page
   └─> User enters credentials
   └─> User grants permissions
   └─> Google returns ID Token/Access Token

3. FRONTEND RECEIVES TOKEN
   Frontend receives token from Google
   └─> Send token to API

4. API VALIDATES TOKEN
   POST /api/auth/google { idToken: "..." }
   └─> AuthController.GoogleLogin()
   └─> AuthenticationService.GoogleSignIn()
   └─> Validate token với Google
   └─> Extract user info

5. USER MANAGEMENT
   Check if user exists in database
   ├─> New user: Create user + Assign "Basic" role
   └─> Existing user: Use existing user

6. JWT GENERATION
   Generate JWT token với permissions
   └─> TokenService.GenerateTokensAndUpdateUser()

7. RESPONSE
   Return JWT token to frontend
   └─> Frontend stores token
   └─> User logged in successfully ✅
```

### 📌 Key Concepts (Khái niệm Chính):

**OAuth2 Flows:**
- **ID Token Flow (Implicit Flow):**
  - Frontend validates với provider
  - API chỉ validate token signature
  - Đơn giản, phổ biến
  - Good for SPAs

- **Authorization Code Flow:**
  - API trao đổi code với provider backend-to-backend
  - Secure hơn
  - Recommended for production

**Token Types:**
- **Google ID Token:** JWT signed by Google, contains user info
- **Facebook Access Token:** Opaque token, cần call Graph API để lấy user info
- **JWT Token (ECO API):** Token của API, contains permissions

**Security Considerations (Cân nhắc Bảo mật):**
- Always validate tokens với provider
- Check token expiration
- Verify audience (ClientId match)
- Use HTTPS only
- Don't expose Client Secrets trong frontend

### 📁 Complete File Structure (Cấu trúc File Hoàn chỉnh):

```
src/
├── Core/
│   └── Application/
│    └── Identity/
│      └── O2Auth/
│               └── IAuthenticationService.cs
├── Infrastructure/
│   └── Infrastructure/
│       ├── Auth/
││   └── OAuth2/
│       │       ├── GoogleAuthSettings.cs
│       │├── FacebookAuthSettings.cs
│       │       └── Startup.cs
│     └── Identity/
│     └── AuthenticationService.cs
└── Host/
    └── Host/
     ├── Controllers/
    │   └── Identity/
  │       └── AuthController.cs
        └── appsettings.json (Authentication configuration)
```

---

## 12. Next Steps (Các Bước Tiếp theo)

**Tiếp theo:** [BUILD_19 - Caching Services](BUILD_19_Caching_Services.md)

Trong bước tiếp theo, chúng ta sẽ implement Caching:
1. ✅ ICacheService interface (Interface Dịch vụ Cache)
2. ✅ LocalCacheService (IMemoryCache) (Cache cục bộ)
3. ✅ DistributedCacheService (Redis/SQL Server) (Cache phân tán)
4. ✅ CacheSettings configuration (Cấu hình Cache)
5. ✅ Cache patterns (Cache-Aside, Write-Through) (Các mẫu Cache)

---

**Quay lại:** [Mục lục](BUILD_INDEX.md)
