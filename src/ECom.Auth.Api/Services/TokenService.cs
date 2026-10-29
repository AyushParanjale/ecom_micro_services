namespace ECom.Auth.Api.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECom.Auth.Api.DTOs;
using ECom.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

/// <summary>Issues the JWTs that every other service (and the gateway) validates.</summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public TokenResult GenerateToken(Guid userId, string email, string name, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTimeOffset.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, name),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiry.UtcDateTime,
            signingCredentials: creds);

        return new TokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiry);
    }
}
