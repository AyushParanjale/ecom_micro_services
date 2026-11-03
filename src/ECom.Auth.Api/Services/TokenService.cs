namespace ECom.Auth.Api.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECom.Auth.Api.DTOs;
using ECom.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

/// <summary>Issues the encrypted JWTs (JWE) that every other service (and the gateway) validates, and opaque refresh tokens.</summary>
public sealed class TokenService : ITokenService
{
    private const int RefreshTokenByteLength = 64;

    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public TokenResult GenerateToken(Guid userId, string email, string name, string role)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var encryptionKeyBytes = _options.TryGetEncryptionKeyBytes()
            ?? throw new InvalidOperationException("Jwt:EncryptionKey must be base64 of exactly 32 bytes.");
        var encryptingCredentials = new EncryptingCredentials(
            new SymmetricSecurityKey(encryptionKeyBytes),
            JwtConstants.DirectKeyUseAlg,
            SecurityAlgorithms.Aes128CbcHmacSha256);

        var now = _timeProvider.GetUtcNow();
        var expiry = now.AddMinutes(_options.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, name),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            }),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expiry.UtcDateTime,
            SigningCredentials = signingCredentials,
            EncryptingCredentials = encryptingCredentials,
        };

        // Sign-then-encrypt: the result is a 5-segment JWE whose payload is opaque to clients.
        var token = new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
        return new TokenResult(token, "Bearer", expiry);
    }

    public RefreshTokenResult GenerateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenByteLength));
        var expiry = _timeProvider.GetUtcNow().AddDays(_options.RefreshTokenExpirationDays);
        return new RefreshTokenResult(token, HashRefreshToken(token), expiry);
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
