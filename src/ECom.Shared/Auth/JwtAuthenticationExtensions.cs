namespace ECom.Shared.Auth;

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer validation shared by every service and the gateway.
    /// Issuer, audience and signing key must be identical everywhere.
    /// </summary>
    /// <param name="signalRHubPath">When set, the token may also be supplied as the
    /// <c>access_token</c> query-string parameter for requests to this path (SignalR WebSockets).</param>
    public static IServiceCollection AddEComJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string? signalRHubPath = null)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.AddOptions<JwtOptions>()
            .Bind(jwtSection)
            .Validate(o =>
                !string.IsNullOrWhiteSpace(o.Issuer) &&
                !string.IsNullOrWhiteSpace(o.Audience) &&
                !string.IsNullOrWhiteSpace(o.SigningKey) &&
                o.SigningKey.Length >= 32 &&
                o.ExpirationMinutes > 0,
                "Jwt configuration is invalid. Ensure Issuer, Audience, SigningKey (≥32 chars), and ExpirationMinutes (>0) are set.")
            .ValidateOnStart();

        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
                        ? new SymmetricSecurityKey(new byte[32])
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                };

                if (!string.IsNullOrWhiteSpace(signalRHubPath))
                {
                    // SignalR WebSocket connections cannot send HTTP headers, so the JWT is passed
                    // as a query-string parameter. Accept it only for the hub path.
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken) &&
                                context.HttpContext.Request.Path.StartsWithSegments(signalRHubPath))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                }
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

        return services;
    }
}
