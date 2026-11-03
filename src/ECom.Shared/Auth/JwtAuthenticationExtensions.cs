namespace ECom.Shared.Auth;

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public static class JwtAuthenticationExtensions
{
    private const int JweSeparatorCount = 4; // header.encryptedKey.iv.ciphertext.tag

    /// <summary>
    /// Registers JWT bearer validation shared by every service and the gateway.
    /// Issuer, audience, signing key and encryption key must be identical everywhere.
    /// Access tokens are JWEs (signed, then encrypted); plain signed JWTs are rejected.
    /// </summary>
    /// <param name="signalRHubPath">When set, the token may also be supplied as the
    /// <c>access_token</c> query-string parameter for requests to this path (SignalR WebSockets).</param>
    public static IServiceCollection AddEComJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string? signalRHubPath = null)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o =>
                !string.IsNullOrWhiteSpace(o.Issuer) &&
                !string.IsNullOrWhiteSpace(o.Audience) &&
                !string.IsNullOrWhiteSpace(o.SigningKey) &&
                o.SigningKey.Length >= 32 &&
                o.TryGetEncryptionKeyBytes() is not null &&
                o.ExpirationMinutes > 0 &&
                o.RefreshTokenExpirationDays > 0,
                "Jwt configuration is invalid. Ensure Issuer, Audience, SigningKey (≥32 chars), EncryptionKey (base64 of exactly 32 bytes), ExpirationMinutes (>0), and RefreshTokenExpirationDays (>0) are set.")
            .ValidateOnStart();

        // Configured at options-resolution time so JwtOptions stays the single source of truth.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    // Access tokens are JWEs: decrypt with this key before validating the inner signed JWT.
                    TokenDecryptionKey = new SymmetricSecurityKey(jwt.TryGetEncryptionKeyBytes()!),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // SignalR WebSocket connections cannot send HTTP headers, so the token is passed
                        // as a query-string parameter. Accept it only for the hub path.
                        if (!string.IsNullOrWhiteSpace(signalRHubPath))
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken) &&
                                context.HttpContext.Request.Path.StartsWithSegments(signalRHubPath))
                            {
                                context.Token = accessToken;
                            }
                        }

                        // TokenDecryptionKey lets the handler decrypt JWEs but does not require them, so a
                        // plain signed JWT would otherwise still validate. Insist on the 5-segment JWE shape.
                        var token = context.Token;
                        if (string.IsNullOrEmpty(token))
                        {
                            const string prefix = "Bearer ";
                            var header = context.Request.Headers.Authorization.ToString();
                            if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                token = header[prefix.Length..].Trim();
                        }

                        if (!string.IsNullOrEmpty(token) && token.Count(c => c == '.') != JweSeparatorCount)
                            context.Fail("Access tokens must be encrypted (JWE).");

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

        return services;
    }
}
