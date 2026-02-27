using NightMarket.WebApi.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace NightMarket.WebApi.Infrastructure.Auth.Jwt;

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
}
